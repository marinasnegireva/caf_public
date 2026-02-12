namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Base class for data type-specific enrichers.
/// Each enricher handles all applicable availability mechanisms for its data type.
/// </summary>
public abstract class DataTypeEnricherBase<TEnricher>(
    IContextDataService contextDataService,
    IDbContextFactory<GeneralDbContext> dbContextFactory,
    ILogger<TEnricher> logger) : IEnricher where TEnricher : class
{
    protected IContextDataService ContextDataService => contextDataService;
    protected IDbContextFactory<GeneralDbContext> DbContextFactory => dbContextFactory;
    protected ILogger<TEnricher> Logger => logger;

    /// <summary>
    /// The data type this enricher handles
    /// </summary>
    protected abstract DataType DataType { get; }

    /// <summary>
    /// The name of this enricher for logging
    /// </summary>
    protected abstract string EnricherName { get; }

    /// <summary>
    /// Whether this data type supports Manual availability
    /// </summary>
    protected abstract bool SupportsManual { get; }

    /// <summary>
    /// Whether this data type supports Semantic availability
    /// </summary>
    protected abstract bool SupportsSemantic { get; }

    public virtual async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        if (state?.Session == null || state.CurrentTurn == null)
        {
            Logger.LogDebug("{Enricher} skipped: no session or current turn available", EnricherName);
            return;
        }

        try
        {
            var allData = new List<ContextData>();
            var seenIds = new HashSet<int>();

            // 1. Load AlwaysOn data (all types support this)
            var alwaysOnData = await contextDataService.GetAlwaysOnDataAsync(DataType, cancellationToken);
            foreach (var data in alwaysOnData.Where(d => seenIds.Add(d.Id)))
                allData.Add(data);

            if (alwaysOnData.Count > 0)
                Logger.LogDebug("{Enricher}: loaded {Count} AlwaysOn entries", EnricherName, alwaysOnData.Count);

            // 2. Load Manual toggle data (if supported)
            if (SupportsManual)
            {
                var manualData = await contextDataService.GetActiveManualDataAsync(DataType, cancellationToken);
                foreach (var data in manualData.Where(d => seenIds.Add(d.Id)))
                    allData.Add(data);

                if (manualData.Count > 0)
                    Logger.LogDebug("{Enricher}: loaded {Count} Manual entries", EnricherName, manualData.Count);
            }

            // Add all data to the state's type-specific collection
            if (allData.Count > 0)
            {
                state.AddContextDataRange(allData);

                Logger.LogInformation(
                "{Enricher} loaded {Total} entries for session {SessionId}",
                EnricherName,
                allData.Count,
                state.Session.Id);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{Enricher} failed for session {SessionId}", EnricherName, state.Session?.Id);
        }
    }

    /// <summary>
    /// Builds the text to scan for triggers from current input and recent turns
    /// </summary>
    protected async Task<string> BuildTriggerTextAsync(ConversationState state, CancellationToken cancellationToken)
    {
        var textParts = new List<string>();

        // Add current input
        if (!string.IsNullOrWhiteSpace(state.CurrentTurn.Input))
        {
            textParts.Add(state.CurrentTurn.Input);
        }

        // Get max lookback from trigger data
        var triggerData = await contextDataService.GetTriggerDataAsync(cancellationToken);
        var typeTriggers = triggerData.Where(t => t.Type == DataType).ToList();
        var maxLookback = typeTriggers.Count > 0
            ? typeTriggers.Max(t => t.TriggerLookbackTurns)
            : 3;

        if (maxLookback > 0)
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var recentTurns = await db.Turns
                .Where(t => t.SessionId == state.Session.Id && t.Accepted)
                .OrderByDescending(t => t.CreatedAt)
                .Take(maxLookback)
                .ToListAsync(cancellationToken);

            foreach (var turn in recentTurns)
            {
                if (!string.IsNullOrWhiteSpace(turn.Input))
                    textParts.Add(turn.Input);
                if (!string.IsNullOrWhiteSpace(turn.Response))
                    textParts.Add(turn.Response);
            }
        }

        return string.Join(" ", textParts);
    }

    /// <summary>
    /// Evaluates triggers and returns only data of this enricher's type
    /// </summary>
    protected async Task<List<ContextData>> EvaluateTriggersForTypeAsync(
        string triggerText,
        CancellationToken cancellationToken)
    {
        var allTriggered = await contextDataService.EvaluateTriggersAsync(triggerText, cancellationToken);
        return [.. allTriggered.Where(d => d.Type == DataType)];
    }
}