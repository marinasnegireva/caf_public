namespace CAF.Services.VectorDB;

/// <summary>
/// Collection identifiers for vector databases
/// </summary>
public enum VectorCollection
{
    CAF_Quotes,
    CAF_CanonQuotes
}

/// <summary>
/// Manages batch retrieval from vector collections
/// </summary>
public class VectorCollectionManager(IGeminiClient geminiClient, IQdrantServiceFactory qdrantFactory) : IVectorCollectionManager
{
    /// <summary>
    /// Batch retrieves semantically similar entries for multiple queries
    /// </summary>
    public async Task<List<List<VectorEntry>>> RecallBatchAsync(
        VectorCollection vectorCollection,
        IReadOnlyList<string> queries,
        int k = 4,
        CancellationToken ct = default)
    {
        if (queries.Count == 0)
            return [];

        // Filter valid queries
        var validQueries = queries
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .ToList();

        if (validQueries.Count == 0)
            return [.. queries.Select(_ => new List<VectorEntry>())];

        // Batch embed all queries
        var embeddings = await geminiClient.EmbedBatchAsync(
            validQueries,
            cancellationToken: ct);

        // Search with each embedding using factory
        var qdrantService = qdrantFactory.CreateService(vectorCollection.ToString());

        var searchTasks = embeddings
            .Select(emb => qdrantService.SearchWithEmbeddingAsync(emb, k, ct))
            .ToArray();

        await Task.WhenAll(searchTasks);

        // Maintain original query order
        var results = new List<List<VectorEntry>>();
        var embeddingIndex = 0;

        foreach (var query in queries)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                results.Add([]);
            }
            else
            {
                results.Add(searchTasks[embeddingIndex++].Result);
            }
        }

        return results;
    }

    /// <summary>
    /// Batch retrieves semantically similar entries for multiple queries filtered by ProfileId
    /// </summary>
    public async Task<List<List<VectorEntry>>> RecallBatchAsync(
        VectorCollection vectorCollection,
        IReadOnlyList<string> queries,
        int profileId,
        int k = 4,
        CancellationToken ct = default)
    {
        if (queries.Count == 0)
            return [];

        // Filter valid queries
        var validQueries = queries
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .ToList();

        if (validQueries.Count == 0)
            return [.. queries.Select(_ => new List<VectorEntry>())];

        // Batch embed all queries
        var embeddings = await geminiClient.EmbedBatchAsync(
            validQueries,
            cancellationToken: ct);

        // Search with each embedding using factory and profile filter
        var qdrantService = qdrantFactory.CreateService(vectorCollection.ToString());

        var searchTasks = embeddings
            .Select(emb => qdrantService.SearchWithEmbeddingAsync(emb, profileId, k, ct))
            .ToArray();

        await Task.WhenAll(searchTasks);

        // Maintain original query order
        var results = new List<List<VectorEntry>>();
        var embeddingIndex = 0;

        foreach (var query in queries)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                results.Add([]);
            }
            else
            {
                results.Add(searchTasks[embeddingIndex++].Result);
            }
        }

        return results;
    }
}