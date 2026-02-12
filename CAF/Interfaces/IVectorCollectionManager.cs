namespace CAF.Interfaces;

/// <summary>
/// Interface for managing batch retrieval from vector collections
/// </summary>
public interface IVectorCollectionManager
{
    /// <summary>
    /// Batch retrieves semantically similar entries for multiple queries
    /// </summary>
    Task<List<List<VectorEntry>>> RecallBatchAsync(
        VectorCollection vectorCollection,
        IReadOnlyList<string> queries,
        int k = 4,
        CancellationToken ct = default);

    /// <summary>
    /// Batch retrieves semantically similar entries for multiple queries filtered by ProfileId
    /// </summary>
    Task<List<List<VectorEntry>>> RecallBatchAsync(
        VectorCollection vectorCollection,
        IReadOnlyList<string> queries,
        int profileId,
        int k = 4,
        CancellationToken ct = default);
}