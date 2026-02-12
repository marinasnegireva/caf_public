namespace CAF.Services.Conversation.Enrichment.Enrichers;

/// <summary>
/// Enriches conversation state with CharacterProfile data.
/// Supports: AlwaysOn, Manual, Trigger
/// Does NOT support: Semantic
///
/// Special handling: User profile (IsUser=true) is always loaded into state.UserProfile
/// </summary>
public class CharacterProfileEnricher(
IContextDataService contextDataService,
IDbContextFactory<GeneralDbContext> dbContextFactory,
ILogger<CharacterProfileEnricher> logger)
: DataTypeEnricherBase<CharacterProfileEnricher>(contextDataService, dbContextFactory, logger)
{
    protected override DataType DataType => DataType.CharacterProfile;
    protected override string EnricherName => nameof(CharacterProfileEnricher);
    protected override bool SupportsManual => true;
    protected override bool SupportsSemantic => false;

    public override async Task EnrichAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        if (state?.Session == null || state.CurrentTurn == null)
        {
            Logger.LogDebug("{Enricher} skipped: no session or current turn available", EnricherName);
            return;
        }

        try
        {
            Logger.LogDebug("{Enricher}: Starting enrichment. State.UserProfile is currently {IsNull}",
                EnricherName, state.UserProfile == null ? "NULL" : "NOT NULL");

            // First, load the user profile (always loaded, special handling)
            var userProfile = await ContextDataService.GetUserProfileAsync(cancellationToken);
            if (userProfile != null)
            {
                state.UserProfile = userProfile;
                state.UserName = userProfile.Name;
                Logger.LogInformation("{Enricher}: ✅ SUCCESSFULLY loaded user profile '{Name}' (Id: {Id}, ProfileId: {ProfileId}, IsUser: {IsUser}). State.UserProfile is now {IsNull}",
                    EnricherName, userProfile.Name, userProfile.Id, userProfile.ProfileId, userProfile.IsUser,
                    state.UserProfile == null ? "NULL (FAILED!)" : "NOT NULL (SUCCESS!)");
            }
            else
            {
                Logger.LogWarning("{Enricher}: ❌ No user profile found! Check that a CharacterProfile with IsUser=true exists for the active profile.", EnricherName);
            }

            // Then call base implementation to load other character profiles
            // via AlwaysOn, Manual, and Trigger mechanisms
            await base.EnrichAsync(state, cancellationToken);

            Logger.LogDebug("{Enricher}: Completed. State.UserProfile final status: {IsNull}, State.CharacterProfiles count: {Count}",
                EnricherName, state.UserProfile == null ? "NULL" : $"NOT NULL (Name: {state.UserProfile.Name})",
                state.CharacterProfiles.Count);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{Enricher} failed for session {SessionId}", EnricherName, state.Session?.Id);
        }
    }
}