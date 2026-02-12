using CAF.Services.Conversation;

namespace CAF.Interfaces;

public interface IConversationRequestBuilder
{
    Task<GeminiRequest> BuildGeminiRequestAsync(ConversationState context, CancellationToken cancellationToken = default);

    Task<ClaudeRequest> BuildClaudeRequestAsync(ConversationState context, CancellationToken cancellationToken = default);
}