namespace CAF.Services.Conversation.Providers;

/// <summary>
/// LLM provider strategy for Gemini, supporting both standard and batch processing
/// </summary>
public class GeminiProviderStrategy(
    IGeminiClient geminiClient,
    ISettingService settingService,
    ILogger<GeminiProviderStrategy> logger) : ILLMProviderStrategy
{
    public string ProviderName => ConversationConstants.GeminiProvider;

    public async Task<(bool success, string result)> ExecuteAsync(
        ConversationState state,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Using Gemini for turn {TurnId}", state.CurrentTurn.Id);

        return await geminiClient.GenerateContentAsync(
            state.GeminiRequest,
            false,
            state.CurrentTurn.Id,
            cancellationToken);
    }
}