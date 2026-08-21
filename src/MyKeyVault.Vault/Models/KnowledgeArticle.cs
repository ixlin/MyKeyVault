using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public sealed class KnowledgeArticle
{
    public long Id { get; set; }

    [Required, MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string SourceUrl { get; set; } = string.Empty;

    [MaxLength(100)] public string? StorageKey { get; set; }
    [MaxLength(240)] public string? Title { get; set; }
    [MaxLength(120)] public string? Author { get; set; }
    [MaxLength(80)] public string? PublishedText { get; set; }
    [MaxLength(240)] public string? HtmlFileName { get; set; }
    [MaxLength(240)] public string? PdfFileName { get; set; }
    public int ImagesCount { get; set; }
    public int VideosCount { get; set; }
    [Required, MaxLength(24)] public string Status { get; set; } = "pending";
    [MaxLength(120)] public string? TaskId { get; set; }
    [MaxLength(600)] public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public ICollection<ArticleExtraction> Extractions { get; set; } = new List<ArticleExtraction>();
}
