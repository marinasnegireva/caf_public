namespace CAF.Interfaces;

public interface IGeminiClient
{
    Task<(bool success, string result)> GenerateContentAsync(GeminiRequest request, bool technical = true, int? turnId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count tokens in the given text using Gemini's native token counting API
    /// </summary>
    /// <param name="text">Text to count tokens for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of tokens, or -1 if the API call fails</returns>
    Task<int> CountTokensAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embedding for a single text string
    /// </summary>
    /// <param name="text">Text to embed</param>
    /// <param name="taskType">Type of task (RETRIEVAL_QUERY, RETRIEVAL_DOCUMENT, etc.)</param>
    /// <param name="model">Model to use for embedding (default: text-embedding-004)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Embedding vector as float array</returns>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in a single API call
    /// </summary>
    /// <param name="texts">Texts to embed</param>
    /// <param name="taskType">Type of task (RETRIEVAL_QUERY, RETRIEVAL_DOCUMENT, etc.)</param>
    /// <param name="model">Model to use for embedding (default: text-embedding-004)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of embedding vectors</returns>
    Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit a batch of GenerateContent requests for asynchronous processing
    /// </summary>
    /// <param name="requests">List of requests with optional metadata</param>
    /// <param name="displayName">User-defined name for this batch</param>
    /// <param name="model">Model to use (optional, can use default)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operation representing the batch job</returns>
    Task<BatchOperation> BatchGenerateContentAsync(
        List<(GeminiRequest request, Dictionary<string, object> metadata)> requests,
        string displayName,
        string? model = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the status and results of a batch operation
    /// </summary>
    /// <param name="operationName">Name of the batch operation (e.g., "batches/{batchId}")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated operation with current status</returns>
    Task<BatchOperation> GetBatchOperationAsync(string operationName, CancellationToken cancellationToken = default);

    Task<(bool success, string result)> StreamGenerateContentAsync(GeminiRequest request, bool technical = true, int? turnId = null, CancellationToken cancellationToken = default);
}