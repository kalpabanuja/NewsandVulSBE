using SQLite;
using System;

namespace ThreatIntelClient.Models;

public class Cve
{
    [PrimaryKey]
    public string Id { get; set; }
    
    public string Description { get; set; }
    
    public double CvssScore { get; set; }
    
    public string Severity { get; set; }
    
    public DateTime PublishedDate { get; set; }
    
    public DateTime LastModifiedDate { get; set; }
}
