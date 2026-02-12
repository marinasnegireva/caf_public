namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with perception processing results
/// </summary>
public class PerceptionEnricher(
    ISystemMessageService systemMessageService,
    IGeminiClient geminiClient,
    IServiceProvider serviceProvider,
    ISettingService settingService,
    ILogger<PerceptionEnricher> logger) : IEnricher
{
    public async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        // Check if perception is enabled
        var perceptionEnabled = await settingService.GetBoolAsync(
            SettingsKeys.PerceptionEnabled,
            defaultValue: true,
            cancellationToken);

        if (!perceptionEnabled)
        {
            logger.LogDebug("Perception processing is disabled via settings");
            state.Perceptions = [];
            return;
        }

        if (string.IsNullOrWhiteSpace(state.CurrentTurn.Input))
        {
            state.Perceptions = [];
            return;
        }

        // Get all active perception system messages
        var perceptionMessages = await systemMessageService.GetActivePerceptionsAsync(cancellationToken);

        if (perceptionMessages.Count == 0)
        {
            logger.LogWarning("No active perception system messages found");
            state.Perceptions = [];
            return;
        }

        var fullPerception = new ConcurrentBag<PerceptionRecord>();

        try
        {
            var tasks = perceptionMessages.Select(perceptionMessage =>
                ProcessPerceptionMessageAsync(
                    perceptionMessage,
                    state,
                    fullPerception,
                    cancellationToken));

            await Task.WhenAll(tasks);

            state.Perceptions = [.. fullPerception];

            logger.LogInformation(
                "Perception processing completed successfully for turn {TurnId}: {Count} records from {MessageCount} perception messages",
                state.CurrentTurn.Id,
                fullPerception.Count,
                perceptionMessages.Count);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Perception processing was cancelled for turn {TurnId}", state.CurrentTurn.Id);
            state.Perceptions = [];
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing perceptions for turn {TurnId}", state.CurrentTurn.Id);
            state.Perceptions = [];
        }
    }

    private async Task ProcessPerceptionMessageAsync(
        SystemMessage perceptionMessage,
        ConversationState context,
        ConcurrentBag<PerceptionRecord> fullPerception,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get context messages from attached contexts
            var contextMessages = await GetContextMessages(perceptionMessage, cancellationToken);

            var request = BuildGeminiRequest(
                perceptionMessage.Content,
                contextMessages,
                context.CurrentTurn.Input,
                context.PreviousResponse,
                context.PersonaName,
                context.UserName);

            // Get LLM response
            var response = await GetLlmResponseAsync(
                perceptionMessage.Name,
                request,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(response))
            {
                logger.LogWarning("Empty LLM response for perception message {MessageName}", perceptionMessage.Name);
                return;
            }

            // Parse perception response
            var perceptionRecords = ParsePerceptionResponse(
                response,
                perceptionMessage.Id,
                context.CurrentTurn.Id);

            // Add records to full perception
            foreach (var record in perceptionRecords)
            {
                fullPerception.Add(record);
            }

            logger.LogInformation("Processed perception message '{MessageName}': {Count} records",
                perceptionMessage.Name, perceptionRecords.Length);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Perception message '{MessageName}' processing was cancelled", perceptionMessage.Name);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing perception message '{MessageName}'", perceptionMessage.Name);
            // Don't throw - allow other perception messages to continue processing
        }
    }

    private static Task<List<string>> GetContextMessages(
        SystemMessage systemMessage,
        CancellationToken cancellationToken)
    {
        // Context messages are no longer attached to perception messages
        // Return empty list as this feature has been removed
        return Task.FromResult(new List<string>());
    }

    private static GeminiRequest BuildGeminiRequest(
        string systemInstruction,
        List<string> contextMessages,
        string input,
        string? previousResponse,
        string personaName,
        string userName)
    {
        var userMessage = string.IsNullOrEmpty(previousResponse)
            ? $"{userName}: {input}"
            : $"{personaName}: {previousResponse}\n{userName}: {input}";

        var builder = GeminiMessageBuilder
            .Create()
            .WithSystemInstruction(systemInstruction);

        foreach (var ctx in contextMessages)
        {
            if (!string.IsNullOrWhiteSpace(ctx))
            {
                builder.AddUserMessage(ctx);
            }
        }

        builder.AddUserMessage(userMessage);

        return builder.Build();
    }

    private async Task<string> GetLlmResponseAsync(
        string perceptionName,
        GeminiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await geminiClient.GenerateContentAsync(request, cancellationToken: cancellationToken);
            return response.result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling Gemini for perception '{PerceptionName}'", perceptionName);
            throw;
        }
    }

    private PerceptionRecord[] ParsePerceptionResponse(string response, int systemMessageId, int turnId)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();

            // Extract JSON array if response contains extra text
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                response = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            var records = JsonSerializer.Deserialize<PerceptionRecord[]>(response, Extensions.GeminiOptions);

            if (records == null || records.Length == 0)
            {
                logger.LogWarning("Failed to parse perception response for system message {SystemMessageId}: {Response}",
                    systemMessageId, response);
                return [];
            }

            return records;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error for system message {SystemMessageId}: {Response}",
                systemMessageId, response);
            return [];
        }
    }
}

public class PerceptionRecord
{
    public int Id { get; set; }
    public string Property { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}