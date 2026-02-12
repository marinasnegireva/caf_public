namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with dialogue log from older turns
/// </summary>
public class DialogueLogEnricher(
    IServiceProvider serviceProvider,
    ILogger<DialogueLogEnricher> logger) : IEnricher
{
    public async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        if (state?.Session == null)
        {
            logger.LogDebug("DialogueLogEnricher skipped: no session available");
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GeneralDbContext>();

            var acceptedTurns = await dbContext.Turns
                .Where(t => t.SessionId == state.Session.Id && t.Accepted)
                .OrderBy(t => t.CreatedAt)
                .ToArrayAsync(cancellationToken);

            if (acceptedTurns.Length == 0)
            {
                return;
            }

            var lastTurnsStartIndex = Math.Max(0, acceptedTurns.Length - state.RecentTurnsCount);

            // Build dialogue log for older turns (like GlobalContext.FillDialogueLog)
            if (lastTurnsStartIndex > 0)
            {
                var logSb = new StringBuilder();
                logSb.AppendLine("`[meta] Log: Older events this session - For Information Only, DO NOT USE THIS FORMAT`");

                var compressedStartIndex = Math.Max(0, lastTurnsStartIndex - state.MaxDialogueLogTurns);
                var truncatedTurns = compressedStartIndex;
                if (truncatedTurns > 0)
                {
                    logSb.AppendLine("---");
                    logSb.AppendLine($"Truncated {truncatedTurns} earlier turns");
                    logSb.AppendLine("---");
                }

                for (var i = compressedStartIndex; i < lastTurnsStartIndex; i++)
                {
                    var turn = acceptedTurns[i];

                    if (string.IsNullOrWhiteSpace(turn.StrippedTurn))
                    {
                        logger.LogWarning(
                            "DialogueLog includes turn {TurnId} with missing StrippedTurn.",
                            turn.Id);
                    }

                    // Use StrippedTurn if available, otherwise fallback to Input + Response
                    var turnContent = !string.IsNullOrWhiteSpace(turn.StrippedTurn)
                        ? turn.StrippedTurn
                        : $"{turn.Input}\n{turn.Response}";

                    logSb.AppendLine(turnContent);
                    logSb.AppendLine();
                }

                state.DialogueLog = logSb.Length > 0 ? logSb.ToString() : null;
            }

            logger.LogDebug(
                "Built dialogue log for session {SessionId} with {TurnCount} turns",
                state.Session.Id,
                lastTurnsStartIndex > 0 ? lastTurnsStartIndex : 0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to build dialogue log for session {SessionId}", state.Session.Id);
        }
    }
}