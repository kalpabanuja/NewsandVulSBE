using System.Text.Json;
using System.Text.RegularExpressions;
using NewsandVulSBE.Data;
using NewsandVulSBE.Models;
using NewsandVulSBE.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace NewsandVulSBE.Services;

public class MitreSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MitreSyncService> _logger;
    private readonly IHubContext<ThreatIntelHub> _hubContext;

    public MitreSyncService(IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory, ILogger<MitreSyncService> logger, IHubContext<ThreatIntelHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MitreSyncService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncMitreAsync(stoppingToken);
                await SyncMitreDescriptionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing MITRE CVEs.");
            }
            
            // Wait 1 minute between checks to handle pending descriptions without hitting rate limits too hard
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task SyncMitreAsync(CancellationToken stoppingToken)
    {
        // Simple implementation: Fetch recent commits from the cvelistV5 repo
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "NewsandVulSBE-Agent");
        
        var response = await client.GetAsync("https://api.github.com/repos/CVEProject/cvelistV5/commits?per_page=30", stoppingToken);
        if (!response.IsSuccessStatusCode) return;

        var content = await response.Content.ReadAsStringAsync(stoppingToken);
        var commits = JsonDocument.Parse(content).RootElement;

        var newCveIds = new HashSet<string>();
        foreach (var commit in commits.EnumerateArray())
        {
            var message = commit.GetProperty("commit").GetProperty("message").GetString() ?? "";
            
            // Extract CVE IDs from commit messages
            var matches = Regex.Matches(message, @"CVE-\d{4}-\d{4,7}");
            foreach (Match match in matches)
            {
                newCveIds.Add(match.Value);
            }
        }

        if (newCveIds.Any())
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            int added = 0;
            foreach (var cveId in newCveIds)
            {
                if (!dbContext.PendingVulnerabilities.Any(v => v.CveId == cveId) && 
                    !dbContext.ReleasedVulnerabilities.Any(v => v.CveId == cveId))
                {
                    dbContext.PendingVulnerabilities.Add(new PendingVulnerability
                    {
                        CveId = cveId,
                        DiscoveredAt = DateTime.UtcNow
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Added {AddedCount} new pending CVEs from MITRE.", added);
                
                // Notify clients via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveNewCve", newCveIds, stoppingToken);
            }
        }
    }

    private async Task SyncMitreDescriptionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingVuls = await dbContext.PendingVulnerabilities
            .Where(v => v.Description == null)
            .Take(10) // Batch of 10
            .ToListAsync(stoppingToken);

        if (!pendingVuls.Any()) return;

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "NewsandVulSBE-Agent");

        foreach (var vul in pendingVuls)
        {
            try
            {
                var url = $"https://cveawg.mitre.org/api/cve/{vul.CveId}";
                var response = await client.GetAsync(url, stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("containers", out var containers) && containers.TryGetProperty("cna", out var cna))
                    {
                        if (cna.TryGetProperty("descriptions", out var descs) && descs.GetArrayLength() > 0)
                        {
                            foreach (var desc in descs.EnumerateArray())
                            {
                                if (desc.GetProperty("lang").GetString() == "en")
                                {
                                    vul.Description = desc.GetProperty("value").GetString();
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // If it fails or not found, we don't want to get stuck in an infinite loop. 
                // We'll set description to a placeholder if we still couldn't find one.
                if (string.IsNullOrEmpty(vul.Description))
                {
                    vul.Description = "Awaiting description from MITRE/NIST.";
                }

                await Task.Delay(1000, stoppingToken); // Respect rate limits
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch description for {CveId} from MITRE.", vul.CveId);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
