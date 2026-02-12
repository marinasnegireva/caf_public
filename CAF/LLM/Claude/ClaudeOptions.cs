namespace CAF.LLM.Claude;

public class ClaudeOptions
{
    public const string SectionName = "Claude";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";
    public int MaxTokens { get; set; } = 8192;
    public double Temperature { get; set; } = 1.0;

    /// <summary>
    /// Enable extended thinking for Claude models
    /// </summary>
    public bool EnableThinking { get; set; } = true;

    /// <summary>
    /// Token budget for extended thinking (minimum 1024, must be less than MaxTokens)
    /// </summary>
    public int ThinkingBudgetTokens { get; set; } = 4096;

    /// <summary>
    /// Enable prompt caching to reduce costs and latency for repeated content
    /// Caching is applied to system instructions and user messages
    /// </summary>
    public bool EnablePromptCaching { get; set; } = true;

    /// <summary>
    /// Minimum content length (in characters) to apply caching
    /// Content shorter than this won't be cached (not worth the overhead)
    /// </summary>
    public int MinCachingContentLength { get; set; } = 1024;
}