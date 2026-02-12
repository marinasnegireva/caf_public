namespace CAF.Services;

public sealed class TurnStripperBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<TurnStripperBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Turn Stripper Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessActiveSessionAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing active session turn stripping");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
        }

        logger.LogInformation("Turn Stripper Background Service stopped");
    }

    private async Task ProcessActiveSessionAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();

        var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var turnStripper = scope.ServiceProvider.GetRequiredService<ITurnStripperService>();
        var contextBuilder = scope.ServiceProvider.GetRequiredService<IConversationContextBuilder>();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GeneralDbContext>>();

        var activeSession = await sessionService.GetActiveSessionAsync();
        if (activeSession == null)
        {
            logger.LogDebug("No active session found, skipping turn stripping");
            return;
        }

        using var db = dbFactory.CreateDbContext();

        // Get unstripped accepted turns from the active session
        var unstrippedTurns = await db.Turns
            .Where(t => t.SessionId == activeSession.Id &&
                        t.Accepted &&
                        (t.StrippedTurn == null || t.StrippedTurn == ""))
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (unstrippedTurns.Count == 0)
        {
            logger.LogDebug("No unstripped turns found for active session {SessionId}", activeSession.Id);
            return;
        }

        logger.LogInformation("Processing {Count} unstripped turns for session {SessionId}",
            unstrippedTurns.Count, activeSession.Id);

        // Build context once for the session
        var dummyTurn = new Turn { Id = 0, SessionId = activeSession.Id, Input = "", Response = "", CreatedAt = DateTime.UtcNow };
        var context = await contextBuilder.BuildContextAsync(dummyTurn, activeSession, cancellationToken);

        // Process up to 20 turns concurrently
        var semaphore = new SemaphoreSlim(20);
        var tasks = unstrippedTurns.Select(async turn =>
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Create a scoped service provider for each concurrent task
                using var taskScope = serviceProvider.CreateScope();
                var scopedTurnStripper = taskScope.ServiceProvider.GetRequiredService<ITurnStripperService>();

                logger.LogDebug("Stripping turn {TurnId}", turn.Id);
                await scopedTurnStripper.StripAndSaveTurnAsync(turn, context, cancellationToken);
                logger.LogDebug("Successfully stripped turn {TurnId}", turn.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to strip turn {TurnId}", turn.Id);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        logger.LogInformation("Completed turn stripping for session {SessionId}", activeSession.Id);
    }
}