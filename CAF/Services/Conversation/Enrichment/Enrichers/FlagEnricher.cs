namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with active flags
/// </summary>
public class FlagEnricher(
    IServiceProvider serviceProvider,
    ILogger<FlagEnricher> logger) : IEnricher
{
    public async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GeneralDbContext>();

            var profileId = state.Session?.ProfileId;

            // Load active or constant flags for the current profile
            var activeFlags = await db.Flags
                .Where(f => (f.Active || f.Constant) && f.ProfileId == profileId)
                .OrderByDescending(f => f.Active)
                .ThenByDescending(f => f.LastUsedAt ?? f.CreatedAt)
                .ToListAsync(cancellationToken);

            if (activeFlags.Count > 0)
            {
                state.Flags = [.. activeFlags];

                logger.LogInformation(
                    "Loaded {Count} active flags for profile {ProfileId}: {Values}",
                    activeFlags.Count,
                    profileId,
                    string.Join(", ", activeFlags.Select(f => f.Value)));
            }
            else
            {
                logger.LogDebug("No active flags found for profile {ProfileId}", profileId);
                state.Flags = [];
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load active flags");
            state.Flags = [];
        }
    }
}