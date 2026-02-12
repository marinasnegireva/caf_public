namespace CAF.Services.Telegram;

public class TelegramBotHostedService(IServiceProvider serviceProvider, ILogger<TelegramBotHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create a scope for starting the bot so scoped services are resolved properly
        using var scope = serviceProvider.CreateScope();
        var bot = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();

        try
        {
            await bot.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Telegram bot");
            throw;
        }

        // Keep running until stopped
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) { }
        finally
        {
            try
            {
                await bot.StopAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error stopping Telegram bot");
            }
        }
    }
}