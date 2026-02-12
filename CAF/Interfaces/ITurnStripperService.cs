using CAF.Services.Conversation;

namespace CAF.Interfaces;

public interface ITurnStripperService
{
    Task<Turn> StripAndSaveTurnAsync(Turn turn, ConversationState context, CancellationToken cancellationToken = default, string? modelOverride = null);

    Task StripAndSaveSessionAsync(int sessionId, ConversationState context, CancellationToken cancellationToken = default, string? modelOverride = null);
}