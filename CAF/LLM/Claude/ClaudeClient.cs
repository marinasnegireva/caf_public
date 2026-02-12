using CAF.LLM.Logging;

namespace CAF.LLM.Claude;

public class ClaudeClient : IClaudeClient
{
    private readonly string _defaultModel;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IDbContextFactory<GeneralDbContext>? _dbFactory;
    private readonly ILogger<LLMLogger> _llmLogger;
    private readonly ClaudeOptions _options;
    private readonly ISettingService _settingService;

    public ClaudeClient(
        IOptions<ClaudeOptions> options,
        HttpClient httpClient,
        IDbContextFactory<GeneralDbContext>? dbFactory,
        ILogger<LLMLogger> llmLogger,
        ISettingService settingService)
    {
        _options = options.Value;
        _defaultModel = _options.Model;
        _apiKey = _options.ApiKey ?? throw new ArgumentException("ApiKey is required", nameof(_options.ApiKey));
        _httpClient = httpClient;

        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri("https://api.anthropic.com/");

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        // Add prompt caching beta header if caching is enabled
        if (_options.EnablePromptCaching)
        {
            _httpClient.DefaultRequestHeaders.Add("anthropic-beta", "prompt-caching-2024-07-31");
        }

        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        _dbFactory = dbFactory;
        _llmLogger = llmLogger;
        _settingService = settingService;
    }

    private async Task<string> GetActiveModelAsync(CancellationToken cancellationToken = default)
    {
        return await _settingService.GetByNameAsync(SettingsKeys.ClaudeModel, cancellationToken)
            .ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    var setting = t.Result;
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
                    {
                        return setting.Value;
                    }
                }
                return _defaultModel;
            }, cancellationToken);
    }

    public async Task<(bool success, string result)> GenerateContentAsync(
        ClaudeRequest request,
        CancellationToken cancellationToken = default,
        int? turnId = null)
    {
        var activeModel = await GetActiveModelAsync(cancellationToken);
        request.Model = activeModel;

        LLMLogger? logger = null;
        if (_dbFactory is not null)
        {
            logger = new LLMLogger(_dbFactory, _llmLogger, "GenerateContent", activeModel, request, "Claude", turnId);

            // Best-effort capture of main prompt
            try
            {
                var lastUserMessage = request.Messages
                    .LastOrDefault(m => m.Role == "user");

                if (lastUserMessage?.Content is string textContent)
                {
                    logger.SetPrompt(textContent);
                }

                // Handle both string and structured system content
                if (request.System is string systemText)
                {
                    logger.SetSystemInstruction(systemText);
                }
                else if (request.System is List<ClaudeSystemBlock> systemBlocks)
                {
                    logger.SetSystemInstruction(string.Join("\n", systemBlocks.Select(b => b.Text)));
                }
            }
            catch { }
        }

        try
        {
            var json = JsonSerializer.Serialize(request, Extensions.ClaudeOptions);

            _llmLogger.LogInformation("Sending Claude API request to model: {Model}", activeModel);

            var resp = await GenerateContentViaRestAsync(json, cancellationToken);
            var content = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (logger is not null)
                await logger.FinalizeAndSaveAsync(resp, content, cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Claude API request failed with status {StatusCode}: {Content}",
                    resp.StatusCode, content);

                var errorResponse = JsonSerializer.Deserialize<ClaudeErrorResponse>(content, Extensions.ClaudeOptions);

                var errorMessage = errorResponse?.Error?.Message ?? $"HTTP {resp.StatusCode}: {content}";
                _llmLogger.LogError("Claude API error: {ErrorMessage}", errorMessage);

                return (false, errorMessage);
            }

            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(content, Extensions.ClaudeOptions);

            if (claudeResponse?.Content == null || claudeResponse.Content.Count == 0)
            {
                _llmLogger.LogWarning("Claude API returned empty content: {Content}", content);
                return (false, content);
            }

            // Extract text content blocks (excluding thinking blocks)
            var textBlocks = claudeResponse.Content
                .Where(c => c.Type == "text" && !string.IsNullOrWhiteSpace(c.Text))
                .Select(c => c.Text)
                .ToList();

            // Extract thinking blocks for logging
            var thinkingBlocks = claudeResponse.Content
                .Where(c => c.Type == "thinking" && !string.IsNullOrWhiteSpace(c.Thinking))
                .Select(c => c.Thinking)
                .ToList();

            if (thinkingBlocks.Count > 0)
            {
                _llmLogger.LogInformation("Claude thinking process included {ThinkingBlocks} thought blocks",
                    thinkingBlocks.Count);
            }

            var replyText = string.Join("\n", textBlocks);

            if (string.IsNullOrWhiteSpace(replyText))
            {
                _llmLogger.LogWarning("Claude API returned no text content (only thinking blocks)");
                return (false, "No text response generated");
            }

            var usage = claudeResponse.Usage;

            if (logger is not null && usage is not null)
            {
                // Calculate correct total tokens including all cache operations
                var totalTokens = usage.InputTokens + 
                                 (usage.CacheCreationInputTokens ?? 0) + 
                                 (usage.CacheReadInputTokens ?? 0) + 
                                 usage.OutputTokens;

                await logger.FinalizeResponseAsync(
                    resp,
                    content,
                    replyText,
                    totalTokens,
                    usage.InputTokens,
                    usage.OutputTokens,
                    usage.CacheCreationInputTokens ?? 0,
                    usage.CacheReadInputTokens ?? 0,
                    0, // Claude doesn't have separate reasoning token count in this context
                    ct: cancellationToken
                );
            }

            // Log cache performance if caching was used
            if (usage?.CacheCreationInputTokens > 0 || usage?.CacheReadInputTokens > 0)
            {
                _llmLogger.LogInformation(
                    "Claude prompt caching: {CacheCreated} tokens cached, {CacheRead} tokens read from cache (saved {Saved} tokens)",
                    usage.CacheCreationInputTokens ?? 0,
                    usage.CacheReadInputTokens ?? 0,
                    usage.CacheReadInputTokens ?? 0);
            }

            _llmLogger.LogInformation("Claude API request successful. Tokens: {InputTokens} in, {OutputTokens} out",
                usage?.InputTokens ?? 0, usage?.OutputTokens ?? 0);

            return (true, replyText);
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Claude API: {Message}", ex.Message);
            return (false, ex.ToString());
        }
    }

    private async Task<HttpResponseMessage> GenerateContentViaRestAsync(
        string json,
        CancellationToken cancellationToken)
    {
        var endpoint = "v1/messages";

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(req, cancellationToken);
        return response;
    }

    /// <summary>
    /// Count tokens in the given text using Claude's API
    /// Note: Claude doesn't have a dedicated token counting endpoint,
    /// so we use the Messages API with max_tokens=1 and read usage from response
    /// </summary>
    public async Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        try
        {
            var activeModel = await GetActiveModelAsync(cancellationToken);

            var request = new ClaudeCountTokensRequest
            {
                Model = activeModel,
                MaxTokens = 1,
                Messages =
                [
                    new ClaudeMessage
                    {
                        Role = "user",
                        Content = text
                    }
                ]
            };

            var json = JsonSerializer.Serialize(request, Extensions.ClaudeOptions);

            var response = await GenerateContentViaRestAsync(json, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _llmLogger.LogWarning("Claude CountTokens failed with status {StatusCode}: {Content}",
                    response.StatusCode, errorContent);
                return -1;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(content, Extensions.ClaudeOptions);

            return claudeResponse?.Usage?.InputTokens ?? -1;
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Claude CountTokens: {Message}", ex.Message);
            return -1;
        }
    }
}