using CAF.Services.Conversation;

namespace CAF.Interfaces;

/// <summary>
/// Strategy interface for LLM provider-specific execution logic
/// </summary>
public interface ILLMProviderStrategy
{
    /// <summary>
    /// Gets the name of the LLM provider this strategy handles
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Executes the LLM request for this provider
    /// </summary>
    /// <param name="state">The conversation state containing the request and context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of success flag and result text</returns>
    Task<(bool success, string result)> ExecuteAsync(
        ConversationState state,
        CancellationToken cancellationToken = default);
}