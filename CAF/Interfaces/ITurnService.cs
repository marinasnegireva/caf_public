using CAF.Services.Conversation;

namespace CAF.Interfaces;

public interface ITurnService
{
    Task<Turn?> GetByIdAsync(int id);

    Task<List<Turn>> GetTurnsBySessionIdAsync(int sessionId);

    Task<List<Turn>> GetRecentTurnsAsync(int sessionId, int count, CancellationToken cancellationToken = default);

    Task<Turn> CreateTurnAsync(int sessionId, string input);

    Task<Turn> UpdateTurnAsync(ConversationState state, bool accepted, CancellationToken cancellationToken = default);

    // Legacy overload for backward compatibility
    Task<Turn> UpdateTurnAsync(int id, string? input = null, string? jsonInput = null,
        string? response = null, string? strippedTurn = null, bool? accepted = null);

    Task<bool> DeleteTurnAsync(int id);
}