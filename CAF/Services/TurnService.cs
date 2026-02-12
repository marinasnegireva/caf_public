using CAF.Services.Conversation;

namespace CAF.Services;

public class TurnService(GeneralDbContext context) : ITurnService
{
    public async Task<Turn?> GetByIdAsync(int id)
    {
        return await context.Turns
            .Include(t => t.Session)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<List<Turn>> GetTurnsBySessionIdAsync(int sessionId)
    {
        return await context.Turns
            .Where(t => t.SessionId == sessionId && t.Accepted)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Turn>> GetRecentTurnsAsync(int sessionId, int count, CancellationToken cancellationToken = default)
    {
        return await context.Turns
            .Where(t => t.SessionId == sessionId && t.Accepted)
            .OrderByDescending(t => t.CreatedAt)
            .Take(count)
            .OrderBy(t => t.CreatedAt) // Re-order chronologically
            .ToListAsync(cancellationToken);
    }

    public async Task<Turn> CreateTurnAsync(int sessionId, string input)
    {
        var turn = new Turn
        {
            SessionId = sessionId,
            Input = input,
            CreatedAt = DateTime.UtcNow
        };

        context.Turns.Add(turn);
        await context.SaveChangesAsync();

        return turn;
    }

    public async Task<Turn> UpdateTurnAsync(ConversationState state, bool accepted, CancellationToken cancellationToken = default)
    {
        var turn = state.CurrentTurn;
        var dbTurn = await context.Turns.FindAsync([turn.Id], cancellationToken) 
            ?? throw new KeyNotFoundException($"Turn with id {turn.Id} not found");

        dbTurn.Input = turn.Input;
        dbTurn.Response = turn.Response;
        dbTurn.StrippedTurn = turn.StrippedTurn;
        dbTurn.JsonInput = state.GeminiRequest?.ToJson() ?? state.ClaudeRequest?.ToJson();
        dbTurn.Accepted = accepted;

        // Mark all context data as used by setting UsedLastOnTurnId
        var contextDataIds = state.GetAllContextDataIds();
        if (contextDataIds.Count > 0)
        {
            var contextDataToUpdate = await context.ContextData
                .Where(cd => contextDataIds.Contains(cd.Id))
                .ToListAsync(cancellationToken);

            foreach (var contextData in contextDataToUpdate)
            {
                contextData.UsedLastOnTurnId = turn.Id;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        return dbTurn;
    }

    // Legacy overload for backward compatibility
    public async Task<Turn> UpdateTurnAsync(int id, string? input = null, string? jsonInput = null,
        string? response = null, string? strippedTurn = null, bool? accepted = null)
    {
        var turn = await context.Turns.FindAsync(id) ?? throw new KeyNotFoundException($"Turn with id {id} not found");

        if (input != null)
            turn.Input = input;

        if (jsonInput != null)
            turn.JsonInput = jsonInput;

        if (response != null)
            turn.Response = response;

        if (strippedTurn != null)
            turn.StrippedTurn = strippedTurn;

        if (accepted.HasValue)
            turn.Accepted = accepted.Value;

        await context.SaveChangesAsync();

        return turn;
    }

    public async Task<bool> DeleteTurnAsync(int id)
    {
        var turn = await context.Turns.FindAsync(id);
        if (turn == null)
        {
            return false;
        }

        context.Turns.Remove(turn);
        await context.SaveChangesAsync();

        return true;
    }
}