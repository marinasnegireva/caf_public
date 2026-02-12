namespace CAF.Services.Conversation;

/// <summary>
/// Factory for selecting and creating LLM provider strategies
/// </summary>
public class LLMProviderFactory(
    IEnumerable<ILLMProviderStrategy> strategies,
    ISettingService settingService,
    ILogger<LLMProviderFactory> logger) : ILLMProviderFactory
{
    private readonly Dictionary<string, ILLMProviderStrategy> _strategyMap = strategies
        .ToDictionary(s => s.ProviderName, s => s, StringComparer.OrdinalIgnoreCase);

    public async Task<ILLMProviderStrategy> GetProviderAsync(CancellationToken cancellationToken = default)
    {
        var providerName = await settingService.GetValueAsync(SettingsKeys.LLMProvider, cancellationToken)
            ?? ConversationConstants.DefaultProvider;

        logger.LogInformation("Selected LLM provider: {Provider}", providerName);

        return GetProvider(providerName);
    }

    public ILLMProviderStrategy GetProvider(string providerName)
    {
        if (_strategyMap.TryGetValue(providerName, out var strategy))
        {
            return strategy;
        }

        logger.LogWarning("Provider {Provider} not found, falling back to {Default}",
            providerName, ConversationConstants.DefaultProvider);

        return _strategyMap.TryGetValue(ConversationConstants.DefaultProvider, out var defaultStrategy)
            ? defaultStrategy
            : throw new InvalidOperationException(
            $"No LLM provider strategy registered for '{providerName}' and default provider '{ConversationConstants.DefaultProvider}' is also unavailable.");
    }
}