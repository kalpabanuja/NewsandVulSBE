using Microsoft.EntityFrameworkCore;
using NewsandVulSBE.Data;
using NewsandVulSBE.Hubs;
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

// Automatically apply migrations to the database on startup (Great for Docker)
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

app.MapGet("/api/vulnerabilities", async (AppDbContext db, int page = 1, int limit = 50) =>
{
    var vulnerabilities = await db.Vulnerabilities
        .OrderByDescending(v => v.PublishedDate ?? DateTime.UtcNow) // Sort newest first
        .Skip((page - 1) * limit)
        .Take(limit)
        .ToListAsync();
        
    return Results.Ok(vulnerabilities);
})
.WithName("GetVulnerabilities");

app.MapGet("/api/vulnerabilities/search", async (AppDbContext db, string q) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest("Query parameter 'q' is required.");

    var queryLower = q.ToLower();
    var vulnerabilities = await db.Vulnerabilities
        .Where(v => v.CveId.ToLower().Contains(queryLower) || (v.Description != null && v.Description.ToLower().Contains(queryLower)))
        .OrderByDescending(v => v.PublishedDate ?? DateTime.UtcNow)
        .Take(50)
        .ToListAsync();
        
    return Results.Ok(vulnerabilities);
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
    var vulnerabilities = await db.Vulnerabilities
        .Where(v => v.PublishedDate >= startDate && v.PublishedDate <= endDate)
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

app.Run();
