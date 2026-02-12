namespace CAF.Interfaces;

/// <summary>
/// Factory for creating LLM provider strategies
/// </summary>
public interface ILLMProviderFactory
{
    /// <summary>
    /// Gets the appropriate LLM provider strategy based on current settings
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The selected LLM provider strategy</returns>
    Task<ILLMProviderStrategy> GetProviderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific LLM provider strategy by name
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "Gemini", "Claude")</param>
    /// <returns>The specified LLM provider strategy</returns>
    ILLMProviderStrategy GetProvider(string providerName);
}