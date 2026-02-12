namespace CAF.Services.Conversation.Providers;

/// <summary>
/// LLM provider strategy for Claude
/// </summary>
public class ClaudeProviderStrategy(
    IClaudeClient claudeClient,
    ILogger<ClaudeProviderStrategy> logger) : ILLMProviderStrategy
{
    public string ProviderName => ConversationConstants.ClaudeProvider;

    public async Task<(bool success, string result)> ExecuteAsync(
        ConversationState state,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Using Claude for turn {TurnId}", state.CurrentTurn.Id);

        return await claudeClient.GenerateContentAsync(
            state.ClaudeRequest,
            cancellationToken,
            state.CurrentTurn.Id);
    }
}