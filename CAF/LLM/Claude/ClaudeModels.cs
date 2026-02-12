namespace CAF.LLM.Claude;

/// <summary>
/// Request to Claude Messages API
/// https://docs.anthropic.com/en/api/messages
/// </summary>
public class ClaudeRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("system")]
    public object System { get; set; } // Changed to object to support both string and structured system blocks

    [JsonPropertyName("messages")]
    public List<ClaudeMessage> Messages { get; set; }

    [JsonPropertyName("stop_sequences")]
    public List<string> StopSequences { get; set; }

    [JsonPropertyName("top_p")]
    public double? TopP { get; set; }

    [JsonPropertyName("top_k")]
    public int? TopK { get; set; }

    [JsonPropertyName("metadata")]
    public ClaudeMetadata Metadata { get; set; }

    [JsonPropertyName("thinking")]
    public ClaudeThinkingConfig Thinking { get; set; }

    [JsonPropertyName("output_config")]
    public ClaudeOutputConfig OutputConfig { get; set; } = new();
}

/// <summary>
/// Configuration for Claude's extended thinking feature.
/// Supports both adaptive thinking (recommended for Opus 4.6+) and manual mode.
/// https://docs.anthropic.com/en/build-with-claude/adaptive-thinking
/// https://docs.anthropic.com/en/build-with-claude/extended-thinking
/// </summary>
public class ClaudeThinkingConfig
{
    /// <summary>
    /// Type of thinking mode:
    /// - "adaptive": Claude dynamically decides when and how much to think (Opus 4.6+ only, recommended)
    /// - "enabled": Manual mode with fixed budget_tokens (all models, deprecated on Opus 4.6+)
    /// - Omit thinking parameter entirely to disable extended thinking
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    /// <summary>
    /// Fixed token budget for thinking (manual mode only).
    /// Only used when Type = "enabled". Deprecated on Opus 4.6+ - use adaptive mode with effort parameter instead.
    /// </summary>
    [JsonPropertyName("budget_tokens")]
    public int? BudgetTokens { get; set; }
}

/// <summary>
/// Output configuration including effort level for adaptive thinking.
/// https://docs.anthropic.com/en/build-with-claude/effort
/// </summary>
public class ClaudeOutputConfig
{
    /// <summary>
    /// Effort level for adaptive thinking:
    /// - "max": Always thinks with no constraints (Opus 4.6 only)
    /// - "high": Always thinks, provides deep reasoning (default)
    /// - "medium": Uses moderate thinking, may skip for very simple queries
    /// - "low": Minimizes thinking, skips for simple tasks where speed matters
    /// </summary>
    [JsonPropertyName("effort")]
    public string Effort { get; set; } = "max";
}

public class ClaudeMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public object Content { get; set; }
}

/// <summary>
/// Content block with optional cache control for prompt caching.
/// Supports text, thinking, and redacted_thinking types.
/// https://docs.anthropic.com/en/build-with-claude/adaptive-thinking#thinking-redaction
/// </summary>
public class ClaudeContentBlock
{
    /// <summary>
    /// Type of content block: "text", "thinking", or "redacted_thinking"
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; }

    /// <summary>
    /// Contains the thinking process (summarized in Claude 4+ models).
    /// Note: You're charged for the full thinking tokens, not the summary tokens visible in the response.
    /// </summary>
    [JsonPropertyName("thinking")]
    public string Thinking { get; set; }

    /// <summary>
    /// Encrypted signature for thinking verification.
    /// Must be passed back unmodified when including thinking blocks in subsequent turns.
    /// </summary>
    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    /// <summary>
    /// Encrypted thinking content flagged by safety systems.
    /// Must be passed back unmodified to preserve Claude's reasoning flow.
    /// </summary>
    [JsonPropertyName("data")]
    public string Data { get; set; }

    [JsonPropertyName("cache_control")]
    public ClaudeCacheControl CacheControl { get; set; }
}

/// <summary>
/// Cache control configuration for prompt caching
/// https://docs.anthropic.com/en/docs/build-with-claude/prompt-caching
/// </summary>
public class ClaudeCacheControl
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ephemeral";
}

/// <summary>
/// System content block with cache control support
/// </summary>
public class ClaudeSystemBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("cache_control")]
    public ClaudeCacheControl CacheControl { get; set; }
}

public class ClaudeTextContent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("cache_control")]
    public ClaudeCacheControl CacheControl { get; set; }
}

public class ClaudeMetadata
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; }
}

/// <summary>
/// Response from Claude Messages API
/// </summary>
public class ClaudeResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("content")]
    public List<ClaudeContentBlock> Content { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; }

    [JsonPropertyName("stop_sequence")]
    public string StopSequence { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage Usage { get; set; }
}

public class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("cache_creation_input_tokens")]
    public int? CacheCreationInputTokens { get; set; }

    [JsonPropertyName("cache_read_input_tokens")]
    public int? CacheReadInputTokens { get; set; }
}

/// <summary>
/// Error response from Claude API
/// </summary>
public class ClaudeErrorResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("error")]
    public ClaudeError Error { get; set; }
}

public class ClaudeError
{
    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}

/// <summary>
/// Request for counting tokens (using Messages API with max_tokens=1)
/// </summary>
public class ClaudeCountTokensRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1;

    [JsonPropertyName("messages")]
    public List<ClaudeMessage> Messages { get; set; }

    [JsonPropertyName("system")]
    public object System { get; set; }
}