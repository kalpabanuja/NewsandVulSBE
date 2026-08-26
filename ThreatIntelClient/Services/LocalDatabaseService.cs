using SQLite;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using ThreatIntelClient.Models;
using System;

namespace ThreatIntelClient.Services;

public class LocalDatabaseService
{
    private SQLiteAsyncConnection _database;
    private readonly string _dbPath;

    public LocalDatabaseService()
    {
        _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThreatIntel.db3");
    }

    private async Task InitAsync()
    {
        if (_database != null)
            return;

        _database = new SQLiteAsyncConnection(_dbPath);
        await _database.CreateTableAsync<Cve>();
        await _database.CreateTableAsync<NewsArticle>();
        
        // Setup FTS tables here later if needed
    }

    public async Task<List<Cve>> GetCvesAsync()
    {
        await InitAsync();
        return await _database.Table<Cve>().OrderByDescending(c => c.PublishedDate).ToListAsync();
    }

    public async Task<List<Cve>> SearchCvesAsync(string query, string severityFilter, int limit = 50, int offset = 0)
    {
        await InitAsync();
        
        var table = _database.Table<Cve>();

        if (!string.IsNullOrWhiteSpace(severityFilter) && severityFilter != "ALL")
        {
            table = table.Where(c => c.Severity == severityFilter);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            string lowerQuery = query.ToLower();
            table = table.Where(c => c.Id.ToLower().Contains(lowerQuery) || c.Description.ToLower().Contains(lowerQuery));
        }

        return await table.OrderByDescending(c => c.PublishedDate).Skip(offset).Take(limit).ToListAsync();
    }

    public async Task SaveCveAsync(Cve cve)
    {
        await InitAsync();
        await _database.InsertOrReplaceAsync(cve);
    }
    
    public async Task SaveCvesAsync(IEnumerable<Cve> cves)
    {
        await InitAsync();
        await _database.InsertAllAsync(cves, "OR REPLACE");
    }

    public async Task<List<NewsArticle>> GetNewsArticlesAsync()
    {
        await InitAsync();
        return await _database.Table<NewsArticle>().OrderByDescending(n => n.PublishedDate).ToListAsync();
    }

    public async Task<List<NewsArticle>> SearchNewsArticlesAsync(string query, int limit = 50, int offset = 0)
    {
        await InitAsync();
        
        var table = _database.Table<NewsArticle>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            string lowerQuery = query.ToLower();
            table = table.Where(n => n.Title.ToLower().Contains(lowerQuery) || n.Summary.ToLower().Contains(lowerQuery));
        }

        return await table.OrderByDescending(n => n.PublishedDate).Skip(offset).Take(limit).ToListAsync();
    }

    public async Task SaveNewsArticleAsync(NewsArticle article)
    {
        await InitAsync();
        await _database.InsertOrReplaceAsync(article);
    }

    public async Task PruneDatabaseAsync(int cutoffYear)
    {
        await InitAsync();
        // Since DateTime is stored as ticks/string depending on sqlite-net settings,
        // The safest way is to fetch and delete, or format date in SQL.
        // For simplicity we will do a LINQ query to find IDs to delete to remain DB agnostic
        var cutoffDate = new DateTime(cutoffYear, 1, 1);
        
        var oldCves = await _database.Table<Cve>().Where(c => c.PublishedDate < cutoffDate).ToListAsync();
        foreach (var item in oldCves) await _database.DeleteAsync(item);

        var oldNews = await _database.Table<NewsArticle>().Where(n => n.PublishedDate < cutoffDate).ToListAsync();
        foreach (var item in oldNews) await _database.DeleteAsync(item);

        // Run Vacuum to reclaim space
        await _database.ExecuteAsync("VACUUM;");
    }

    public async Task<(int CveCount, int NewsCount)> GetDatabaseMetricsAsync()
    {
        await InitAsync();
        int cveCount = await _database.Table<Cve>().CountAsync();
        int newsCount = await _database.Table<NewsArticle>().CountAsync();
        return (cveCount, newsCount);
    }
}
