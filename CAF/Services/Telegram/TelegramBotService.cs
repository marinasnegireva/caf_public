using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace CAF.Services.Telegram;

public class TelegramBotService : ITelegramBotService
{
    private readonly TelegramBotClient _botClient;
    private readonly TelegramBotOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly DateTime _startTime;
    private CancellationTokenSource? _cts;

    private readonly Lock _chatCtsLock = new();
    private readonly Dictionary<long, CancellationTokenSource> _chatProcessingCts = [];

    public TelegramBotService(
        IOptions<TelegramBotOptions> options,
        IServiceProvider serviceProvider,
        ILogger<TelegramBotService> logger)
    {
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _startTime = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            throw new InvalidOperationException("Telegram bot token is not configured.");
        }

        _botClient = new TelegramBotClient(_options.BotToken);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.EnablePolling)
        {
            _logger.LogInformation("Telegram bot polling is disabled.");
            return;
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message]
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _cts.Token
        );

        var me = await _botClient.GetMe(cancellationToken);
        _logger.LogInformation("Telegram bot started: @{BotUsername}", me.Username);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts != null)
        {
            await _cts.CancelAsync();
            _cts.Dispose();
            _cts = null;
        }

        _logger.LogInformation("Telegram bot stopped.");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { } message || message.Text is null || message.Date < _startTime)
            return;

        // Fire and forget the processing
        _ = Task.Run(async () =>
        {
            var chatId = message.Chat.Id;

            // Check if chat is allowed
            if (_options.AllowedChatId != 0 && _options.AllowedChatId != chatId)
            {
                _logger.LogWarning("Unauthorized chat attempt from {ChatId}", chatId);
                return;
            }

            var input = message.Text.Trim();
            _logger.LogInformation("Message from {ChatId}: {Input}", chatId, input);

            if (string.IsNullOrWhiteSpace(input))
                return;

            try
            {
                if (input.Equals(ConversationConstants.TelegramCommands.Cancel, StringComparison.OrdinalIgnoreCase))
                {
                    if (TryCancelChatProcessing(chatId))

                        await botClient.SendMessage(chatId, "Cancelled the current request.", cancellationToken: cancellationToken);
                    else
                        await botClient.SendMessage(chatId, "No request is currently running.", cancellationToken: cancellationToken);

                    return;
                }

                if (input.StartsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle admin commands
                    var response = await ProcessAdminCommandAsync(input);
                    await botClient.SendMessage(chatId, response, cancellationToken: cancellationToken);
                }
                else
                {
                    var processingCts = BeginChatProcessing(chatId, cancellationToken);

                    try
                    {
                        // Process through conversation pipeline
                        using var scope = _serviceProvider.CreateScope();
                        var pipeline = scope.ServiceProvider.GetRequiredService<IConversationPipeline>();

                        var turn = await pipeline.ProcessInputAsync(input, processingCts.Token);

                        if (turn?.Response == null)
                        {
                            await botClient.SendMessage(chatId, "Error processing input.", cancellationToken: cancellationToken);
                            return;
                        }

                        var responseText = turn.DisplayResponse ?? turn.Response?? string.Empty;

                        const int maxChunkSize = 4000;
                        if (responseText.Length <= maxChunkSize)
                        {
                            await botClient.SendMessage(chatId, responseText, cancellationToken: cancellationToken);
                        }
                        else
                        {
                            var pos = 0;
                            while (pos < responseText.Length)
                            {
                                var len = Math.Min(maxChunkSize, responseText.Length - pos);
                                var part = responseText.Substring(pos, len);
                                await botClient.SendMessage(chatId, part, cancellationToken: cancellationToken);
                                pos += len;
                            }
                        }
                    }
                    finally
                    {
                        EndChatProcessing(chatId, processingCts);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation from {ChatId}", chatId);
                await botClient.SendMessage(chatId, ex.Message, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Processing cancelled for message from {ChatId}", chatId);
                try
                {
                    await botClient.SendMessage(chatId, "Processing was cancelled.", cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Cancelled message delivery also cancelled.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {ChatId}", chatId);
                try
                {
                    await botClient.SendMessage(chatId, "An error occurred while processing your request.", cancellationToken: cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Error message delivery was cancelled.");
                }
            }
        }, cancellationToken);
    }

    private CancellationTokenSource BeginChatProcessing(long chatId, CancellationToken outerToken)
    {
        CancellationTokenSource cts;
        lock (_chatCtsLock)
        {
            if (_chatProcessingCts.TryGetValue(chatId, out var existing))
            {
                try
                { existing.Cancel(); }
                catch { }
                existing.Dispose();
                _chatProcessingCts.Remove(chatId);
            }

            cts = CancellationTokenSource.CreateLinkedTokenSource(outerToken);
            _chatProcessingCts[chatId] = cts;
        }

        return cts;
    }

    private void EndChatProcessing(long chatId, CancellationTokenSource cts)
    {
        lock (_chatCtsLock)
        {
            if (_chatProcessingCts.TryGetValue(chatId, out var existing) && ReferenceEquals(existing, cts))
            {
                _chatProcessingCts.Remove(chatId);
            }
        }

        cts.Dispose();
    }

    private bool TryCancelChatProcessing(long chatId)
    {
        lock (_chatCtsLock)
        {
            if (!_chatProcessingCts.TryGetValue(chatId, out var cts))
                return false;

            cts.Cancel();
            return true;
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var errorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error: [{apiRequestException.ErrorCode}] {apiRequestException.Message}",
            _ => exception.ToString()
        };

        _logger.LogError(exception, "Telegram polling error: {ErrorMessage}", errorMessage);
        return Task.CompletedTask;
    }

    public async Task<string> ProcessAdminCommandAsync(string command)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sessionService = scope.ServiceProvider.GetRequiredService<ISessionService>();

            if (command.StartsWith(ConversationConstants.TelegramCommands.Status))
            {
                var activeSession = await sessionService.GetActiveSessionAsync();

                var allSessions = await sessionService.GetAllSessionsAsync();

                return $"? Bot is running.\n" +
                       $"?? Total sessions: {allSessions.Count}\n" +
                        $"?? Active session: {(activeSession != null ? $"#{activeSession.Number} - {activeSession.Name}" : "None")}";
            }
            else if (command.StartsWith(ConversationConstants.TelegramCommands.Restart))
            {
                var activeSession = await sessionService.GetActiveSessionAsync();

                if (activeSession == null)
                {
                    return "? No active session to restart.";
                }

                // Create a new session with similar name
                var newSession = await sessionService.CreateSessionAsync($"{activeSession.Name} (Restarted)");
                await sessionService.SetActiveSessionAsync(newSession.Id);

                return $"?? Session restarted. New session #{newSession.Number} created and activated.";
            }
            else if (command.StartsWith(ConversationConstants.TelegramCommands.New))
            {
                // Extract session name from command if provided
                var sessionName = command.Length > ConversationConstants.TelegramCommands.New.Length
                    ? command[ConversationConstants.TelegramCommands.New.Length..].Trim()
                    : $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}";

                var session = await sessionService.CreateSessionAsync(sessionName);

                await sessionService.SetActiveSessionAsync(session.Id);

                return $"? New session #{session.Number} '{session.Name}' created and activated.";
            }
            else if (command.StartsWith(ConversationConstants.TelegramCommands.Sessions))
            {
                var sessions = await sessionService.GetAllSessionsAsync();

                if (sessions.Count == 0)
                {
                    return "?? No sessions found.";
                }

                var activeSession = sessions.FirstOrDefault(s => s.IsActive);
                var response = "?? All Sessions:\n\n";

                foreach (var session in sessions.Take(10))
                {
                    var isActive = session.Id == activeSession?.Id ? "?? " : "";
                    response += $"{isActive}#{session.Number} - {session.Name}\n";
                    response += $"   Turns: {session.Turns?.Count ?? 0} | Created: {session.CreatedAt:g}\n\n";
                }

                if (sessions.Count > 10)
                {
                    response += $"... and {sessions.Count - 10} more sessions.";
                }

                return response;
            }
            else if (command.StartsWith(ConversationConstants.TelegramCommands.Activate))
            {
                // Extract session number from command
                var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2 || !int.TryParse(parts[1], out var sessionNumber))
                {
                    return "? Usage: /activate <session_number>";
                }

                var sessions = await sessionService.GetAllSessionsAsync();
                var session = sessions.FirstOrDefault(s => s.Number == sessionNumber);

                if (session == null)
                {
                    return $"? Session #{sessionNumber} not found.";
                }

                await sessionService.SetActiveSessionAsync(session.Id);
                return $"? Session #{session.Number} '{session.Name}' activated.";
            }
            else
            {
                return command.StartsWith(ConversationConstants.TelegramCommands.Help)
                    ? "?? Available Commands:\n\n" +
                                       $"{ConversationConstants.TelegramCommands.Status} - Show bot and session status\n" +
                                       $"{ConversationConstants.TelegramCommands.New} [name] - Create and activate a new session\n" +
                                       $"{ConversationConstants.TelegramCommands.Restart} - Restart current session\n" +
                                       $"{ConversationConstants.TelegramCommands.Sessions} - List all sessions\n" +
                                       $"{ConversationConstants.TelegramCommands.Activate} <number> - Activate a specific session\n" +
                                       $"{ConversationConstants.TelegramCommands.Help} - Show this help message"
                    : "? Unknown command. Use /help to see available commands.";
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing admin command: {Command}", command);
            return $"? Error processing command: {ex.Message}";
        }
    }
}