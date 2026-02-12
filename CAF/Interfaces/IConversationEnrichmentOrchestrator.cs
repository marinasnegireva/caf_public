using CAF.Services.Conversation;

namespace CAF.Interfaces;

/// <summary>
/// Orchestrates all conversation enrichment processes asynchronously
/// </summary>
public interface IConversationEnrichmentOrchestrator
{
    /// <summary>
    /// Runs all enrichment processes and populates the ConversationState
    /// </summary>
    Task EnrichAsync(
        ConversationState state,
        CancellationToken cancellationToken = default);
}