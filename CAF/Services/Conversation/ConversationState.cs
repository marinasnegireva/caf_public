namespace CAF.Services.Conversation;

/// <summary>
/// Represents the context for a conversation turn, including user/persona information,
/// previous turns, and dialogue history.
/// </summary>
public class ConversationState
{
    /// <summary>
    /// The current turn being processed
    /// </summary>
    public Turn CurrentTurn { get; set; } = null!;

    /// <summary>
    /// The active session for this conversation
    /// </summary>
    public Session Session { get; set; } = null!;

    /// <summary>
    /// The user's name
    /// </summary>
    public string UserName { get; set; } = "User";

    /// <summary>
    /// The persona's name
    /// </summary>
    public string PersonaName { get; set; } = "Assistant";

    public bool IsOOCRequest { get; set; }

    /// <summary>
    /// The persona system message
    /// </summary>
    public SystemMessage? Persona { get; set; }

    #region Context Data By Type

    /// <summary>
    /// User's character profile (always loaded if exists)
    /// </summary>
    public ContextData? UserProfile { get; set; }

    /// <summary>
    /// Quote data loaded via all applicable availability mechanisms (AlwaysOn, Manual, Semantic)
    /// </summary>
    public ConcurrentBag<ContextData> Quotes { get; set; } = [];

    /// <summary>
    /// Persona voice sample data loaded via all applicable availability mechanisms (AlwaysOn, Semantic)
    /// </summary>
    public ConcurrentBag<ContextData> PersonaVoiceSamples { get; set; } = [];

    /// <summary>
    /// Memory data loaded via all applicable availability mechanisms (AlwaysOn, Manual, Semantic, Trigger)
    /// </summary>
    public ConcurrentBag<ContextData> Memories { get; set; } = [];

    /// <summary>
    /// Insight data loaded via all applicable availability mechanisms (AlwaysOn, Manual, Semantic, Trigger)
    /// </summary>
    public ConcurrentBag<ContextData> Insights { get; set; } = [];

    /// <summary>
    /// Character profile data loaded via all applicable availability mechanisms (AlwaysOn, Manual, Trigger)
    /// Excludes user profile which is stored separately
    /// </summary>
    public ConcurrentBag<ContextData> CharacterProfiles { get; set; } = [];

    /// <summary>
    /// Generic data loaded via all applicable availability mechanisms (AlwaysOn, Manual, Trigger)
    /// </summary>
    public ConcurrentBag<ContextData> Data { get; set; } = [];

    #endregion Context Data By Type

    /// <summary>
    /// Active or constant flags for the current turn
    /// </summary>
    public ConcurrentBag<Flag> Flags { get; set; } = [];

    /// <summary>
    /// The previous turn's response (most recent accepted turn)
    /// </summary>
    public string? PreviousResponse { get; set; }

    /// <summary>
    /// Previous turn entity (for accessing full details)
    /// </summary>
    public Turn? PreviousTurn { get; set; }

    /// <summary>
    /// Recent turns to be included in history (ordered chronologically)
    /// </summary>
    public List<Turn> RecentTurns { get; set; } = [];

    /// <summary>
    /// Recent conversation context (formatted prose from recent turns)
    /// </summary>
    public string? RecentContext { get; set; }

    /// <summary>
    /// Older dialogue log (compressed/stripped format)
    /// </summary>
    public string? DialogueLog { get; set; }

    /// <summary>
    /// Perception records for the current turn
    /// </summary>
    public ConcurrentBag<PerceptionRecord> Perceptions { get; set; } = [];

    /// <summary>
    /// Cancellation token for the current operation
    /// </summary>
    [JsonIgnore]
    public CancellationToken CancellationToken { get; set; } = default;

    /// <summary>
    /// Number of recent turns to include in context
    /// </summary>
    public int RecentTurnsCount { get; set; } = 5;

    /// <summary>
    /// Maximum number of older turns to include in dialogue log
    /// </summary>
    public int MaxDialogueLogTurns { get; set; } = 50;

    /// <summary>
    /// Gets all context data to be loaded (combines all data types)
    /// Ensures no duplicates
    /// </summary>
    public IEnumerable<ContextData> GetAllContextData()
    {
        var seen = new HashSet<int>();

        // User profile first (always)
        if (UserProfile != null && seen.Add(UserProfile.Id))
            yield return UserProfile;

        // Character profiles
        foreach (var data in CharacterProfiles.Where(d => seen.Add(d.Id)))
            yield return data;

        // Quotes
        foreach (var data in Quotes.Where(d => seen.Add(d.Id)))
            yield return data;

        // Persona voice samples
        foreach (var data in PersonaVoiceSamples.Where(d => seen.Add(d.Id)))
            yield return data;

        // Memories
        foreach (var data in Memories.Where(d => seen.Add(d.Id)))
            yield return data;

        // Insights
        foreach (var data in Insights.Where(d => seen.Add(d.Id)))
            yield return data;

        // Generic data
        foreach (var data in Data.Where(d => seen.Add(d.Id)))
            yield return data;
    }

    /// <summary>
    /// Gets context data by type
    /// </summary>
    public ConcurrentBag<ContextData> GetDataByType(DataType type) => type switch
    {
        DataType.Quote => Quotes,
        DataType.PersonaVoiceSample => PersonaVoiceSamples,
        DataType.Memory => Memories,
        DataType.Insight => Insights,
        DataType.CharacterProfile => CharacterProfiles,
        DataType.Generic => Data,
        _ => []
    };

    /// <summary>
    /// Adds context data to the appropriate type-specific collection (thread-safe)
    /// </summary>
    public void AddContextData(ContextData data)
    {
        var collection = GetDataByType(data.Type);
        if (!collection.Any(d => d.Id == data.Id))
        {
            collection.Add(data);
        }
    }

    /// <summary>
    /// Adds multiple context data entries to their appropriate type-specific collections
    /// </summary>
    public void AddContextDataRange(IEnumerable<ContextData> dataList)
    {
        foreach (var data in dataList)
        {
            AddContextData(data);
        }
    }

    /// <summary>
    /// Gets all context data IDs for usage tracking
    /// </summary>
    public List<int> GetAllContextDataIds()
    {
        return [.. GetAllContextData().Select(d => d.Id)];
    }

    /// <summary>
    /// Creates a formatted string with user input prefix
    /// </summary>
    public string FormatUserInput(string input)
    {
        return $"{UserName[0]}: {input}";
    }

    /// <summary>
    /// Creates a formatted string with persona response prefix
    /// </summary>
    public string FormatPersonaResponse(string response)
    {
        return $"{PersonaName[0]}: {response}";
    }

    /// <summary>
    /// Creates a formatted conversation turn with previous response context
    /// </summary>
    public string FormatConversationTurn(string input)
    {
        return string.IsNullOrEmpty(PreviousResponse)
            ? FormatUserInput(input)
            : $"{FormatPersonaResponse(PreviousResponse)}\n\n{FormatUserInput(input)}";
    }

    /// <summary>
    /// Gets all context messages (recent + dialogue log) formatted for LLM
    /// </summary>
    public string GetFullContextForLLM()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(DialogueLog))
        {
            parts.Add(DialogueLog);
        }

        if (!string.IsNullOrEmpty(RecentContext))
        {
            parts.Add(RecentContext);
        }

        return string.Join("\n\n---\n\n", parts);
    }

    public GeminiRequest GeminiRequest { get; set; } = null!;
    public ClaudeRequest ClaudeRequest { get; set; } = null!;
}