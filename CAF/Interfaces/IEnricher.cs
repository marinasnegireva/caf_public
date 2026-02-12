using CAF.Services.Conversation;

namespace CAF.Interfaces;

/// <summary>
/// Interface for conversation enrichment components that populate ConversationState
/// </summary>
public interface IEnricher
{
    /// <summary>
    /// Enriches the conversation state with specific data
    /// </summary>
    /// <param name="state">The conversation state to enrich</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default);
}