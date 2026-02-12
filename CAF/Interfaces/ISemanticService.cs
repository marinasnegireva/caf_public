using CAF.Services.Conversation;

namespace CAF.Interfaces;

/// <summary>
/// Service for semantic search and embedding operations on ContextData entries.
/// Supports Quote, Memory, Insight, and PersonaVoiceSample types with separate vector collections.
/// </summary>
public interface ISemanticService
{
    #region Embedding Operations

    /// <summary>
    /// Embeds a single ContextData entry into its type-specific collection
    /// </summary>
    Task EmbedAsync(ContextData data, CancellationToken ct = default);

    /// <summary>
    /// Embeds multiple ContextData entries (grouped by type automatically)
    /// </summary>
    Task EmbedBatchAsync(IEnumerable<ContextData> data, CancellationToken ct = default);

    /// <summary>
    /// Batch indexes multiple context data items with multi-chunk embedding strategy.
    /// Similar to EmbedBatchAsync but optimized for bulk operations.
    /// </summary>
    /// <param name="items">Context data items to index</param>
    /// <param name="embeddingBatchSize">Number of chunks to embed per API call</param>
    /// <param name="ct">Cancellation token</param>
    Task IndexContextDataBatchAsync(
        IEnumerable<ContextData> items,
        int embeddingBatchSize = 96,
        CancellationToken ct = default);

    /// <summary>
    /// Removes embedding for a ContextData entry from its collection
    /// </summary>
    Task UnembedAsync(ContextData data, CancellationToken ct = default);

    /// <summary>
    /// Removes embeddings for multiple ContextData entries
    /// </summary>
    Task UnembedBatchAsync(IEnumerable<ContextData> data, CancellationToken ct = default);

    /// <summary>
    /// Re-embeds a ContextData entry (updates the embedding)
    /// </summary>
    Task ReembedAsync(ContextData data, CancellationToken ct = default);

    /// <summary>
    /// Syncs all ContextData entries with Semantic availability - embeds those not in vector DB
    /// </summary>
    Task SyncAllAsync(int profileId, CancellationToken ct = default);

    #endregion Embedding Operations

    #region Search Operations

    /// <summary>
    /// Searches for semantically similar ContextData by query text
    /// </summary>
    /// <param name="query">Search query text</param>
    /// <param name="profileId">Profile to search within</param>
    /// <param name="type">Optional: filter by data type</param>
    /// <param name="limit">Maximum results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of ContextData entries ordered by relevance</returns>
    Task<List<ContextData>> SearchAsync(
        string query,
        int profileId,
        DataType? type = null,
        int limit = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Searches using pre-computed embedding vector
    /// </summary>
    Task<List<ContextData>> SearchWithEmbeddingAsync(
        float[] embedding,
        int profileId,
        DataType? type = null,
        int limit = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Searches across multiple types with per-type limits
    /// </summary>
    Task<Dictionary<DataType, List<ContextData>>> SearchMultiTypeAsync(
        string query,
        int profileId,
        Dictionary<DataType, int> typeLimits,
        CancellationToken ct = default);

    /// <summary>
    /// Searches across multiple types using LLM-generated queries for better semantic matching.
    /// Uses the conversation state to generate contextually relevant search queries.
    /// </summary>
    /// <param name="state">Conversation state containing current input and context</param>
    /// <param name="profileId">Profile to search within</param>
    /// <param name="typeLimits">Per-type result limits</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary of results by type, with deduplication and score aggregation</returns>
    Task<Dictionary<DataType, List<ContextData>>> SearchWithQueryTransformationAsync(
        ConversationState state,
        int profileId,
        Dictionary<DataType, int> typeLimits,
        CancellationToken ct = default);

    #endregion Search Operations

    #region Collection Management

    /// <summary>
    /// Ensures all type-specific collections exist in Qdrant
    /// </summary>
    Task EnsureCollectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the collection name for a specific data type
    /// </summary>
    string GetCollectionName(DataType type);

    /// <summary>
    /// Gets statistics about embeddings for a profile
    /// </summary>
    Task<SemanticStats> GetStatsAsync(int profileId, CancellationToken ct = default);

    #endregion Collection Management
}

/// <summary>
/// Statistics about semantic embeddings
/// </summary>
public record SemanticStats(
    int TotalEmbedded,
    int TotalPending,
    Dictionary<DataType, int> EmbeddedByType,
    Dictionary<DataType, int> PendingByType);