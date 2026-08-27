using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewsandVulSBE.Data;
using NewsandVulSBE.Hubs;
using NewsandVulSBE.Models;
using Microsoft.AspNetCore.SignalR;

namespace NewsandVulSBE.Services;

public class NistSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NistSyncService> _logger;
    private readonly IHubContext<ThreatIntelHub> _hubContext;

    public NistSyncService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<NistSyncService> logger, IHubContext<ThreatIntelHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NistSyncService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncNistAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while enriching CVEs from NIST.");
            }
            
            // Poll every 1 minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SyncNistAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find vulnerabilities that are pending and haven't been checked recently
        var pendingVuls = await dbContext.PendingVulnerabilities
            .Where(v => v.LastCheckedWithNist == null || v.LastCheckedWithNist < DateTime.UtcNow.AddHours(-1))
            .Take(10) // Batch of 10
            .ToListAsync(stoppingToken);

        if (!pendingVuls.Any()) return;

        var client = _httpClientFactory.CreateClient();
        var apiKey = _configuration["NistApiKey"];
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("apiKey", apiKey);
        }

        foreach (var vul in pendingVuls)
        {
            try
            {
                var url = $"https://services.nvd.nist.gov/rest/json/cves/2.0?cveId={vul.CveId}";
                var response = await client.GetAsync(url, stoppingToken);
                
                vul.LastCheckedWithNist = DateTime.UtcNow;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);
                    using var doc = JsonDocument.Parse(content);
                    
                    var root = doc.RootElement;
                    var resultsPerPage = root.GetProperty("resultsPerPage").GetInt32();

                    if (resultsPerPage > 0)
                    {
                        var cveItems = root.GetProperty("vulnerabilities");
                        if (cveItems.GetArrayLength() > 0)
                        {
                            var cveData = cveItems[0].GetProperty("cve");
                            var releasedVul = new ReleasedVulnerability { 
                                CveId = vul.CveId,
                                RawNistJson = cveData.GetRawText()
                            };
                            
                            if (cveData.TryGetProperty("descriptions", out var descs) && descs.GetArrayLength() > 0)
                            {
                                // Get English description if possible
                                foreach (var desc in descs.EnumerateArray())
                                {
                                    if (desc.GetProperty("lang").GetString() == "en")
                                    {
                                        releasedVul.Description = desc.GetProperty("value").GetString();
                                        break;
                                    }
                                }
                            }

                            if (cveData.TryGetProperty("metrics", out var metrics))
                            {
                                if (metrics.TryGetProperty("cvssMetricV31", out var cvssV31) && cvssV31.GetArrayLength() > 0)
                                {
                                    var cvssData = cvssV31[0].GetProperty("cvssData");
                                    releasedVul.CvssScore = (float)cvssData.GetProperty("baseScore").GetDouble();
                                    releasedVul.Severity = cvssData.GetProperty("baseSeverity").GetString();
                                }
                            }

                            if (cveData.TryGetProperty("published", out var publishedDate))
                            {
                                releasedVul.PublishedDate = publishedDate.GetDateTime();
                            }

                            dbContext.ReleasedVulnerabilities.Add(releasedVul);
                            dbContext.PendingVulnerabilities.Remove(vul);
                            _logger.LogInformation("Successfully analyzed {CveId} from NIST.", vul.CveId);
                            
                            // Notify clients via SignalR
                            await _hubContext.Clients.All.SendAsync("ReceiveNewCve", releasedVul, stoppingToken);
                        }
                    }
                }
                
                // Sleep to respect NIST API rate limits (even with API key)
                await Task.Delay(1000, stoppingToken); 
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch {CveId} from NIST.", vul.CveId);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
