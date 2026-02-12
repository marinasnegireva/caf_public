namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with semantically similar context data.
/// Uses vector similarity search to find relevant data based on the current input.
/// Supports LLM-based query transformation for improved semantic matching.
/// Quotas are token-based limits, not item counts.
/// </summary>
public class SemanticDataEnricher(
    ISemanticService semanticService,
    IProfileService profileService,
    ISettingService settingService,
    ILogger<SemanticDataEnricher> logger) : IEnricher
{
    private const int MaxResultsPerType = 20; // Fetch more, then trim by tokens

    public async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        if (state?.Session == null || state.CurrentTurn == null)
        {
            logger.LogDebug("SemanticDataEnricher skipped: no session or current turn available");
            return;
        }

        if (string.IsNullOrWhiteSpace(state.CurrentTurn.Input))
        {
            logger.LogDebug("SemanticDataEnricher skipped: empty input");
            return;
        }

        try
        {
            var profileId = profileService.GetActiveProfileId();

            // Get token quotas for each data type from settings
            var quoteQuota = await settingService.GetIntAsync(SettingsKeys.SemanticTokenQuota_Quote, 3000, cancellationToken);
            var memoryQuota = await settingService.GetIntAsync(SettingsKeys.SemanticTokenQuota_Memory, 4500, cancellationToken);
            var insightQuota = await settingService.GetIntAsync(SettingsKeys.SemanticTokenQuota_Insight, 2250, cancellationToken);
            var voiceSampleQuota = await settingService.GetIntAsync(SettingsKeys.SemanticTokenQuota_PersonaVoiceSample, 2250, cancellationToken);

            // Check if LLM query transformation is enabled
            var useLLMTransformation = await settingService.GetBoolAsync(SettingsKeys.SemanticUseLLMQueryTransformation, true, cancellationToken);

            // Build type limits for multi-type search
            var typeLimits = new Dictionary<DataType, int>();
            if (quoteQuota > 0)
                typeLimits[DataType.Quote] = MaxResultsPerType * 5;
            if (memoryQuota > 0)
                typeLimits[DataType.Memory] = MaxResultsPerType;
            if (insightQuota > 0)
                typeLimits[DataType.Insight] = MaxResultsPerType;
            if (voiceSampleQuota > 0)
                typeLimits[DataType.PersonaVoiceSample] = MaxResultsPerType * 5;

            if (typeLimits.Count == 0)
            {
                logger.LogDebug("SemanticDataEnricher: all quotas are 0");
                return;
            }

            // Use LLM query transformation for richer semantic search, or direct search
            var resultsByType = useLLMTransformation
                ? await semanticService.SearchWithQueryTransformationAsync(
                    state,
                    profileId,
                    typeLimits,
                    cancellationToken)
                : await semanticService.SearchMultiTypeAsync(
                    state.CurrentTurn.Input,
                    profileId,
                    typeLimits,
                    cancellationToken);

            var allSemanticData = new List<ContextData>();

            // Apply token quotas per type
            if (resultsByType.TryGetValue(DataType.Quote, out var quotes))
                allSemanticData.AddRange(ApplyTokenQuota(quotes, quoteQuota));

            if (resultsByType.TryGetValue(DataType.Memory, out var memories))
                allSemanticData.AddRange(ApplyTokenQuota(memories, memoryQuota));

            if (resultsByType.TryGetValue(DataType.Insight, out var insights))
                allSemanticData.AddRange(ApplyTokenQuota(insights, insightQuota));

            if (resultsByType.TryGetValue(DataType.PersonaVoiceSample, out var voiceSamples))
                allSemanticData.AddRange(ApplyTokenQuota(voiceSamples, voiceSampleQuota));

            if (allSemanticData.Count > 0)
            {
                // Filter out any data that's already loaded via other mechanisms (by checking all type-specific collections)
                var existingIds = state.GetAllContextDataIds().ToHashSet();

                var uniqueSemantic = allSemanticData
                    .Where(d => !existingIds.Contains(d.Id))
                    .ToList();

                // Add to type-specific collections
                state.AddContextDataRange(uniqueSemantic);

                var totalTokens = uniqueSemantic.Sum(d => d.TokenCount ?? 0);
                logger.LogInformation(
                    "SemanticDataEnricher found {Count} entries ({Tokens} tokens) for session {SessionId}",
                    uniqueSemantic.Count,
                    totalTokens,
                    state.Session.Id);

                // Log breakdown by type
                var byType = uniqueSemantic.GroupBy(d => d.Type)
                    .Select(g => $"{g.Key}: {g.Count()} ({g.Sum(d => d.TokenCount ?? 0)} tokens)")
                    .ToList();
                if (byType.Count > 0)
                {
                    logger.LogDebug("Semantic results by type: {Breakdown}", string.Join(", ", byType));
                }
            }
            else
            {
                logger.LogDebug("SemanticDataEnricher: no semantic matches found");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to perform semantic search for session {SessionId}", state.Session.Id);
        }
    }

    /// <summary>
    /// Applies a token quota to a list of context data, keeping items until the quota is exceeded.
    /// Items are assumed to be ordered by relevance (most relevant first).
    /// Items without token counts are skipped.
    /// </summary>
    private static List<ContextData> ApplyTokenQuota(List<ContextData> data, int tokenQuota)
    {
        var result = new List<ContextData>();
        var currentTokens = 0;

        foreach (var item in data)
        {
            // Skip items without token counts
            if (!item.TokenCount.HasValue || item.TokenCount.Value == 0)
                continue;

            var itemTokens = item.TokenCount.Value;

            // Always include at least one item if we have any
            if (result.Count == 0 || currentTokens + itemTokens <= tokenQuota)
            {
                result.Add(item);
                currentTokens += itemTokens;
            }
            else
            {
                break; // Stop once quota is exceeded
            }
        }

        return result;
    }
}