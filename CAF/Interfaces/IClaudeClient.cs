namespace CAF.Interfaces;

public interface IClaudeClient
{
    Task<(bool success, string result)> GenerateContentAsync(
        ClaudeRequest request,
        CancellationToken cancellationToken = default,
        int? turnId = null);

    /// <summary>
    /// Count tokens in the given text using Claude's API
    /// </summary>
    /// <param name="text">Text to count tokens for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens, or -1 if the API call fails</returns>
    Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default);
}