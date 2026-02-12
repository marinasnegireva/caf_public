using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CAF.DB.Entities;

/// <summary>
/// Database entity for storing LLM request logs
/// </summary>
[Table("LLMRequestLogs")]
public class LLMRequestLogEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string RequestId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Operation { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public long DurationMs { get; set; }

    // Request Details
    public string? Prompt { get; set; }

    public string? SystemInstruction { get; set; }

    public int InputTokens { get; set; }

    // Raw vendor request/response
    public string? RawRequestJson { get; set; }

    public string? RawResponseJson { get; set; }

    // Response Details
    public string? GeneratedText { get; set; }

    public int OutputTokens { get; set; }

    // Token Details (from API usage metadata)
    public int TotalTokens { get; set; }

    public int CachedContentTokenCount { get; set; } // Tokens served from cache

    public int ThinkingTokens { get; set; } // Reasoning tokens (for thinking models)

    [Column(TypeName = "decimal(18, 6)")]
    public decimal TotalCost { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    // Error Details
    public int? StatusCode { get; set; }

    // Foreign Key
    public int? TurnId { get; set; }

    [ForeignKey(nameof(TurnId))]
    public Turn? Turn { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Indexes will be defined in the DbContext configuration
}