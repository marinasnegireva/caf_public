namespace CAF.Interfaces;

public interface ISettingService
{
    Task<List<Setting>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Setting?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<Setting?> GetByNameAsync(SettingsKeys key, CancellationToken cancellationToken = default);

    Task<string?> GetValueAsync(SettingsKeys key, CancellationToken cancellationToken = default);

    Task<bool> GetBoolAsync(SettingsKeys key, bool defaultValue = false, CancellationToken cancellationToken = default);

    Task<int> GetIntAsync(SettingsKeys key, int defaultValue, CancellationToken cancellationToken = default);

    Task<Setting> CreateOrUpdateAsync(SettingsKeys key, string value, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task EnsureDefaultsInitializedAsync(CancellationToken cancellationToken = default);

    Task EnsureConversationDefaultsInitializedAsync(CancellationToken cancellationToken = default);

    Task EnsureQuoteDefaultsInitializedAsync(CancellationToken cancellationToken = default);

    Task EnsureContextTriggerDefaultsInitializedAsync(CancellationToken cancellationToken = default);

    Task EnsureSemanticDefaultsInitializedAsync(CancellationToken cancellationToken = default);
}