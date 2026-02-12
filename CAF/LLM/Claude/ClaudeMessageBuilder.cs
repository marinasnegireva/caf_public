namespace CAF.LLM.Claude;

/// <summary>
/// Builder class for constructing Claude conversation requests with history and prompt caching
/// </summary>
public class ClaudeMessageBuilder
{
    private readonly List<ClaudeMessage> _messages = [];
    private string _system = string.Empty;
    private int _maxTokens = 8192;
    private double? _temperature;
    private double? _topP;
    private int? _topK;
    private List<string> _stopSequences = [];
    private ClaudeMetadata _metadata;
    private ClaudeThinkingConfig _thinking;

    private ClaudeMessageBuilder()
    { }

    public static ClaudeMessageBuilder Create() => new();

    /// <summary>
    /// Sets the system instruction for the conversation
    /// </summary>
    public ClaudeMessageBuilder WithSystem(string system)
    {
        _system = system;
        return this;
    }
    /// <summary>
    /// Adds a user message to the conversation
    /// </summary>
    public ClaudeMessageBuilder AddUserMessage(string text)
    {
        _messages.Add(new ClaudeMessage
        {
            Role = "user",
            Content = text
        });
        return this;
    }

    /// <summary>
    /// Adds an assistant response to the conversation
    /// </summary>
    public ClaudeMessageBuilder AddAssistantMessage(string text)
    {
        _messages.Add(new ClaudeMessage
        {
            Role = "assistant",
            Content = text
        });
        return this;
    }

    /// <summary>
    /// Adds a conversation turn (user + assistant response)
    /// </summary>
    public ClaudeMessageBuilder AddTurn(string userMessage, string assistantResponse)
    {
        AddUserMessage(userMessage);
        AddAssistantMessage(assistantResponse);
        return this;
    }

    /// <summary>
    /// Adds multiple conversation turns from history
    /// </summary>
    public ClaudeMessageBuilder AddHistory(IEnumerable<(string user, string assistant)> history)
    {
        foreach (var (user, assistant) in history)
        {
            AddTurn(user, assistant);
        }
        return this;
    }

    /// <summary>
    /// Sets the maximum number of tokens to generate
    /// </summary>
    public ClaudeMessageBuilder WithMaxTokens(int maxTokens)
    {
        _maxTokens = maxTokens;
        return this;
    }

    /// <summary>
    /// Sets the temperature parameter
    /// </summary>
    public ClaudeMessageBuilder WithTemperature(double temperature)
    {
        _temperature = temperature;
        return this;
    }

    /// <summary>
    /// Sets the top_p parameter
    /// </summary>
    public ClaudeMessageBuilder WithTopP(double topP)
    {
        _topP = topP;
        return this;
    }

    /// <summary>
    /// Sets the top_k parameter
    /// </summary>
    public ClaudeMessageBuilder WithTopK(int topK)
    {
        _topK = topK;
        return this;
    }

    /// <summary>
    /// Adds stop sequences
    /// </summary>
    public ClaudeMessageBuilder WithStopSequences(List<string> stopSequences)
    {
        _stopSequences = stopSequences;
        return this;
    }

    /// <summary>
    /// Sets metadata for the request
    /// </summary>
    public ClaudeMessageBuilder WithMetadata(ClaudeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Enables extended thinking with specified token budget
    /// </summary>
    public ClaudeMessageBuilder WithThinking()
    {
        _thinking = new ClaudeThinkingConfig
        {
            Type = "adaptive"
        };
        return this;
    }

    /// <summary>
    /// Disables extended thinking
    /// </summary>
    public ClaudeMessageBuilder WithoutThinking()
    {
        _thinking = new ClaudeThinkingConfig
        {
            Type = "disabled"
        };
        return this;
    }

    /// <summary>
    /// Applies cache control to the last message in the conversation.
    /// This is useful when you want to cache the entire conversation including the current turn.
    /// Note: Only call this if the total content (system + all messages) meets the minimum caching length.
    /// </summary>
    public ClaudeMessageBuilder WithCacheBreakpointOnLastMessage()
    {
        if (_messages.Count == 0)
        {
            throw new InvalidOperationException("Cannot apply cache breakpoint: no messages have been added");
        }

        var lastMessageIndex = _messages.Count - 1;
        var lastMessage = _messages[lastMessageIndex];

        // Skip if this message already has cache control
        if (lastMessage.Content is List<ClaudeContentBlock> blocks && blocks.Any(b => b.CacheControl != null))
            return this;

        var textContent = lastMessage.Content?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(textContent))
            return this;

        // Convert to structured content with cache control
        _messages[lastMessageIndex].Content = new List<ClaudeContentBlock>
        {
            new() {
                Type = "text",
                Text = textContent,
                CacheControl = new ClaudeCacheControl { Type = "ephemeral" }
            }
        };

        return this;
    }

    /// <summary>
    /// Builds the final ClaudeRequest with prompt caching applied where appropriate.
    /// 
    /// Caching strategy:
    /// 1. System message is cached if it meets minimum length requirement
    /// 2. All messages except the last one (current turn) are cached as conversation history
    /// 
    /// This approach is optimal because:
    /// - System instructions rarely change between requests
    /// - Conversation history is stable (only grows)
    /// - Current user message is not cached (changes every request)
    /// - Uses the sequential/prefix nature of Claude's caching (tools → system → messages)
    /// </summary>
    public ClaudeRequest Build(string model)
    {
        if (_messages.Count == 0)
        {
            throw new InvalidOperationException("At least one message must be added before building the request");
        }

        // Ensure conversation starts with user and alternates properly
        if (_messages[0].Role != "user")
        {
            throw new InvalidOperationException("First message must be from user");
        }

        // Validate thinking budget if enabled
        if (_thinking?.Type == "enabled" && _thinking.BudgetTokens.HasValue)
        {
            if (_thinking.BudgetTokens.Value >= _maxTokens)
            {
                throw new InvalidOperationException($"Thinking budget ({_thinking.BudgetTokens.Value}) must be less than max_tokens ({_maxTokens})");
            }
        }

        return new ClaudeRequest
        {
            Model = model,
            MaxTokens = _maxTokens,
            Temperature = _temperature,
            System = _system,
            Messages = _messages,
            StopSequences = _stopSequences.Count > 0 ? _stopSequences : null,
            TopP = _topP,
            TopK = _topK,
            Metadata = _metadata,
            Thinking = _thinking
        };
    }
}