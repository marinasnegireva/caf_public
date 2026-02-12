using CAF.Services.Conversation;

namespace CAF.Services;

public sealed class GeminiTurnStripperService(IDbContextFactory<GeneralDbContext> dbFactory, IGeminiClient geminiClient) : ITurnStripperService
{
    public async Task<Turn> StripAndSaveTurnAsync(Turn turn, ConversationState context, CancellationToken cancellationToken = default, string? modelOverride = null)
    {
        ArgumentNullException.ThrowIfNull(turn);

        if (!string.IsNullOrWhiteSpace(turn.StrippedTurn))
            return turn;

        if (string.IsNullOrWhiteSpace(turn.Input) && string.IsNullOrWhiteSpace(turn.Response))
            return turn;

        using var db = dbFactory.CreateDbContext();

        var instructions = await db.SystemMessages
            .Where(m => m.Type == SystemMessage.SystemMessageType.Technical &&
                        m.Name == ConversationConstants.TechnicalMessages.TurnStripperInstructions &&
                        m.IsActive && !m.IsArchived)

            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.Content)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        instructions ??= DefaultTechnicalMessages.TurnStripperInstructions;

        var userName = string.IsNullOrWhiteSpace(context.UserName) ? "User" : context.UserName;
        var personaName = string.IsNullOrWhiteSpace(context.PersonaName) ? "Assistant" : context.PersonaName;

        // Get last 3 accepted turns before current turn for conversation history
        var historyTurns = await db.Turns
            .Where(t => t.SessionId == turn.SessionId &&
                        t.Accepted &&
                        t.Id < turn.Id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(3)
            .OrderBy(t => t.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var systemPrompt = instructions + $"\n\nCONTEXT:\n- User name: {userName}\n- Persona name: {personaName}\n\nWhen formatting actions/dialogue, use these actual names when known.";

        // Build Gemini request using GeminiMessageBuilder
        var builder = new GeminiMessageBuilder()
            .WithSystemInstruction(systemPrompt);

        // Add conversation history as previous turns
        var conversationHistory = BuildConversationHistory(historyTurns, userName, personaName);
        if (!string.IsNullOrWhiteSpace(conversationHistory))
        {
            builder.AddUserMessage($"CONVERSATION HISTORY (for context only, do not strip these):\n{conversationHistory}")
                   .AddModelResponse("Understood. I have the conversation history for context. Please provide the current turn to strip.");
        }

        // Add current turn to strip as the final user message
        var currentTurnContent = BuildTurnContent(turn, userName, personaName);
        builder.AddUserMessage(currentTurnContent);

        var request = builder.Build();
        var (success, result) = await geminiClient.GenerateContentAsync(request, technical: true, turnId: turn.Id, cancellationToken);

        if (!success)
        {
            throw new InvalidOperationException($"Failed to strip turn {turn.Id}: {result}");
        }

        turn.StrippedTurn = result;

        db.Turns.Update(turn);
        await db.SaveChangesAsync(cancellationToken);

        // Clear navigation properties to avoid circular reference during serialization
        turn.Session = null;

        return turn;
    }

    public async Task StripAndSaveSessionAsync(int sessionId, ConversationState context, CancellationToken cancellationToken = default, string? modelOverride = null)
    {
        using var db = dbFactory.CreateDbContext();

        var turns = await db.Turns
            .Where(t => t.SessionId == sessionId &&
                        t.Accepted &&
                        t.StrippedTurn == "")
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var turn in turns)
        {
            if (string.IsNullOrWhiteSpace(turn.StrippedTurn) &&
                (!string.IsNullOrWhiteSpace(turn.Input) || !string.IsNullOrWhiteSpace(turn.Response)))
            {
                await StripAndSaveTurnAsync(turn, context, cancellationToken, modelOverride);
            }
        }
    }

    private static string BuildConversationHistory(List<Turn> turns, string userName, string personaName)
    {
        if (turns.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var turn in turns)
        {
            if (!string.IsNullOrWhiteSpace(turn.Input))
            {
                sb.AppendLine($"{userName}: {turn.Input}");
            }
            if (!string.IsNullOrWhiteSpace(turn.Response))
            {
                sb.AppendLine($"{personaName}: {turn.Response}");
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildTurnContent(Turn turn, string userName, string personaName)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(turn.Input))
        {
            sb.AppendLine($"{userName}: {turn.Input}");
        }

        if (!string.IsNullOrWhiteSpace(turn.Response))
        {
            sb.AppendLine($"{personaName}: {turn.Response}");
        }

        return sb.ToString().TrimEnd();
    }
}