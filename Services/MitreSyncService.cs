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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while syncing MITRE CVEs.");
            }
            
            // Wait 10 minutes between checks
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
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
                if (!dbContext.Vulnerabilities.Any(v => v.CveId == cveId))
                {
                    dbContext.Vulnerabilities.Add(new Vulnerability
                    {
                        CveId = cveId,
                        Status = "Pending Research"
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Added {AddedCount} new pending CVEs from MITRE.", added);
                
                // Notify clients via SignalR
                await _hubContext.Clients.All.SendAsync("ReceiveNewVulnerabilities", newCveIds, stoppingToken);
            }
        }
    }
}
