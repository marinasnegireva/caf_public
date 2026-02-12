namespace CAF;

/// <summary>
/// Constants for conversation processing configuration
/// </summary>
public static class ConversationConstants
{
    /// <summary>
    /// Timeout duration for batch operations
    /// </summary>
    public static readonly TimeSpan BatchOperationTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Interval between batch operation status polls
    /// </summary>
    public static readonly TimeSpan BatchPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Default LLM provider when not specified
    /// </summary>
    public const string DefaultProvider = "Gemini";

    /// <summary>
    /// Gemini provider name
    /// </summary>
    public const string GeminiProvider = "Gemini";

    /// <summary>
    /// Claude provider name
    /// </summary>
    public const string ClaudeProvider = "Claude";

    /// <summary>
    /// Separator between displayed response and log-only content.
    /// Must be a unique token that will never appear in narrative prose.
    /// </summary>
    public const string ResponseSeparator = "[ANALYSIS]";

    /// <summary>
    /// Metadata tag prefix
    /// </summary>
    public const string MetaTagPrefix = "[meta]";

    /// <summary>
    /// Out-of-character request prefix
    /// </summary>
    public const string OocPrefix = "[ooc]";

    /// <summary>
    /// Header for system flags section
    /// </summary>
    public const string FlagsHeader = "Flags:";

    /// <summary>
    /// Common acknowledgment messages
    /// </summary>
    public static class Acknowledgments
    {
        public const string Default = "Received.";
        public const string UserProfile = "Acknowledging user profile.";
        public const string History = "History noted.";
        public static string Grouped(int count, string header) => $"Received {count} relevant {header} entries.";
    }

    /// <summary>
    /// Context data header names
    /// </summary>
    public static class Headers
    {
        public const string Memories = "memories";
        public const string Insights = "insights";
        public const string VoiceSample = "voice sample";
        public const string Quotes = "quotes";
        public const string UserProfile = "user profile";
    }

    /// <summary>
    /// Names of technical system messages
    /// </summary>
    public static class TechnicalMessages
    {
        public const string TurnStripperInstructions = "turn stripper instructions";
        public const string MemorySummaryInstructions = "memory summary instructions";
        public const string MemoryCoreFactsInstructions = "memory core facts instructions";
        public const string QuoteQueryTransformer = "quote query transformer";
        public const string QuoteMapper = "quote mapper";
    }

    /// <summary>
    /// Telegram bot commands
    /// </summary>
    public static class TelegramCommands
    {
        public const string Help = "/help";
        public const string Status = "/status";
        public const string New = "/new";
        public const string Restart = "/restart";
        public const string Sessions = "/sessions";
        public const string Activate = "/activate";
        public const string Cancel = "/cancel";
    }
}