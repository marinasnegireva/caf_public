namespace CAF.LLM.Gemini;

/// <summary>
/// Builder class for constructing Gemini conversation requests with history
/// </summary>
public class GeminiMessageBuilder
{
    private readonly List<Content> _messages = [];
    private SystemInstruction _systemInstruction = new();
    private GenerationConfig _generationConfig = new();
    private List<SafetySetting> _safetySettings = [];

    /// <summary>
    /// Sets the system instruction for the conversation
    /// </summary>
    public GeminiMessageBuilder WithSystemInstruction(string instruction)
    {
        _systemInstruction = new SystemInstruction
        {
            Parts = [new() { Text = instruction }]
        };
        return this;
    }

    /// <summary>
    /// Adds a user message to the conversation
    /// </summary>
    public GeminiMessageBuilder AddUserMessage(string text)
    {
        _messages.Add(new()
        {
            Role = "user",
            Parts = [new() { Text = text }]
        });
        return this;
    }

    /// <summary>
    /// Adds a model (assistant) response to the conversation
    /// </summary>
    public GeminiMessageBuilder AddModelResponse(string text)
    {
        _messages.Add(new()
        {
            Role = "model",
            Parts = [new() { Text = text }]
        });
        return this;
    }

    /// <summary>
    /// Adds a conversation turn (user + model response)
    /// </summary>
    public GeminiMessageBuilder AddTurn(string userMessage, string modelResponse)
    {
        AddUserMessage(userMessage);
        AddModelResponse(modelResponse);
        return this;
    }

    /// <summary>
    /// Adds multiple conversation turns from history
    /// </summary>
    public GeminiMessageBuilder AddHistory(IEnumerable<(string user, string model)> history)
    {
        foreach (var (user, model) in history)
        {
            AddTurn(user, model);
        }
        return this;
    }

    /// <summary>
    /// Sets the generation configuration with individual parameters
    /// </summary>
    public GeminiMessageBuilder WithGenerationConfig(
        int? maxOutputTokens = null,
        float? temperature = null,
        string? responseMimeType = null)
    {
        _generationConfig = new GenerationConfig
        {
            MaxOutputTokens = maxOutputTokens ?? 8192,
            Temperature = temperature ?? 1.0f,
            ResponseMimeType = responseMimeType
        };
        return this;
    }

    /// <summary>
    /// Sets the safety settings
    /// </summary>
    public GeminiMessageBuilder WithSafetySettings(List<SafetySetting> safetySettings)
    {
        _safetySettings = safetySettings;
        return this;
    }

    /// <summary>
    /// Builds the GeminiRequest
    /// </summary>
    public GeminiRequest Build()
    {
        if (_messages.Count == 0)
        {
            throw new InvalidOperationException("At least one message must be added before building the request.");
        }

        // Ensure conversation ends with a user message for Gemini API
        return _messages.Last().Role != "user"
            ? throw new InvalidOperationException("Conversation must end with a user message.")
            : new GeminiRequest
            {
                Contents = [.. _messages],
                SystemInstruction = _systemInstruction,
                GenerationConfig = _generationConfig,
                SafetySettings = _safetySettings
            };
    }

    /// <summary>
    /// Clears all messages and configuration
    /// </summary>
    public GeminiMessageBuilder Clear()
    {
        _messages.Clear();
        _systemInstruction = null;
        _generationConfig = null;
        _safetySettings = [];
        return this;
    }

    /// <summary>
    /// Gets the current message count
    /// </summary>
    public int MessageCount => _messages.Count;

    /// <summary>
    /// Creates a new builder instance
    /// </summary>
    public static GeminiMessageBuilder Create() => new();
}