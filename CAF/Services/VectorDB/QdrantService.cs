using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace CAF.Services.VectorDB;

/// <summary>
/// Service for interacting with Qdrant vector database
/// </summary>
public class QdrantService : IQdrantService, IAsyncDisposable
{
    private readonly string _collection;
    private readonly ulong _dimension = 3072;
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(string collection, IOptions<QdrantOptions> options, ILogger<QdrantService> logger)
    {
        var qdrantOptions = options.Value;
        _collection = collection;
        _client = new QdrantClient(qdrantOptions.Host, qdrantOptions.Port);
        _logger = logger;
    }

    /// <summary>
    /// Ensures collection exists (idempotent)
    /// </summary>
    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        try
        {
            await _client.CreateCollectionAsync(
                _collection,
                new VectorParams
                {
                    Size = _dimension,
                    Distance = Distance.Cosine
                },
                cancellationToken: ct);

            _logger.LogInformation("Created Qdrant collection '{Collection}'", _collection);
        }
        catch (Grpc.Core.RpcException ex)
            when (ex.StatusCode == Grpc.Core.StatusCode.AlreadyExists)
        {
            _logger.LogDebug("Qdrant collection '{Collection}' already exists", _collection);
        }
    }

    /// <summary>
    /// Batch upserts multiple embeddings
    /// </summary>
    public async Task UpsertChunksBatchAsync(
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
        CancellationToken ct = default)
    {
        if (chunks.Count == 0)
            return;

        var points = chunks.Select(c => new PointStruct
        {
            Id = new PointId { Num = (ulong)c.Id },
            Vectors = c.Embedding,
            Payload =
            {
                { "id", new Value { StringValue = c.PayloadId } },
                { "json", new Value { StringValue = c.Text } },
                { "entry_type", new Value { StringValue = c.EntryType } },
                { "session_Id", new Value { IntegerValue = c.SessionId ?? 0 } },
                { "speaker", new Value { StringValue = c.Speaker } },
                { "dbPK", new Value { IntegerValue = c.DbPK } },
                { "profile_id", new Value { IntegerValue = c.ProfileId } }
            }
        }).ToList();

        await _client.UpsertAsync(_collection, points, cancellationToken: ct);

        _logger.LogInformation("Upserted {Count} points to Qdrant collection '{Collection}'", points.Count, _collection);
    }

    /// <summary>
    /// Searches using a pre-computed embedding with MMR (Maximal Marginal Relevance) for diversity
    /// </summary>
    public async Task<List<VectorEntry>> SearchWithEmbeddingAsync(
        float[] embedding,
        int k = 8,
        CancellationToken ct = default)
    {
        var query = new Query
        {
            NearestWithMmr = new()
            {
                Nearest = new VectorInput(embedding),
                Mmr = new Mmr
                {
                    Diversity = 0.7f,
                    CandidatesLimit = (uint)(k * 10)
                }
            }
        };

        var results = await _client.QueryAsync(
            collectionName: _collection,
            query: query,
            limit: (ulong)k,
            cancellationToken: ct);

        return ParseResults(results);
    }

    /// <summary>
    /// Searches using a pre-computed embedding with ProfileId filter and MMR for diversity
    /// </summary>
    public async Task<List<VectorEntry>> SearchWithEmbeddingAsync(
        float[] embedding,
        int profileId,
        int k = 8,
        CancellationToken ct = default)
    {
        var query = new Query
        {
            NearestWithMmr = new()
            {
                Nearest = new VectorInput(embedding),
                Mmr = new Mmr
                {
                    Diversity = 0.7f,
                    CandidatesLimit = (uint)(k * 10)
                }
            }
        };

        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "profile_id",
                        Match = new Qdrant.Client.Grpc.Match { Integer = profileId }
                    }
                }
            }
        };

        var results = await _client.QueryAsync(
            collectionName: _collection,
            query: query,
            filter: filter,
            limit: (ulong)k,
            cancellationToken: ct);

        return ParseResults(results);
    }

    /// <summary>
    /// Deletes all points associated with a specific database primary key (ContextData.Id)
    /// </summary>
    public async Task DeleteByDbPKAsync(int dbPK, CancellationToken ct = default)
    {
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "dbPK",
                        Match = new Qdrant.Client.Grpc.Match { Integer = dbPK }
                    }
                }
            }
        };

        await _client.DeleteAsync(_collection, filter, cancellationToken: ct);

        _logger.LogInformation("Deleted points with dbPK {DbPK} from collection '{Collection}'", dbPK, _collection);
    }

    /// <summary>
    /// Batch deletes all points associated with multiple database primary keys
    /// </summary>
    public async Task DeleteByDbPKBatchAsync(IEnumerable<int> dbPKs, CancellationToken ct = default)
    {
        var dbPKList = dbPKs.ToList();
        if (dbPKList.Count == 0)
            return;

        foreach (var dbPK in dbPKList)
        {
            await DeleteByDbPKAsync(dbPK, ct);
        }

        _logger.LogInformation("Deleted {Count} entries from collection '{Collection}'", dbPKList.Count, _collection);
    }

    private static List<VectorEntry> ParseResults(IReadOnlyList<ScoredPoint> results)
    {
        var list = new List<VectorEntry>();
        foreach (var hit in results)
        {
            if (!hit.Payload.ContainsKey("id") || !hit.Payload.ContainsKey("json"))
                continue;

            var pid = hit.Payload["id"].StringValue;
            var json = hit.Payload["json"].StringValue;
            var session = hit.Payload.ContainsKey("session_Id")
                ? hit.Payload["session_Id"].IntegerValue : 0L;
            var entryType = hit.Payload.ContainsKey("entry_type")
                ? hit.Payload["entry_type"].StringValue : null;
            var dbPK = hit.Payload.ContainsKey("dbPK")
                ? hit.Payload["dbPK"].IntegerValue : 0L;
            var profileId = hit.Payload.ContainsKey("profile_id")
                ? (int)hit.Payload["profile_id"].IntegerValue : 0;

            list.Add(new VectorEntry(pid, hit.Score, session, entryType, json, dbPK, profileId));
        }

        return list;
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }
}

/// <summary>
/// Represents a vector search result from Qdrant
/// </summary>
/// <param name="PayloadId">Unique identifier for the payload</param>
/// <param name="Score">Similarity score from vector search</param>
/// <param name="Session">Session identifier for the quote</param>
/// <param name="EntryType">Type of entry (e.g., 'quote_full', 'canon_full')</param>
/// <param name="Json">JSON content of the vector entry</param>
/// <param name="DbPK">Database primary key reference</param>
/// <param name="ProfileId">Profile identifier for multi-tenancy</param>
public record struct VectorEntry(
    string PayloadId,
    float Score,
    long Session,
    string? EntryType,
    string Json,
    long DbPK,
    int ProfileId);