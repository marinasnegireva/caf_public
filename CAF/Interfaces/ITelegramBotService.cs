namespace CAF.Interfaces;

public interface ITelegramBotService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<string> ProcessAdminCommandAsync(string command);
}