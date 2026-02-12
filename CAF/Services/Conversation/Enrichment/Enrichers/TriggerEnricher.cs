namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enricher that evaluates trigger-based ContextData items and adds matched content to conversation state.
/// Triggers are ContextData items with Availability = Trigger that activate based on keyword matching.
/// </summary>
public class TriggerEnricher(
    IContextDataService contextDataService,
    ITurnService turnService,
    ISettingService settingService,
    ILogger<TriggerEnricher> logger) : IEnricher
{
    public async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        // Get all active trigger items
        var triggers = await contextDataService.GetAllAsync(
            type: null,
            availability: AvailabilityType.Trigger,
            includeArchived: false,
            cancellationToken);

        if (triggers.Count == 0)
        {
            logger.LogDebug("No active triggers configured");
            return;
        }

        var sessionId = state.Session.Id;
        var userInput = state.CurrentTurn.Input;

        logger.LogDebug("Evaluating {Count} trigger items for session {SessionId}",
            triggers.Count, sessionId);

        // Get the maximum lookback needed
        var maxLookback = triggers.Max(t => t.TriggerLookbackTurns);

        // Get recent turns for this session
        var recentTurns = await turnService.GetRecentTurnsAsync(sessionId, maxLookback, cancellationToken);

        logger.LogDebug("Retrieved {Count} recent turns for trigger evaluation", recentTurns.Count);

        foreach (var trigger in triggers.Where(t => !string.IsNullOrWhiteSpace(t.TriggerKeywords)))
        {
            // Build the text to scan based on this trigger's lookback setting
            var turnsToScan = recentTurns.Take(trigger.TriggerLookbackTurns).ToList();
            var textBuilder = new StringBuilder();

            foreach (var turn in turnsToScan)
            {
                if (!string.IsNullOrWhiteSpace(turn.Input))
                    textBuilder.AppendLine(turn.Input);
                if (!string.IsNullOrWhiteSpace(turn.Response))
                    textBuilder.AppendLine(turn.Response);
            }

            // Add current input and any additional words from settings
            var additionalWords = await settingService.GetValueAsync(SettingsKeys.TriggerScanTextAdditionalWords, cancellationToken);
            var scanText = textBuilder.ToString().ToLowerInvariant() + " " + userInput.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(additionalWords))
            {
                scanText += " " + additionalWords.ToLowerInvariant();
            }

            var keywords = trigger.TriggerKeywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(k => k.ToLowerInvariant())
                .ToList();

            logger.LogDebug(
                "Evaluating trigger '{TriggerName}' (ID: {Id}, lookback: {Lookback}, keywords: [{Keywords}]) against text length: {TextLength}",
                trigger.Name,
                trigger.Id,
                trigger.TriggerLookbackTurns,
                string.Join("', '", keywords),
                scanText.Length);

            // Find matching keywords
            var matchedKeywords = new List<string>();
            foreach (var keyword in keywords)
            {
                var pattern = $@"\b{Regex.Escape(keyword)}\b";
                var isMatch = Regex.IsMatch(scanText, pattern, RegexOptions.IgnoreCase);

                logger.LogDebug(
                    "Keyword '{Keyword}': {Result}",
                    keyword,
                    isMatch ? "MATCH" : "no match");

                if (isMatch)
                {
                    matchedKeywords.Add(keyword);
                }
            }

            logger.LogDebug(
                "Trigger '{TriggerName}': matched {MatchCount}/{RequiredCount} keywords: {Matched}",
                trigger.Name,
                matchedKeywords.Count,
                trigger.TriggerMinMatchCount,
                matchedKeywords.Count > 0 ? string.Join(", ", matchedKeywords) : "none");

            if (matchedKeywords.Count >= trigger.TriggerMinMatchCount)
            {
                // Add to appropriate collection based on data type
                state.AddContextData(trigger);

                logger.LogInformation(
                    "✓ Trigger '{TriggerName}' (ID: {Id}, Type: {Type}) ACTIVATED by keywords: {Keywords}",
                    trigger.Name,
                    trigger.Id,
                    trigger.Type,
                    string.Join(", ", matchedKeywords));

                // Update usage count
                trigger.UsageCount++;
                await contextDataService.UpdateAsync(trigger.Id, trigger, cancellationToken);
            }
            else
            {
                logger.LogDebug(
                    "✗ Trigger '{TriggerName}' NOT activated (needed {Needed}, got {Got})",
                    trigger.Name,
                    trigger.TriggerMinMatchCount,
                    matchedKeywords.Count);
            }
        }
    }
}