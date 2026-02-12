namespace CAF.DB.Entities;

/// <summary>
/// Unified context data entity that combines data type with availability mechanism.
/// Each entry has exactly one availability type at a time.
/// </summary>
public class ContextData
{
    public int Id { get; set; }

    /// <summary>
    /// Profile this data belongs to. 0 for global data.
    /// </summary>
    public int ProfileId { get; set; } = 0;

    /// <summary>
    /// Navigation property to the profile
    /// </summary>
    [JsonIgnore]
    public Profile? Profile { get; set; }

    /// <summary>
    /// Display name for this data entry
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The main content of this data entry
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional summary for compressed retrieval
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Optional core facts extraction
    /// </summary>
    public string? CoreFacts { get; set; }

    /// <summary>
    /// The type of data this entry represents
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DataType Type { get; set; } = DataType.Generic;

    /// <summary>
    /// Current availability mechanism for this data
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AvailabilityType Availability { get; set; } = AvailabilityType.Archive;

    /// <summary>
    /// For CharacterProfile type: indicates if this is the user's profile (always loaded)
    /// </summary>
    public bool IsUser { get; set; } = false;

    /// <summary>
    /// Whether this data entry is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Whether this data entry is archived (soft delete)
    /// </summary>
    public bool IsArchived { get; set; } = false;

    #region Manual Toggle Fields

    /// <summary>
    /// For Manual availability: if true, use on the next turn only, then revert to PreviousAvailability
    /// </summary>
    public bool UseNextTurnOnly { get; set; } = false;

    /// <summary>
    /// For Manual availability: if true, use on every turn until toggled off
    /// </summary>
    public bool UseEveryTurn { get; set; } = false;

    /// <summary>
    /// Stores the previous availability type when temporarily switched to Manual
    /// Used to restore after "use next turn" completes
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AvailabilityType? PreviousAvailability { get; set; }

    #endregion Manual Toggle Fields

    #region Trigger Fields

    /// <summary>
    /// For Trigger availability: comma-separated keywords that activate this data
    /// </summary>
    public string? TriggerKeywords { get; set; }

    /// <summary>
    /// For Trigger availability: number of turns to look back for keyword matches
    /// </summary>
    public int TriggerLookbackTurns { get; set; } = 3;

    /// <summary>
    /// For Trigger availability: minimum keyword matches required
    /// </summary>
    public int TriggerMinMatchCount { get; set; } = 1;

    #endregion Trigger Fields

    #region Semantic Search Fields

    /// <summary>
    /// For Semantic availability: the vector embedding ID in Qdrant
    /// </summary>
    public string? VectorId { get; set; }

    /// <summary>
    /// For Semantic availability: when the embedding was last updated
    /// </summary>
    public DateTime? EmbeddingUpdatedAt { get; set; }

    /// <summary>
    /// Whether this entry has been indexed in the vector database
    /// </summary>
    public bool InVectorDb { get; set; } = false;

    #endregion Semantic Search Fields

    #region Source/Origin Fields

    /// <summary>
    /// Session this data originated from (for quotes, memories from sessions)
    /// </summary>
    public int? SourceSessionId { get; set; }

    /// <summary>
    /// Speaker/author of this content (for quotes, voice samples)
    /// </summary>
    public string? Speaker { get; set; }

    /// <summary>
    /// Subtype within the DataType (e.g., "dialogue", "narration", "internal" for quotes)
    /// </summary>
    public string? Subtype { get; set; }

    /// <summary>
    /// Nonverbal behavior/action description (for quotes from canon - body language, gestures, etc.)
    /// </summary>
    public string? NonverbalBehavior { get; set; }

    /// <summary>
    /// File system path to the source file (for file-based data that can be reloaded)
    /// </summary>
    public string? Path { get; set; }

    #endregion Source/Origin Fields

    #region Relevance/Scoring

    /// <summary>
    /// Manual relevance score (0-100) for prioritization
    /// </summary>
    public int RelevanceScore { get; set; } = 0;

    /// <summary>
    /// Explanation of why this entry is relevant
    /// </summary>
    public string? RelevanceReason { get; set; }

    /// <summary>
    /// Transient weight used during retrieval (not stored)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public decimal ProcessWeight { get; set; } = 0;

    #endregion Relevance/Scoring

    #region Repetition Prevention

    /// <summary>
    /// Last turn ID where this was used (prevents repetition)
    /// </summary>
    public int UsedLastOnTurnId { get; set; } = 0;

    /// <summary>
    /// Minimum turns before this can be used again (0 = no cooldown)
    /// </summary>
    public int CooldownTurns { get; set; } = 0;

    #endregion Repetition Prevention

    #region Display/Retrieval

    /// <summary>
    /// How to display/retrieve this data: full content, summary, or core facts
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RetrievalType Display { get; set; } = RetrievalType.Content;

    /// <summary>
    /// Sort order for display within same type
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Optional description for documentation
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    #endregion Display/Retrieval

    #region Audit Fields

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }

    #endregion Audit Fields

    #region Usage Tracking

    /// <summary>
    /// How many times this data has been loaded into context
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// When this data was last loaded into context
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// For Trigger: how many times triggered
    /// </summary>
    public int TriggerCount { get; set; } = 0;

    /// <summary>
    /// For Trigger: when last triggered
    /// </summary>
    public DateTime? LastTriggeredAt { get; set; }

    /// <summary>
    /// Cached token count from Gemini API (based on GetDisplayContent())
    /// </summary>
    public int? TokenCount { get; set; }

    /// <summary>
    /// When the token count was last updated
    /// </summary>
    public DateTime? TokenCountUpdatedAt { get; set; }

    #endregion Usage Tracking

    /// <summary>
    /// Gets the content to display based on the Display setting
    /// </summary>
    public string GetDisplayContent()
    {
        var baseContent = Display switch
        {
            RetrievalType.Summary => Summary ?? Content,
            RetrievalType.CoreFacts => CoreFacts ?? Content,
            _ => Content
        };

        return Type is DataType.Quote
            ? this.FormatAsQuote()
            : Type is DataType.PersonaVoiceSample ? this.FormatAsVoiceSampleQuote() : baseContent;
    }

    /// <summary>
    /// Gets keywords as a list for trigger matching
    /// </summary>
    public IReadOnlyList<string> GetKeywordList()
    {
        return string.IsNullOrWhiteSpace(TriggerKeywords)
            ? []
            : [.. TriggerKeywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())];
    }

    /// <summary>
    /// Validates that the current DataType supports the current AvailabilityType
    /// </summary>
    public bool IsValidCombination()
    {
        return (Type, Availability) switch
        {
            // Always on: Any
            (_, AvailabilityType.AlwaysOn) => true,

            // Archive: Any
            (_, AvailabilityType.Archive) => true,

            // Manual: Quote|Memory|Insight|CharacterProfile|Data
            (DataType.Quote, AvailabilityType.Manual) => true,
            (DataType.Memory, AvailabilityType.Manual) => true,
            (DataType.Insight, AvailabilityType.Manual) => true,
            (DataType.CharacterProfile, AvailabilityType.Manual) => true,
            (DataType.Generic, AvailabilityType.Manual) => true,
            (DataType.PersonaVoiceSample, AvailabilityType.Manual) => false,

            // Semantic: Quote|Memory|Insight|PersonaVoiceSample
            (DataType.Quote, AvailabilityType.Semantic) => true,
            (DataType.Memory, AvailabilityType.Semantic) => true,
            (DataType.Insight, AvailabilityType.Semantic) => true,
            (DataType.PersonaVoiceSample, AvailabilityType.Semantic) => true,
            (DataType.CharacterProfile, AvailabilityType.Semantic) => false,
            (DataType.Generic, AvailabilityType.Semantic) => false,

            // Trigger: CharacterProfile|Data|Memory|Insight
            (DataType.CharacterProfile, AvailabilityType.Trigger) => true,
            (DataType.Generic, AvailabilityType.Trigger) => true,
            (DataType.Memory, AvailabilityType.Trigger) => true,
            (DataType.Insight, AvailabilityType.Trigger) => true,
            (DataType.Quote, AvailabilityType.Trigger) => false,
            (DataType.PersonaVoiceSample, AvailabilityType.Trigger) => false,

            _ => false
        };
    }

    /// <summary>
    /// Checks if this entry is on cooldown and cannot be used yet
    /// </summary>
    /// <param name="currentTurnId">The current turn ID to check against</param>
    public bool IsOnCooldown(int currentTurnId)
    {
        return CooldownTurns > 0 && UsedLastOnTurnId > 0 && (currentTurnId - UsedLastOnTurnId) < CooldownTurns;
    }

    /// <summary>
    /// Marks this entry as used on the specified turn
    /// </summary>
    public void MarkUsed(int turnId)
    {
        UsedLastOnTurnId = turnId;
        UsageCount++;
        LastUsedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Types of data that can be stored and loaded into context
/// </summary>
public enum DataType
{
    /// <summary>
    /// Roleplay quotes/dialogue samples
    /// </summary>
    Quote,

    /// <summary>
    /// Voice/writing style samples for persona consistency
    /// </summary>
    PersonaVoiceSample,

    /// <summary>
    /// Story memories, events, facts
    /// </summary>
    Memory,

    /// <summary>
    /// Analytical insights, interpretations
    /// </summary>
    Insight,

    /// <summary>
    /// Character profiles (NPCs, user)
    /// </summary>
    CharacterProfile,

    /// <summary>
    /// Generic data/reference material
    /// </summary>
    Generic
}

/// <summary>
/// How data becomes available in the conversation context
/// </summary>
public enum AvailabilityType
{
    /// <summary>
    /// Always loaded into every turn
    /// </summary>
    AlwaysOn,

    /// <summary>
    /// Manually toggled: use next turn only, or use every turn
    /// </summary>
    Manual,

    /// <summary>
    /// Retrieved via semantic/vector similarity search
    /// </summary>
    Semantic,

    /// <summary>
    /// Activated by keyword triggers in recent turns
    /// </summary>
    Trigger,

    /// <summary>
    /// Archived, only recalled manually
    /// </summary>
    Archive
}

/// <summary>
/// How to retrieve/display the data content
/// </summary>
public enum RetrievalType
{
    /// <summary>
    /// Use full content
    /// </summary>
    Content,

    /// <summary>
    /// Use compressed summary
    /// </summary>
    Summary,

    /// <summary>
    /// Use extracted core facts
    /// </summary>
    CoreFacts
}