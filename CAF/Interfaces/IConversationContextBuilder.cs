using CAF.Services.Conversation;

namespace CAF.Interfaces;

/// <summary>
/// Service responsible for building ConversationContext objects
/// </summary>
public interface IConversationContextBuilder
{
    Task<ConversationState> BuildContextAsync(Turn currentTurn, Session session, CancellationToken cancellationToken = default);
}