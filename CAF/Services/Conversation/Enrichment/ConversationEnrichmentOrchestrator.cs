namespace CAF.Services.Conversation.Enrichment;

/// <summary>
/// Orchestrates all conversation enrichment processes, running them asynchronously
/// and populating the ConversationState
/// </summary>
public class ConversationEnrichmentOrchestrator(
    IEnumerable<IEnricher> enrichers,
    ILogger<ConversationEnrichmentOrchestrator> logger) : IConversationEnrichmentOrchestrator
{
    public async Task EnrichAsync(
        ConversationState state,
        CancellationToken cancellationToken = default)
    {
        if (state?.CurrentTurn == null)
        {
            logger.LogWarning("EnrichAsync called with null state or current turn");
            return;
        }

        logger.LogDebug("Starting enrichment processes for turn {TurnId}", state.CurrentTurn.Id);

        // Run all enrichment processes in parallel
        var enrichmentTasks = enrichers
            .Select(enricher => enricher.EnrichAsync(state, cancellationToken))
            .ToArray();

        await Task.WhenAll(enrichmentTasks);

        logger.LogInformation(
            "Enrichment complete for turn {TurnId}",
            state.CurrentTurn.Id);
    }
}