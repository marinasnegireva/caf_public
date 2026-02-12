namespace CAF.Services.Conversation;

public class ConversationStateBuilder(
    ISystemMessageService systemMessageService,
    ISettingService settingService,
    IContextDataService contextDataService,
    ILogger<ConversationStateBuilder> logger) : IConversationContextBuilder
{
    public async Task<ConversationState> BuildContextAsync(
        Turn currentTurn,
        Session session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentTurn);
        ArgumentNullException.ThrowIfNull(session);

        var context = new ConversationState
        {
            CurrentTurn = currentTurn,
            Session = session,
            CancellationToken = cancellationToken
        };

        // Match legacy behavior: previous turns window size is controlled by setting (default 6)
        try
        {
            context.RecentTurnsCount = await settingService.GetIntAsync(SettingsKeys.PreviousTurnsCount, 6, cancellationToken);
            context.MaxDialogueLogTurns = await settingService.GetIntAsync(SettingsKeys.MaxDialogueLogTurns, 50, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read settings; using defaults (RecentTurnsCount=6, MaxDialogueLogTurns=50)");
            context.RecentTurnsCount = 6;
            context.MaxDialogueLogTurns = 50;
        }
        context.IsOOCRequest = currentTurn.Input.StartsWith("[ooc]", StringComparison.OrdinalIgnoreCase);

        // Get persona and user names
        await PopulatePersonaInfoAsync(context, cancellationToken);

        return context;
    }

    private async Task PopulatePersonaInfoAsync(ConversationState context, CancellationToken cancellationToken)
    {
        try
        {
            var activePersona = await systemMessageService.GetActivePersonaAsync(cancellationToken);
            if (activePersona != null)
            {
                context.Persona = activePersona;
                context.PersonaName = activePersona.Name;
            }
            var userProfile = await contextDataService.GetUserProfileAsync(cancellationToken);
            if (userProfile != null)
            {
                context.UserProfile = userProfile;
                context.UserName = userProfile.Name ?? "User";
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get active persona, using defaults");
        }
    }
}