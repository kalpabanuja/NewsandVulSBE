using Microsoft.EntityFrameworkCore;
using NewsandVulSBE.Models;

namespace NewsandVulSBE.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Vulnerability> Vulnerabilities { get; set; }
    public DbSet<NewsArticle> NewsArticles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ensure CveId is unique
        modelBuilder.Entity<Vulnerability>()
            .HasIndex(v => v.CveId)
            .IsUnique();

        // Ensure Url is unique for news
        modelBuilder.Entity<NewsArticle>()
            .HasIndex(n => n.Url)
            .IsUnique();
    }
}
