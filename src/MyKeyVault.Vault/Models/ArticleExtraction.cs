using System.ComponentModel.DataAnnotations;

namespace MyKeyVault.Vault.Models;

public sealed class ArticleExtraction
{
    public long Id { get; set; }
    public long ArticleId { get; set; }
    public KnowledgeArticle Article { get; set; } = default!;
    [Required, MaxLength(450)] public string OwnerId { get; set; } = string.Empty;
    [Required, MaxLength(4000)] public string Prompt { get; set; } = string.Empty;
    public string? Result { get; set; }
    [MaxLength(100)] public string? ModelUsed { get; set; }
    public int? TokensUsed { get; set; }
    [Required, MaxLength(24)] public string Status { get; set; } = "processing";
    [MaxLength(600)] public string? ErrorMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
}
