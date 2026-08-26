using SQLite;
using System;

namespace ThreatIntelClient.Models;

public class NewsArticle
{
    [PrimaryKey]
    public string Id { get; set; }
    
    public string Title { get; set; }
    
    public string Summary { get; set; }
    
    public string Url { get; set; }
    
    public string Source { get; set; }
    
    public string ThumbnailUrl { get; set; }
    
    public DateTime PublishedDate { get; set; }
}
