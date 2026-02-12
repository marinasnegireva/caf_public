namespace CAF.Interfaces;

/// <summary>
/// Interface for Qdrant vector database operations
/// </summary>
public interface IQdrantService
{
    /// <summary>
    /// Ensures collection exists (idempotent)
    /// </summary>
    Task EnsureCollectionAsync(CancellationToken ct = default);

    /// <summary>
    /// Batch upserts multiple embeddings
    /// </summary>
    Task UpsertChunksBatchAsync(
        IReadOnlyList<(
            int Id,
            float[] Embedding,
            string PayloadId,
            string Text,
            long? SessionId,
            string EntryType,
            string Speaker,
            int DbPK,
            int ProfileId
        )> chunks,
        CancellationToken ct = default);

    /// <summary>
    /// Searches using a pre-computed embedding with MMR (Maximal Marginal Relevance) for diversity
    /// </summary>
    Task<List<VectorEntry>> SearchWithEmbeddingAsync(
        float[] embedding,
        int k = 8,
        CancellationToken ct = default);

    /// <summary>
    /// Searches using a pre-computed embedding with ProfileId filter and MMR for diversity
    /// </summary>
    Task<List<VectorEntry>> SearchWithEmbeddingAsync(
        float[] embedding,
        int profileId,
        int k = 8,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes all points associated with a specific database primary key (ContextData.Id)
    /// </summary>
    Task DeleteByDbPKAsync(int dbPK, CancellationToken ct = default);

    /// <summary>
    /// Batch deletes all points associated with multiple database primary keys
    /// </summary>
    Task DeleteByDbPKBatchAsync(IEnumerable<int> dbPKs, CancellationToken ct = default);
}