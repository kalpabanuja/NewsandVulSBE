using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NewsandVulSBE.Data;
using NewsandVulSBE.Hubs;
using NewsandVulSBE.Models;
using NewsandVulSBE.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSignalR(); // Added SignalR

// Register HttpClient for the background workers
builder.Services.AddHttpClient();

// Register PostgreSQL DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Background Services
builder.Services.AddHostedService<MitreSyncService>();
builder.Services.AddHostedService<NistSyncService>();
builder.Services.AddHostedService<NewsSyncService>();

var app = builder.Build();

// Automatically apply migrations and seed data on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map SignalR Hub
app.MapHub<ThreatIntelHub>("/hubs/threats");

// --- LIVE QUERIES ---

app.MapGet("/api/vulnerabilities", async (AppDbContext db, int page = 1, int limit = 50, int? year = null, string? query = null, string? severity = null) =>
{
    var queryable = db.ReleasedVulnerabilities.AsQueryable();

    if (year.HasValue)
    {
        queryable = queryable.Where(v => v.PublishedDate.HasValue && v.PublishedDate.Value.Year == year.Value);
    }

    if (!string.IsNullOrWhiteSpace(severity) && severity != "ALL")
    {
        // Ensure case-insensitive or matching format depending on DB
        queryable = queryable.Where(v => v.Severity == severity);
    }

    if (!string.IsNullOrWhiteSpace(query))
    {
        var queryLower = query.ToLower();
        queryable = queryable.Where(v => 
            v.CveId.ToLower().Contains(queryLower) || 
            (v.Description != null && v.Description.ToLower().Contains(queryLower)));
    }

    var released = await queryable
        .OrderByDescending(v => v.PublishedDate ?? DateTime.UtcNow)
        .Skip((page - 1) * limit)
        .Take(limit)
        .Select(v => new VulnDto(v.CveId, v.Title, v.Description, v.CvssScore, v.Severity, v.PublishedDate, false, null))
        .ToListAsync();

    return Results.Ok(released);
})
.WithName("GetVulnerabilities");

app.MapGet("/api/vulnerabilities/count", async (AppDbContext db, int? year = null, string? query = null, string? severity = null) =>
{
    var queryable = db.ReleasedVulnerabilities.AsQueryable();

    if (year.HasValue)
    {
        queryable = queryable.Where(v => v.PublishedDate.HasValue && v.PublishedDate.Value.Year == year.Value);
    }

    if (!string.IsNullOrWhiteSpace(severity) && severity != "ALL")
    {
        queryable = queryable.Where(v => v.Severity == severity);
    }

    if (!string.IsNullOrWhiteSpace(query))
    {
        var queryLower = query.ToLower();
        queryable = queryable.Where(v => 
            v.CveId.ToLower().Contains(queryLower) || 
            (v.Description != null && v.Description.ToLower().Contains(queryLower)));
    }

    int count = await queryable.CountAsync();
    return Results.Ok(count);
})
.WithName("GetVulnerabilitiesCount");

app.MapGet("/api/vulnerabilities/pending", async (AppDbContext db, int page = 1, int limit = 50) =>
{
    var pending = await db.PendingVulnerabilities
        .OrderByDescending(v => v.DiscoveredAt)
        .Skip((page - 1) * limit)
        .Take(limit)
        .Select(v => new VulnDto(v.CveId, null, v.Description, null, null, null, true, v.LastCheckedWithNist))
        .ToListAsync();

    return Results.Ok(pending);
})
.WithName("GetPendingVulnerabilities");

app.MapGet("/api/vulnerabilities/{cveId}", async (AppDbContext db, IHttpClientFactory httpClientFactory, string cveId) =>
{
    var vul = await db.ReleasedVulnerabilities.FirstOrDefaultAsync(v => v.CveId == cveId);
    if (vul == null)
    {
        return Results.NotFound(new { Message = "Vulnerability not found in released database." });
    }

    if (string.IsNullOrEmpty(vul.RawNistJson))
    {
        // Smart Fallback: Fetch missing JSON from NIST
        try
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NewsandVulSBE-Agent");
            
            var url = $"https://services.nvd.nist.gov/rest/json/cves/2.0?cveId={cveId}";
            var response = await client.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                var resultsPerPage = root.GetProperty("resultsPerPage").GetInt32();
                if (resultsPerPage > 0)
                {
                    var cveItems = root.GetProperty("vulnerabilities");
                    if (cveItems.GetArrayLength() > 0)
                    {
                        var cveData = cveItems[0].GetProperty("cve");
                        vul.RawNistJson = cveData.GetRawText();
                        await db.SaveChangesAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Smart fallback failed for {cveId}: {ex.Message}");
            // Continue without raw JSON if fetching fails
        }
    }

    var detailedVul = new DetailedVulnDto(
        vul.CveId,
        vul.Title,
        vul.Description,
        vul.CvssScore,
        vul.Severity,
        vul.PublishedDate,
        $"https://nvd.nist.gov/vuln/detail/{vul.CveId}",
        vul.RawNistJson != null ? JsonSerializer.Deserialize<JsonElement>(vul.RawNistJson) : null
    );

    return Results.Ok(detailedVul);
})
.WithName("GetVulnerabilityDetails");

app.MapGet("/api/vulnerabilities/search", async (AppDbContext db, string q) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest("Query parameter 'q' is required.");

    var queryLower = q.ToLower();
    var released = await db.ReleasedVulnerabilities
        .Where(v => v.CveId.ToLower().Contains(queryLower) || (v.Description != null && v.Description.ToLower().Contains(queryLower)))
        .OrderByDescending(v => v.PublishedDate ?? DateTime.UtcNow)
        .Take(50)
        .Select(v => new VulnDto(v.CveId, v.Title, v.Description, v.CvssScore, v.Severity, v.PublishedDate, false, null))
        .ToListAsync();
        
    return Results.Ok(released);
})
.WithName("SearchVulnerabilities");

app.MapGet("/api/news", async (AppDbContext db, int page = 1, int limit = 50) =>
{
    var news = await db.NewsArticles
        .OrderByDescending(n => n.PublishedAt)
        .Skip((page - 1) * limit)
        .Take(limit)
        .ToListAsync();
        
    return Results.Ok(news);
})
.WithName("GetNews");

app.MapGet("/api/news/search", async (AppDbContext db, string q) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest("Query parameter 'q' is required.");

    var queryLower = q.ToLower();
    var news = await db.NewsArticles
        .Where(n => n.Title.ToLower().Contains(queryLower) || (n.Summary != null && n.Summary.ToLower().Contains(queryLower)))
        .OrderByDescending(n => n.PublishedAt)
        .Take(50)
        .ToListAsync();
        
    return Results.Ok(news);
})
.WithName("SearchNews");

// --- OFFLINE SYNC ---

app.MapGet("/api/sync/cves", async (AppDbContext db, DateTime startDate, DateTime endDate) =>
{
    var vulnerabilities = await db.ReleasedVulnerabilities
        .Where(v => v.PublishedDate >= startDate && v.PublishedDate <= endDate)
        .Select(v => new VulnDto(v.CveId, v.Title, v.Description, v.CvssScore, v.Severity, v.PublishedDate, false, null))
        .ToListAsync();
        
    return Results.Ok(vulnerabilities);
})
.WithName("SyncCves");

app.MapGet("/api/sync/news", async (AppDbContext db, DateTime since) =>
{
    var news = await db.NewsArticles
        .Where(n => n.PublishedAt > since)
        .ToListAsync();
        
    return Results.Ok(news);
})
.WithName("SyncNews");

app.MapGet("/api/stats", async (AppDbContext db) =>
{
    var stats = new
    {
        PendingVulnerabilities = await db.PendingVulnerabilities.CountAsync(),
        ReleasedVulnerabilities = await db.ReleasedVulnerabilities.CountAsync(),
        NewsArticles = await db.NewsArticles.CountAsync()
    };
    return Results.Ok(stats);
})
.WithName("GetStats");

// --- SIMPLE UI DASHBOARD ---
app.MapGet("/", () => 
{
    var html = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Threat Intel Backend Status</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 20px; background-color: #f8f9fa; color: #343a40; }
        h1 { border-bottom: 2px solid #007bff; padding-bottom: 10px; color: #0056b3; }
        .dashboard { display: flex; gap: 20px; flex-wrap: wrap; margin-top: 20px; }
        .panel { flex: 1; min-width: 300px; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }
        .panel h2 { margin-top: 0; font-size: 1.2rem; color: #495057; border-bottom: 1px solid #dee2e6; padding-bottom: 10px; }
        .item { margin-bottom: 15px; padding-bottom: 15px; border-bottom: 1px solid #f1f3f5; }
        .item:last-child { border-bottom: none; margin-bottom: 0; padding-bottom: 0; }
        .item-title { font-weight: bold; color: #d9534f; }
        .news-title { font-weight: bold; color: #0275d8; }
        .item-meta { font-size: 0.85em; color: #6c757d; margin-top: 5px; }
        .status { padding: 10px; background-color: #d4edda; color: #155724; border-radius: 5px; margin-bottom: 20px; display: inline-block; font-weight: bold; }
        .stats-panel { display: flex; gap: 20px; margin-bottom: 20px; }
        .stat-card { background: white; padding: 15px 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); border-left: 4px solid #17a2b8; min-width: 150px; }
        .stat-value { font-size: 1.8rem; font-weight: bold; color: #343a40; margin-bottom: 5px; }
        .stat-label { font-size: 0.9rem; color: #6c757d; text-transform: uppercase; letter-spacing: 1px; }
    </style>
</head>
<body>
    <h1>🛡️ Threat Intel Backend</h1>
    <div class=""status"">✅ Server is running and monitoring data streams</div>
    <p>Below is a live preview of the recent data available in the PostgreSQL database.</p>

    <div class=""stats-panel"" id=""stats-container"">
        <div class=""stat-card"">
            <div class=""stat-value"" id=""count-pending-vuln"">...</div>
            <div class=""stat-label"">Pending CVEs</div>
        </div>
        <div class=""stat-card"">
            <div class=""stat-value"" id=""count-released-vuln"">...</div>
            <div class=""stat-label"">Released CVEs</div>
        </div>
        <div class=""stat-card"">
            <div class=""stat-value"" id=""count-news"">...</div>
            <div class=""stat-label"">News Articles</div>
        </div>
    </div>

    <div class=""dashboard"">
        <div class=""panel"">
            <h2>Pending Vulnerabilities (CVEs)</h2>
            <div id=""pending-list"">Loading pending vulnerabilities...</div>
        </div>
        <div class=""panel"">
            <h2>Recent Updated Vulnerabilities (CVEs)</h2>
            <div id=""cve-list"">Loading vulnerabilities...</div>
        </div>
        <div class=""panel"">
            <h2>Recent Security News</h2>
            <div id=""news-list"">Loading news...</div>
        </div>
    </div>

    <script>
        async function loadData() {
            try {
                // Fetch Stats
                const statsRes = await fetch('/api/stats');
                const stats = await statsRes.json();
                document.getElementById('count-pending-vuln').innerText = stats.pendingVulnerabilities.toLocaleString();
                document.getElementById('count-released-vuln').innerText = stats.releasedVulnerabilities.toLocaleString();
                document.getElementById('count-news').innerText = stats.newsArticles.toLocaleString();

                // Fetch Released CVEs
                const cveRes = await fetch('/api/vulnerabilities?limit=5');
                const cves = await cveRes.json();
                const cveContainer = document.getElementById('cve-list');
                
                if(cves.length === 0) {
                    cveContainer.innerHTML = '<em>No released vulnerabilities found yet.</em>';
                } else {
                    cveContainer.innerHTML = cves.map(c => `
                        <div class=""item"">
                            <div class=""item-title"">${c.cveId || 'Unknown'}</div>
                            <div>${c.description ? c.description.substring(0, 150) + '...' : 'No description available'}</div>
                            <div class=""item-meta"">Published: ${c.publishedDate ? new Date(c.publishedDate).toLocaleString() : 'Date unknown'}</div>
                        </div>
                    `).join('');
                }

                // Fetch Pending CVEs
                const pendingRes = await fetch('/api/vulnerabilities/pending?limit=5');
                const pendingCves = await pendingRes.json();
                const pendingContainer = document.getElementById('pending-list');
                
                if(pendingCves.length === 0) {
                    pendingContainer.innerHTML = '<em>No pending vulnerabilities.</em>';
                } else {
                    pendingContainer.innerHTML = pendingCves.map(c => `
                        <div class=""item"">
                            <div class=""item-title"">${c.cveId || 'Unknown'}</div>
                            <div>${c.description ? c.description.substring(0, 150) + '...' : 'No description available'}</div>
                            <div class=""item-meta"">Pending (Last Checked: ${c.lastChecked ? new Date(c.lastChecked).toLocaleString() : 'Never'})</div>
                        </div>
                    `).join('');
                }

                // Fetch News
                const newsRes = await fetch('/api/news?limit=5');
                const news = await newsRes.json();
                const newsContainer = document.getElementById('news-list');
                
                if(news.length === 0) {
                    newsContainer.innerHTML = '<em>No news found in database yet. Wait for background sync.</em>';
                } else {
                    newsContainer.innerHTML = news.map(n => `
                        <div class=""item"">
                            <div class=""news-title"">${n.title || 'Untitled'}</div>
                            <div class=""item-meta"">Published: ${n.publishedAt ? new Date(n.publishedAt).toLocaleString() : 'Date unknown'}</div>
                        </div>
                    `).join('');
                }
            } catch (error) {
                console.error('Error fetching data:', error);
                document.getElementById('pending-list').innerHTML = '<span style=""color:red"">Failed to connect to API.</span>';
                document.getElementById('cve-list').innerHTML = '<span style=""color:red"">Failed to connect to API.</span>';
                document.getElementById('news-list').innerHTML = '<span style=""color:red"">Failed to connect to API.</span>';
            }
        }

        // Load immediately and refresh every 10 seconds
        loadData();
        setInterval(loadData, 10000);
    </script>
</body>
</html>";
    return Results.Content(html, "text/html");
});

app.Run();

public record VulnDto(string CveId, string? Title, string? Description, float? CvssScore, string? Severity, DateTime? PublishedDate, bool IsPending, DateTime? LastChecked);

public record DetailedVulnDto(string CveId, string? Title, string? Description, float? CvssScore, string? Severity, DateTime? PublishedDate, string OfficialUrl, object? RawNistData);
