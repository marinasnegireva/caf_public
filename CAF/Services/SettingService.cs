namespace CAF.Services;

public class SettingService(IDbContextFactory<GeneralDbContext> dbContextFactory, ILogger<SettingService> logger, IProfileService profileService) : ISettingService
{
    private readonly int profileId = profileService.GetActiveProfileId();

    public async Task EnsureDefaultsInitializedAsync(CancellationToken cancellationToken = default)
    {
        await EnsureConversationDefaultsInitializedAsync(cancellationToken);
        await EnsureQuoteDefaultsInitializedAsync(cancellationToken);
        await EnsureContextTriggerDefaultsInitializedAsync(cancellationToken);
        await EnsureSemanticDefaultsInitializedAsync(cancellationToken);
        await CleanupInvalidSettingsAsync(cancellationToken);
    }

    public async Task CleanupInvalidSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // Get all valid setting keys from the enum
        var validKeys = Enum.GetValues<SettingsKeys>()
            .Select(k => k.ToKey())
            .ToHashSet();

        // Find settings that don't match any valid key
        var invalidSettings = await context.Settings
            .Where(s => s.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        var toRemove = invalidSettings
            .Where(s => !validKeys.Contains(s.Name))
            .ToList();

        if (toRemove.Count == 0)
        {
            logger.LogInformation("No invalid settings found to cleanup");
            return;
        }

        context.Settings.RemoveRange(toRemove);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Cleaned up {Count} invalid settings: {Settings}", 
            toRemove.Count, 
            string.Join(", ", toRemove.Select(s => s.Name)));
    }

    public async Task EnsureConversationDefaultsInitializedAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.PreviousTurnsCount.ToKey()] = "6",
            [SettingsKeys.MaxDialogueLogTurns.ToKey()] = "50",
            [SettingsKeys.PerceptionEnabled.ToKey()] = "true"
        };

        await InitializeDefaultsAsync(defaults, "conversation", cancellationToken);
    }

    public async Task EnsureQuoteDefaultsInitializedAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.QuotesMaxLength.ToKey()] = "2000",
            [SettingsKeys.QuoteCanonMaxLength.ToKey()] = "500",
            [SettingsKeys.QuoteUseLLMQueryTransformation.ToKey()] = "false"
        };

        await InitializeDefaultsAsync(defaults, "quote", cancellationToken);
    }

    public async Task EnsureContextTriggerDefaultsInitializedAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.TriggerScanTextAdditionalWords.ToKey()] = ""
        };

        await InitializeDefaultsAsync(defaults, "context trigger", cancellationToken);
    }

    public async Task EnsureSemanticDefaultsInitializedAsync(CancellationToken cancellationToken = default)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.SemanticTokenQuota_Quote.ToKey()] = "1000",
            [SettingsKeys.SemanticTokenQuota_Memory.ToKey()] = "1000",
            [SettingsKeys.SemanticTokenQuota_Insight.ToKey()] = "1000",
            [SettingsKeys.SemanticTokenQuota_PersonaVoiceSample.ToKey()] = "1000",
            [SettingsKeys.SemanticUseLLMQueryTransformation.ToKey()] = "false"
        };

        await InitializeDefaultsAsync(defaults, "semantic", cancellationToken);
    }

    private async Task InitializeDefaultsAsync(Dictionary<string, string> defaults, string category, CancellationToken cancellationToken)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingKeys = await context.Settings
            .Where(s => s.ProfileId == profileId)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var toAdd = defaults
            .Where(kvp => !existingKeys.Contains(kvp.Key))
            .Select(kvp => new Setting
            {
                Name = kvp.Key,
                Value = kvp.Value,
                CreatedAt = now,
                ProfileId = profileId
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        context.Settings.AddRange(toAdd);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Initialized {Count} default {Category} settings.", toAdd.Count, category);
    }

    public async Task<bool> GetBoolAsync(SettingsKeys key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public async Task<int> GetIntAsync(SettingsKeys key, int defaultValue, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public async Task<List<Setting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Settings
            .Where(s => s.ProfileId == profileId)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Setting?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Settings.FindAsync([id], cancellationToken);
    }

    public async Task<Setting?> GetByNameAsync(SettingsKeys key, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Settings
            .FirstOrDefaultAsync(s => s.Name == key.ToKey() && s.ProfileId == profileId, cancellationToken);
    }

    public async Task<string?> GetValueAsync(SettingsKeys key, CancellationToken cancellationToken = default)
    {
        var setting = await GetByNameAsync(key, cancellationToken);
        return setting?.Value;
    }

    public async Task<Setting> CreateOrUpdateAsync(SettingsKeys key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await GetByNameAsync(key, cancellationToken);

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (setting == null)
        {
            setting = new Setting
            {
                Name = key.ToKey(),
                Value = value ?? string.Empty,
                CreatedAt = DateTime.UtcNow,
                ProfileId = profileId
            };

            context.Settings.Add(setting);
            logger.LogInformation("Created setting {Name} with value: {Value}", setting.Name, setting.Value);
        }
        else
        {
            context.Settings.Attach(setting);
            setting.Value = value ?? string.Empty;
            setting.ModifiedAt = DateTime.UtcNow;
            setting.ProfileId = profileId;
            logger.LogInformation("Updated setting {Name} with value: {Value}", setting.Name, setting.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        return setting;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var setting = await GetByIdAsync(id, cancellationToken);
        if (setting == null)
        {
            return false;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        context.Settings.Remove(setting);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted setting {Id} ({Name})", setting.Id, setting.Name);

        return true;
    }
}

public enum SettingsKeys
{
    PreviousTurnsCount,
    MaxDialogueLogTurns,
    ActivePersonaId,
    LLMProvider,
    GeminiModel,
    ClaudeModel,
    DeepSeekModel,
    MemoryAnalysisAdditionalContext,
    PerceptionEnabled,
    QuotesMaxLength,
    QuoteCanonMaxLength,
    QuoteUseLLMQueryTransformation,
    TriggerScanTextAdditionalWords,
    SemanticTokenQuota_Quote,
    SemanticTokenQuota_Memory,
    SemanticTokenQuota_Insight,
    SemanticTokenQuota_PersonaVoiceSample,
    SemanticUseLLMQueryTransformation
}

public static class SettingsKeysExtensions
{
    public static string ToKey(this SettingsKeys key) => key.ToString();

    public static SettingsKeys? FromKey(string key)
    {
        return Enum.TryParse<SettingsKeys>(key, out var result) ? result : null;
    }
}