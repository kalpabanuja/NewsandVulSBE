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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map SignalR Hub
app.MapHub<ThreatIntelHub>("/hubs/threatintel");

// Minimal API Endpoints
app.MapGet("/api/vulnerabilities", async (AppDbContext db, int page = 1, int pageSize = 50) =>
{
    var vulnerabilities = await db.Vulnerabilities
        .OrderByDescending(v => v.PublishedDate ?? DateTime.UtcNow) // Sort newest first
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
        
    return Results.Ok(vulnerabilities);
})
.WithName("GetVulnerabilities");

app.MapGet("/api/news", async (AppDbContext db, int page = 1, int pageSize = 50) =>
{
    var news = await db.NewsArticles
        .OrderByDescending(n => n.PublishedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
        
    return Results.Ok(news);
})
.WithName("GetNews");

app.Run();
