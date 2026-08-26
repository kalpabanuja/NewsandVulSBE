using System.ServiceModel.Syndication;
using System.Xml;
using NewsandVulSBE.Data;
using NewsandVulSBE.Models;
using NewsandVulSBE.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace NewsandVulSBE.Services;

public class NewsSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NewsSyncService> _logger;
    private readonly IHubContext<ThreatIntelHub> _hubContext;
    private const string HackerNewsRssUrl = "https://feeds.feedburner.com/TheHackersNews";

    public NewsSyncService(IServiceProvider serviceProvider, ILogger<NewsSyncService> logger, IHubContext<ThreatIntelHub> hubContext)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NewsSyncService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncNewsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while syncing news.");
            }

            // Wait 5 minutes before checking again
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task SyncNewsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var reader = XmlReader.Create(HackerNewsRssUrl, new XmlReaderSettings { Async = true });
        var feed = SyndicationFeed.Load(reader);

        var addedArticles = new List<NewsArticle>();

        foreach (var item in feed.Items)
        {
            var url = item.Links.FirstOrDefault()?.Uri.ToString() ?? "";
            
            // Check if article already exists
            if (!dbContext.NewsArticles.Any(n => n.Url == url))
            {
                var article = new NewsArticle
                {
                    Title = item.Title.Text,
                    Url = url,
                    Summary = item.Summary?.Text ?? string.Empty,
                    Source = "The Hacker News",
                    PublishedAt = item.PublishDate.UtcDateTime
                };

                dbContext.NewsArticles.Add(article);
                addedArticles.Add(article);
            }
        }

        if (addedArticles.Count > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Saved {AddedCount} new articles from The Hacker News.", addedArticles.Count);
            
            // Notify clients via SignalR
            await _hubContext.Clients.All.SendAsync("ReceiveNewNews", addedArticles, stoppingToken);
        }
    }
}
