namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with recent turn history and previous turn references
/// </summary>
public class TurnHistoryEnricher(
    IServiceProvider serviceProvider,
    ILogger<TurnHistoryEnricher> logger) : IEnricher
{
    public async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        if (state?.Session == null)
        {
            logger.LogDebug("TurnHistoryEnricher skipped: no session available");
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GeneralDbContext>();

            // Get all accepted turns ordered by creation date, then by ID for deterministic ordering
            var acceptedTurns = await dbContext.Turns
                .Where(t => t.SessionId == state.Session.Id && t.Accepted)
                .OrderBy(t => t.CreatedAt)
                .ThenBy(t => t.Id)
                .ToArrayAsync(cancellationToken);

            if (acceptedTurns.Length == 0)
            {
                logger.LogDebug("No accepted turns found for session {SessionId}", state.Session.Id);
                return;
            }

            // Get the most recent N turns for history (maintaining chronological order)
            var lastTurnsStartIndex = Math.Max(0, acceptedTurns.Length - state.RecentTurnsCount);
            state.RecentTurns = [.. acceptedTurns.Skip(lastTurnsStartIndex)];

            // Keep the last accepted turn for backward compatibility
            state.PreviousTurn = acceptedTurns[^1];
            state.PreviousResponse = state.PreviousTurn.Response;

            logger.LogDebug(
                "Loaded {RecentTurnCount} recent turns for session {SessionId}",
                state.RecentTurns.Count,
                state.Session.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get previous turns for session {SessionId}", state.Session.Id);
        }
    }
}