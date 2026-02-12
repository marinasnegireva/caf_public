namespace CAF.Services.Telegram;

public class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public long AllowedChatId { get; set; }
    public bool EnablePolling { get; set; } = true;
    public bool EnableWebhook { get; set; } = false;
    public string? WebhookUrl { get; set; }
}