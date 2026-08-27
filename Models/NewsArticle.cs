using System.ComponentModel.DataAnnotations;

namespace NewsandVulSBE.Models;

public class NewsArticle
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public required string Title { get; set; }

    [Required]
    public required string Url { get; set; }

    public string? Summary { get; set; }

    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(100)]
    public required string Source { get; set; } = "The Hacker News";

    public DateTime PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
