using CAF.LLM.Logging;

namespace CAF.LLM.Gemini;

public class GeminiClient : IGeminiClient
{
    private readonly string _defaultModel;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IDbContextFactory<GeneralDbContext>? _dbFactory;
    private readonly ILogger<LLMLogger> _llmLogger;
    private readonly IOptions<GeminiOptions> _options;

    public GeminiClient(IOptions<GeminiOptions> options, HttpClient httpClient, IDbContextFactory<GeneralDbContext>? dbFactory, ILogger<LLMLogger> llmLogger)
    {
        var geminiOptions = options.Value;
        _defaultModel = geminiOptions.Model;
        _apiKey = geminiOptions.ApiKey ?? throw new ArgumentException(_apiKey, nameof(geminiOptions.ApiKey));

        _httpClient = httpClient;
        if (_httpClient.BaseAddress == null)
            _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("X-Server-Timeout", "300");
        _httpClient.DefaultRequestHeaders.Add("X-Goog-Api-Deadline", "300");
        _httpClient.Timeout = TimeSpan.FromMinutes(5);

        _dbFactory = dbFactory;
        _llmLogger = llmLogger;
        _options = options;
    }

    private async Task<string> GetActiveModelAsync(CancellationToken cancellationToken = default)
    {
        // Check for runtime model override in settings
        if (_dbFactory is not null)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
                var modelSetting = await db.Settings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Name == SettingsKeys.GeminiModel.ToKey(), cancellationToken);

                if (modelSetting != null && !string.IsNullOrWhiteSpace(modelSetting.Value))
                {
                    return modelSetting.Value;
                }
            }
            catch (Exception ex)
            {
                _llmLogger.LogWarning(ex, "Failed to read GeminiModel setting, using default model");
            }
        }

        return _defaultModel;
    }

    public async Task<(bool success, string result)> GenerateContentAsync(
        GeminiRequest request,
        bool technical = true,
        int? turnId = null,
        CancellationToken cancellationToken = default)
    {
        var activeModel = technical
            ? _options.Value.TechnicalModel
            : await GetActiveModelAsync(cancellationToken);

        LLMLogger? logger = null;
        if (_dbFactory is not null)
        {
            logger = new LLMLogger(_dbFactory, _llmLogger, "GenerateContent", activeModel, request, "Gemini", turnId);

            // best-effort capture of main prompt
            try
            {
                var userText = request.Contents
                    .Where(c => string.Equals(c.Role, "user", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(c => c.Parts)
                    .Select(p => p.Text)
                    .LastOrDefault(t => !string.IsNullOrWhiteSpace(t));
                logger.SetPrompt(userText);
                var instruction = request.SystemInstruction.Parts
                        .Select(p => p.Text)
                        .LastOrDefault(t => !string.IsNullOrWhiteSpace(t));
                logger.SetSystemInstruction(instruction);
            }
            catch { }
        }

        try
        {
            var json = JsonSerializer.Serialize(request, Extensions.GeminiOptions);

            _llmLogger.LogInformation("Sending Gemini API request to model: {Model}", activeModel);

            var resp = await GenerateContentViaRestAsync(json, activeModel, cancellationToken);
            var content = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (logger is not null)
                await logger.FinalizeAndSaveAsync(resp, content, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Gemini API request failed with status {StatusCode}: {Content}",
                    resp.StatusCode, content);
                return (false, content);
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse?>(content, Extensions.GeminiOptions);

            if (geminiResponse is null || geminiResponse?.Candidates is null || geminiResponse?.Candidates.Count == 0 || geminiResponse?.Candidates.First().Content is null || geminiResponse?.Candidates.First().Content.Parts is null)
            {
                _llmLogger.LogWarning("Gemini API returned empty or invalid content: {Content}", content);
                return (false, content);
            }

            var reply = geminiResponse?.Candidates[0]?.Content?.Parts?.FirstOrDefault(p => !(p.Thought == true));
            var replyString = reply?.Text;

            var tokenUsage = geminiResponse?.UsageMetadata;

            if (logger is not null && tokenUsage is not null)
            {
                await logger.FinalizeResponseAsync(
                            resp,
                            content,
                            replyString,
                            tokenUsage.TotalTokenCount,
                            tokenUsage.PromptTokenCount,
                            tokenUsage.CandidatesTokenCount,
                            0, // Gemini doesn't charge extra for cache creation
                            tokenUsage.CachedContentTokenCount ?? 0,
                            tokenUsage.ThoughtsTokenCount ?? 0,
                            ct: cancellationToken
                            );
            }

            return (true, replyString);
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Gemini API: {Message}", ex.Message);
            if (logger is not null)
                await logger.FinalizeWithErrorAsync(ex.ToString(), cancellationToken);
            return (false, ex.ToString());
        }
    }

    public async Task<(bool success, string result)> StreamGenerateContentAsync(
       GeminiRequest request,
       bool technical = true,
       int? turnId = null,
       CancellationToken cancellationToken = default)
    {
        var activeModel = technical
            ? _options.Value.TechnicalModel
            : await GetActiveModelAsync(cancellationToken);

        LLMLogger? logger = null;
        if (_dbFactory is not null)
        {
            logger = new LLMLogger(_dbFactory, _llmLogger, "StreamGenerateContent", activeModel, request, "Gemini", turnId);

            // best-effort capture of main prompt
            try
            {
                var userText = request.Contents
                    .Where(c => string.Equals(c.Role, "user", StringComparison.OrdinalIgnoreCase))
                    .SelectMany(c => c.Parts)
                    .Select(p => p.Text)
                    .LastOrDefault(t => !string.IsNullOrWhiteSpace(t));
                logger.SetPrompt(userText);
                var instruction = request.Contents
                        .Where(c => !string.Equals(c.Role, "user", StringComparison.OrdinalIgnoreCase))
                        .SelectMany(c => c.Parts)
                        .Select(p => p.Text)
                        .LastOrDefault(t => !string.IsNullOrWhiteSpace(t));
                logger.SetSystemInstruction(instruction);
            }
            catch { }
        }

        try
        {
            var json = JsonSerializer.Serialize(request, Extensions.GeminiOptions);

            _llmLogger.LogInformation("Sending Gemini streaming API request to model: {Model}", activeModel);

            var resp = await StreamGenerateContentViaRestAsync(json, activeModel, cancellationToken);
            var content = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (logger is not null)
                await logger.FinalizeAndSaveAsync(resp, content, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Gemini streaming API request failed with status {StatusCode}: {Content}",
                    resp.StatusCode, content);
                return (false, content);
            }

            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse[]?>(content, Extensions.GeminiOptions);

            if (geminiResponse is null || geminiResponse.Length == 0 || geminiResponse[0].Candidates is null || geminiResponse[0].Candidates.Count == 0 || geminiResponse[0].Candidates.First().Content is null || geminiResponse[0].Candidates.First().Content.Parts is null)
            {
                _llmLogger.LogWarning("Gemini streaming API returned empty or invalid content: {Content}", content);
                return (false, content);
            }

            var reply = geminiResponse[0].Candidates[0].Content.Parts.FirstOrDefault(p => !(p.Thought == true));
            var replyString = reply?.Text;

            var tokenUsage = geminiResponse[0].UsageMetadata;

            if (logger is not null && tokenUsage is not null)
            {
                await logger.FinalizeResponseAsync(
                            resp,
                            content,
                            replyString,
                            tokenUsage.TotalTokenCount,
                            tokenUsage.PromptTokenCount,
                            tokenUsage.CandidatesTokenCount,
                            0, // Gemini doesn't charge extra for cache creation
                            tokenUsage.CachedContentTokenCount ?? 0,
                            tokenUsage.ThoughtsTokenCount ?? 0,
                            ct: cancellationToken
                            );
            }

            _llmLogger.LogInformation("Gemini streaming API request successful. Tokens: {InputTokens} in, {OutputTokens} out",
                tokenUsage?.PromptTokenCount ?? 0, tokenUsage?.CandidatesTokenCount ?? 0);

            return (true, replyString);
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Gemini streaming API: {Message}", ex.Message);
            if (logger is not null)
                await logger.FinalizeWithErrorAsync(ex.ToString(), cancellationToken);
            return (false, ex.ToString());
        }
    }

    private async Task<HttpResponseMessage> GenerateContentViaRestAsync(string json, string model, CancellationToken cancellationToken)
    {
        var endpoint = $"models/{model}:generateContent";

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json")
        };
        var response = await _httpClient.SendAsync(req, cancellationToken);
        return response;
    }

    private async Task<HttpResponseMessage> StreamGenerateContentViaRestAsync(string json, string model, CancellationToken cancellationToken)
    {
        var endpoint = $"models/{model}:streamGenerateContent";

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json ?? string.Empty, Encoding.UTF8, "application/json")
        };
        var response = await _httpClient.SendAsync(req, cancellationToken);
        return response;
    }

    /// <summary>
    /// Count tokens in the given text using Gemini's native token counting API
    /// </summary>
    /// <param name="text">Text to count tokens for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens, or -1 if the API call fails</returns>
    public async Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        try
        {
            var activeModel = await GetActiveModelAsync(cancellationToken);

            _llmLogger.LogInformation("Sending Gemini token count request using model: {Model}", activeModel);

            // Build request with just the text content
            var request = new CountTokensRequest
            {
                Contents =
                [
                    new Content
                    {
                        Role = "user",
                        Parts =
                        [
                            new Part { Text = text }
                        ]
                    }
                ]
            };

            var json = JsonSerializer.Serialize(request, Extensions.GeminiOptions);

            var endpoint = $"models/{activeModel}:countTokens";

            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(req, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _llmLogger.LogWarning("Gemini token count request failed with status {StatusCode}: {Content}", 
                    response.StatusCode, content);
                return -1;
            }

            var tokenResponse = JsonSerializer.Deserialize<CountTokensResponse>(content, Extensions.GeminiOptions);

            var tokenCount = tokenResponse?.TotalTokens ?? -1;
            _llmLogger.LogInformation("Gemini token count request successful. Tokens: {TokenCount}", tokenCount);

            return tokenCount;
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Gemini token count API: {Message}", ex.Message);
            return -1;
        }
    }

    /// <summary>
    /// Generate embedding for a single text string
    /// </summary>
    public async Task<float[]> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        var embeddingModel = $"models/{_options.Value.EmbeddingModel}";

        try
        {
            _llmLogger.LogInformation("Sending Gemini embedding request for single text using model: {Model}", embeddingModel);

            var payload = new
            {
                content = new
                {
                    parts = new[] { new { text } }
                },
                taskType = "SEMANTIC_SIMILARITY"
            };

            var requestJson = JsonSerializer.Serialize(payload, Extensions.GeminiOptions);

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Use embedContent endpoint for single embedding
            var url = $"{embeddingModel}:embedContent";

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Gemini embedding request failed with status {StatusCode}: {Body}", 
                    response.StatusCode, responseBody);
                throw new HttpRequestException($"Gemini API returned {response.StatusCode}: {responseBody}");
            }

            var embedResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

            if (!embedResponse.TryGetProperty("embedding", out var embeddingElement) ||
                !embeddingElement.TryGetProperty("values", out var valuesElement))
            {
                _llmLogger.LogError("Gemini embedding response missing required fields. Response: {Response}", responseBody);
                throw new InvalidOperationException($"No embedding returned. Response: {responseBody}");
            }

            var values = valuesElement.EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();

            _llmLogger.LogInformation("Gemini embedding request successful. Dimensions: {Dimensions}", values.Length);

            return values;
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Gemini embedding API: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Generate embeddings for multiple texts using batch API
    /// </summary>
    public async Task<List<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Count == 0)
            throw new ArgumentException("At least one text is required", nameof(texts));

        var embeddingModel = $"models/{_options.Value.EmbeddingModel}";

        try
        {
            _llmLogger.LogInformation("Sending Gemini batch embedding request for {Count} texts using model: {Model}", 
                texts.Count, embeddingModel);

            // Build batch request with all texts
            var requests = texts.Select(text => new
            {
                model = embeddingModel,
                content = new
                {
                    parts = new[] { new { text } }
                },
                taskType = "SEMANTIC_SIMILARITY"
            }).ToArray();

            var payload = new { requests };

            var requestJson = JsonSerializer.Serialize(payload, Extensions.GeminiOptions);

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Use batchEmbedContents endpoint
            var url = $"{embeddingModel}:batchEmbedContents";

            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Gemini batch embedding request failed with status {StatusCode}: {Body}", 
                    response.StatusCode, responseBody);
                throw new HttpRequestException($"Gemini batch API returned {response.StatusCode}: {responseBody}");
            }

            var batchResponse = JsonSerializer.Deserialize<JsonElement>(responseBody);

            if (!batchResponse.TryGetProperty("embeddings", out var embeddingsElement))
            {
                _llmLogger.LogError("Gemini batch embedding response missing embeddings. Response: {Response}", responseBody);
                throw new InvalidOperationException($"No embeddings returned. Response: {responseBody}");
            }

            var results = new List<float[]>();
            foreach (var embeddingItem in embeddingsElement.EnumerateArray())
            {
                if (!embeddingItem.TryGetProperty("values", out var valuesElement))
                {
                    _llmLogger.LogError("Gemini batch embedding item missing values. Response: {Response}", responseBody);
                    throw new InvalidOperationException($"Embedding missing values property. Response: {responseBody}");
                }

                var values = valuesElement.EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();

                results.Add(values);
            }

            _llmLogger.LogInformation("Gemini batch embedding request successful. Received {Count} embeddings", results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Gemini batch embedding API: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Submit a batch of GenerateContent requests for asynchronous processing using Gemini Batch API
    /// </summary>
    public async Task<BatchOperation> BatchGenerateContentAsync(
        List<(GeminiRequest request, Dictionary<string, object> metadata)> requests,
        string displayName,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        if (requests == null || requests.Count == 0)
            throw new ArgumentException("At least one request is required", nameof(requests));

        var activeModel = model ?? await GetActiveModelAsync(cancellationToken);

        try
        {
            _llmLogger.LogInformation("Sending Gemini batch request for {Count} items with display name '{DisplayName}' using model: {Model}",
                requests.Count, displayName, activeModel);

            // Build the batch request - note that the model is specified in the path, not in the body
            var batchRequest = new
            {
                batch = new
                {
                    displayName,
                    inputConfig = new
                    {
                        requests = new
                        {
                            requests = requests.Select(r => new
                            {
                                request = new
                                {
                                    contents = r.request.Contents,
                                    systemInstruction = r.request.SystemInstruction,
                                    generationConfig = r.request.GenerationConfig,
                                    safetySettings = r.request.SafetySettings
                                },
                                metadata = r.metadata ?? []
                            }).ToList()
                        }
                    }
                }
            };

            var requestJson = JsonSerializer.Serialize(batchRequest, Extensions.GeminiOptions);

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Use the correct batch endpoint - must include models/ prefix
            var endpoint = $"models/{activeModel}:batchGenerateContent";

            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Gemini batch request failed with status {StatusCode}: {Body}",
                    response.StatusCode, responseBody);
                throw new HttpRequestException($"Gemini Batch API returned {response.StatusCode}: {responseBody}");
            }

            // Response is an Operation object
            var operation = JsonSerializer.Deserialize<BatchOperation>(responseBody, Extensions.GeminiOptions);

            _llmLogger.LogInformation("Gemini batch request submitted successfully. Operation: {OperationName}", operation?.Name);

            return operation;
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error calling Gemini batch API: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Get the status and results of a batch operation using batches.get API
    /// </summary>
    public async Task<BatchOperation> GetBatchOperationAsync(
        string operationName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name is required", nameof(operationName));

        try
        {
            _llmLogger.LogInformation("Checking Gemini batch operation status for: {OperationName}", operationName);

            // Operation name format is "batches/{batchId}" - use it as-is
            var response = await _httpClient.GetAsync(operationName, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _llmLogger.LogError("Gemini batch operation check failed with status {StatusCode}: {Body}",
                    response.StatusCode, responseBody);
                throw new HttpRequestException($"Failed to get batch operation {response.StatusCode}: {responseBody}");
            }

            var operation = JsonSerializer.Deserialize<BatchOperation>(responseBody, Extensions.GeminiOptions);

            if (operation?.Done == true)
            {
                _llmLogger.LogInformation("Gemini batch operation completed successfully. State: {State}",
                    operation.Response?.State);
            }
            else
            {
                _llmLogger.LogInformation("Gemini batch operation still in progress: {OperationName}", operationName);
            }

            return operation;
        }
        catch (Exception ex)
        {
            _llmLogger.LogError(ex, "Error checking Gemini batch operation status: {Message}", ex.Message);
            throw;
        }
    }
}