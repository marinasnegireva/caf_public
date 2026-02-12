using CAF.Services.Conversation;

namespace CAF.Services;

/// <summary>
/// Service for semantic search and embedding operations on ContextData entries.
/// Uses separate Qdrant collections per DataType for optimal retrieval.
/// </summary>
public partial class SemanticService(
    IGeminiClient geminiClient,
    IQdrantServiceFactory qdrantFactory,
    IDbContextFactory<GeneralDbContext> dbContextFactory,
    ILogger<SemanticService> logger) : ISemanticService
{
    private const int EmbeddingBatchSize = 96;
    private const int RateLimitDelayMs = 200;

    // Collection name prefixes for each semantic-eligible type
    private static readonly Dictionary<DataType, string> CollectionPrefixes = new()
    {
        { DataType.Quote, "context_quotes" },
        { DataType.Memory, "context_memories" },
        { DataType.Insight, "context_insights" },
        { DataType.PersonaVoiceSample, "context_voice_samples" }
    };

    // Types that support semantic search
    private static readonly HashSet<DataType> SemanticEligibleTypes =
    [
        DataType.Quote,
        DataType.Memory,
        DataType.Insight,
        DataType.PersonaVoiceSample
    ];

    #region Embedding Operations

    public async Task EmbedAsync(ContextData data, CancellationToken ct = default)
    {
        if (!IsSemanticEligible(data))
        {
            logger.LogWarning("ContextData {Id} type {Type} is not eligible for semantic embedding", data.Id, data.Type);
            return;
        }

        var collectionName = GetCollectionName(data.Type);
        var qdrant = qdrantFactory.CreateService(collectionName);
        await qdrant.EnsureCollectionAsync(ct);

        var chunks = GenerateChunks(data);
        var texts = chunks.Select(c => c.Text).ToList();

        // Get embeddings from Gemini
        var embeddings = await geminiClient.EmbedBatchAsync(texts, cancellationToken: ct);

        // Prepare for Qdrant
        var chunksWithEmbeddings = chunks.Select((chunk, idx) => (
            chunk.Id,
            Embedding: embeddings[idx],
            chunk.PayloadId,
            chunk.Text,
            SessionId: (long?)(chunk.SourceSessionId ?? 0),
            EntryType: $"{data.Type.ToString().ToLowerInvariant()}_{chunk.ChunkType}",
            chunk.Speaker,
            DbPK: chunk.DataId,
            chunk.ProfileId
        )).ToList();

        await qdrant.UpsertChunksBatchAsync(chunksWithEmbeddings, ct);

        // Update database
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ContextData.FindAsync([data.Id], ct);
        if (entity != null)
        {
            entity.InVectorDb = true;
            entity.VectorId = chunks.First().PayloadId;
            entity.EmbeddingUpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Embedded ContextData {Id} ({Type}) with {ChunkCount} chunks",
            data.Id, data.Type, chunks.Count);
    }

    public async Task EmbedBatchAsync(IEnumerable<ContextData> data, CancellationToken ct = default)
    {
        var dataList = data.Where(IsSemanticEligible).ToList();
        if (dataList.Count == 0)
            return;

        // Group by type for efficient collection handling
        var byType = dataList.GroupBy(d => d.Type);

        foreach (var group in byType)
        {
            var collectionName = GetCollectionName(group.Key);
            var qdrant = qdrantFactory.CreateService(collectionName);
            await qdrant.EnsureCollectionAsync(ct);

            var totalProcessed = 0;
            var items = group.ToList();

            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();

                var chunks = GenerateChunks(item);
                var texts = chunks.Select(c => c.Text).ToList();

                var embeddings = await geminiClient.EmbedBatchAsync(texts, cancellationToken: ct);

                var chunksWithEmbeddings = chunks.Select((chunk, idx) => (
                    chunk.Id,
                    Embedding: embeddings[idx],
                    chunk.PayloadId,
                    chunk.Text,
                    SessionId: (long?)(chunk.SourceSessionId ?? 0),
                    EntryType: $"{item.Type.ToString().ToLowerInvariant()}_{chunk.ChunkType}",
                    chunk.Speaker,
                    DbPK: chunk.DataId,
                    chunk.ProfileId
                )).ToList();

                await qdrant.UpsertChunksBatchAsync(chunksWithEmbeddings, ct);

                // Update database
                await using var db = await dbContextFactory.CreateDbContextAsync(ct);
                var entity = await db.ContextData.FindAsync([item.Id], ct);
                if (entity != null)
                {
                    entity.InVectorDb = true;
                    entity.VectorId = chunks.First().PayloadId;
                    entity.EmbeddingUpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }

                totalProcessed++;

                if (totalProcessed % 20 == 0)
                {
                    logger.LogInformation("Embedded {Current}/{Total} {Type} entries",
                        totalProcessed, items.Count, group.Key);
                }

                await Task.Delay(RateLimitDelayMs, ct);
            }

            logger.LogInformation("Completed embedding {Total} {Type} entries", totalProcessed, group.Key);
        }
    }

    /// <summary>
    /// Batch indexes multiple context data items with multi-chunk embedding strategy.
    /// Similar to EmbedBatchAsync but optimized for bulk operations.
    /// </summary>
    /// <param name="items">Context data items to index</param>
    /// <param name="embeddingBatchSize">Number of chunks to embed per API call (currently unused, processes one at a time)</param>
    /// <param name="ct">Cancellation token</param>
    public async Task IndexContextDataBatchAsync(
        IEnumerable<ContextData> items,
        int embeddingBatchSize = 96,
        CancellationToken ct = default)
    {
        var itemList = items.Where(IsSemanticEligible).ToList();
        if (itemList.Count == 0)
            return;

        logger.LogInformation("Indexing {ItemCount} context data items with true bulk processing", itemList.Count);

        // Group by type for efficient collection handling
        var byType = itemList.GroupBy(d => d.Type);

        foreach (var group in byType)
        {
            var collectionName = GetCollectionName(group.Key);
            var qdrant = qdrantFactory.CreateService(collectionName);
            await qdrant.EnsureCollectionAsync(ct);

            var typeItems = group.ToList();
            var totalProcessed = 0;

            // Generate all chunks upfront
            var itemChunks = typeItems.Select(item => new
            {
                Item = item,
                Chunks = GenerateChunks(item)
            }).ToList();

            // Flatten all chunks with their parent items
            var allChunksWithItems = itemChunks
                .SelectMany(ic => ic.Chunks.Select(chunk => new { ic.Item, Chunk = chunk }))
                .ToList();

            // Process chunks in batches
            for (var offset = 0; offset < allChunksWithItems.Count; offset += embeddingBatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var batch = allChunksWithItems.Skip(offset).Take(embeddingBatchSize).ToList();
                var texts = batch.Select(x => x.Chunk.Text).ToList();

                // Single embedding call for multiple items' chunks
                var embeddings = await geminiClient.EmbedBatchAsync(texts, cancellationToken: ct);

                // Prepare for Qdrant
                var chunksWithEmbeddings = batch.Select((x, idx) => (
                    x.Chunk.Id,
                    Embedding: embeddings[idx],
                    x.Chunk.PayloadId,
                    x.Chunk.Text,
                    SessionId: (long?)(x.Chunk.SourceSessionId ?? 0),
                    EntryType: $"{x.Item.Type.ToString().ToLowerInvariant()}_{x.Chunk.ChunkType}",
                    x.Chunk.Speaker,
                    DbPK: x.Chunk.DataId,
                    x.Chunk.ProfileId
                )).ToList();

                await qdrant.UpsertChunksBatchAsync(chunksWithEmbeddings, ct);

                // Track which items were processed in this batch and update DB
                var processedItemIds = batch.Select(x => x.Item.Id).Distinct().ToHashSet();

                await using var db = await dbContextFactory.CreateDbContextAsync(ct);
                var entities = await db.ContextData
                    .Where(d => processedItemIds.Contains(d.Id))
                    .ToListAsync(ct);

                foreach (var entity in entities)
                {
                    if (!entity.InVectorDb) // Only update if not already marked
                    {
                        var itemChunkData = itemChunks.First(ic => ic.Item.Id == entity.Id);
                        entity.InVectorDb = true;
                        entity.VectorId = itemChunkData.Chunks.First().PayloadId;
                        entity.EmbeddingUpdatedAt = DateTime.UtcNow;
                        totalProcessed++;
                    }
                }

                await db.SaveChangesAsync(ct);

                if (totalProcessed % 20 == 0 && totalProcessed > 0)
                {
                    logger.LogInformation("Indexed {Current}/{Total} {Type} entries", totalProcessed, typeItems.Count, group.Key);
                }

                await Task.Delay(RateLimitDelayMs, ct);
            }

            logger.LogInformation("Completed indexing {Total} {Type} entries", totalProcessed, group.Key);
        }
    }

    public async Task UnembedAsync(ContextData data, CancellationToken ct = default)
    {
        if (!SemanticEligibleTypes.Contains(data.Type))
            return;

        var collectionName = GetCollectionName(data.Type);
        var qdrant = qdrantFactory.CreateService(collectionName);

        try
        {
            // Delete from Qdrant
            await qdrant.DeleteByDbPKAsync(data.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete ContextData {Id} from Qdrant collection {Collection}", 
                data.Id, collectionName);
        }

        // Update database
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entity = await db.ContextData.FindAsync([data.Id], ct);
        if (entity != null)
        {
            entity.InVectorDb = false;
            entity.VectorId = null;
            entity.EmbeddingUpdatedAt = null;
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Unembedded ContextData {Id}", data.Id);
    }

    public async Task UnembedBatchAsync(IEnumerable<ContextData> data, CancellationToken ct = default)
    {
        var dataList = data.Where(d => SemanticEligibleTypes.Contains(d.Type)).ToList();
        if (dataList.Count == 0)
            return;

        // Group by type for efficient collection handling
        var byType = dataList.GroupBy(d => d.Type);

        foreach (var group in byType)
        {
            var collectionName = GetCollectionName(group.Key);
            var qdrant = qdrantFactory.CreateService(collectionName);

            var ids = group.Select(d => d.Id).ToList();

            try
            {
                // Delete from Qdrant
                await qdrant.DeleteByDbPKBatchAsync(ids, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete batch from Qdrant collection {Collection}", collectionName);
            }
        }

        // Update database
        var allIds = dataList.Select(d => d.Id).ToList();
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var entities = await db.ContextData
            .Where(d => allIds.Contains(d.Id))
            .ToListAsync(ct);

        foreach (var entity in entities)
        {
            entity.InVectorDb = false;
            entity.VectorId = null;
            entity.EmbeddingUpdatedAt = null;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Unembedded {Count} entries", entities.Count);
    }

    public async Task ReembedAsync(ContextData data, CancellationToken ct = default)
    {
        // Simply re-embed - the upsert will overwrite existing vectors
        await EmbedAsync(data, ct);
    }

    public async Task SyncAllAsync(int profileId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        // Find all semantic-eligible entries not yet embedded
        var pendingEntries = await db.ContextData
            .Where(d => d.ProfileId == profileId &&
                       d.Availability == AvailabilityType.Semantic &&
                       d.IsEnabled &&
                       !d.IsArchived &&
                       !d.InVectorDb &&
                       (d.Type == DataType.Quote ||
                        d.Type == DataType.Memory ||
                        d.Type == DataType.Insight ||
                        d.Type == DataType.PersonaVoiceSample))
            .ToListAsync(ct);

        if (pendingEntries.Count == 0)
        {
            logger.LogInformation("No pending entries to embed for profile {ProfileId}", profileId);
            return;
        }

        logger.LogInformation("Syncing {Count} pending entries for profile {ProfileId}",
            pendingEntries.Count, profileId);

        await EmbedBatchAsync(pendingEntries, ct);
    }

    #endregion Embedding Operations

    #region Search Operations

    public async Task<List<ContextData>> SearchAsync(
        string query,
        int profileId,
        DataType? type = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        // Get embedding for query
        var embeddings = await geminiClient.EmbedBatchAsync([query], cancellationToken: ct);
        var queryEmbedding = embeddings[0];

        return await SearchWithEmbeddingAsync(queryEmbedding, profileId, type, limit, ct);
    }

    public async Task<List<ContextData>> SearchWithEmbeddingAsync(
        float[] embedding,
        int profileId,
        DataType? type = null,
        int limit = 10,
        CancellationToken ct = default)
    {
        var results = new List<(int DataId, float Score)>();

        // Determine which collections to search
        var typesToSearch = type.HasValue
            ? [type.Value]
            : SemanticEligibleTypes.ToList();

        foreach (var searchType in typesToSearch)
        {
            if (!SemanticEligibleTypes.Contains(searchType))
                continue;

            var collectionName = GetCollectionName(searchType);
            var qdrant = qdrantFactory.CreateService(collectionName);

            try
            {
                var searchResults = await qdrant.SearchWithEmbeddingAsync(
                    embedding,
                    profileId,
                    limit,
                    ct);

                foreach (var result in searchResults)
                {
                    results.Add(((int)result.DbPK, result.Score));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Search failed for collection {Collection}", collectionName);
            }
        }

        if (results.Count == 0)
            return [];

        // Get unique data IDs ordered by score
        var dataIds = results
            .GroupBy(r => r.DataId)
            .Select(g => (DataId: g.Key, Score: g.Max(x => x.Score)))
            .OrderByDescending(x => x.Score)
            .Take(limit)
            .Select(x => x.DataId)
            .ToList();

        // Fetch from database
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var dataEntries = await db.ContextData
            .Where(d => dataIds.Contains(d.Id))
            .ToListAsync(ct);

        // Preserve score ordering
        var orderedResults = dataIds
            .Select(id => dataEntries.FirstOrDefault(d => d.Id == id))
            .Where(d => d != null)
            .Cast<ContextData>()
            .ToList();

        // Set ProcessWeight from scores
        var scoreMap = results.GroupBy(r => r.DataId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.Score));

        foreach (var entry in orderedResults)
        {
            if (scoreMap.TryGetValue(entry.Id, out var score))
            {
                entry.ProcessWeight = (decimal)score;
            }
        }

        return orderedResults;
    }

    public async Task<Dictionary<DataType, List<ContextData>>> SearchMultiTypeAsync(
        string query,
        int profileId,
        Dictionary<DataType, int> typeLimits,
        CancellationToken ct = default)
    {
        // Get embedding for query
        var embeddings = await geminiClient.EmbedBatchAsync([query], cancellationToken: ct);
        var queryEmbedding = embeddings[0];

        var results = new Dictionary<DataType, List<ContextData>>();

        foreach (var (dataType, limit) in typeLimits)
        {
            if (limit <= 0 || !SemanticEligibleTypes.Contains(dataType))
                continue;

            var typeResults = await SearchWithEmbeddingAsync(
                queryEmbedding,
                profileId,
                dataType,
                limit,
                ct);

            results[dataType] = typeResults;
        }

        return results;
    }

    public async Task<Dictionary<DataType, List<ContextData>>> SearchWithQueryTransformationAsync(
        ConversationState state,
        int profileId,
        Dictionary<DataType, int> typeLimits,
        CancellationToken ct = default)
    {
        // Generate multiple semantic queries using LLM
        var queries = await GenerateSemanticQueriesAsync(state, ct);

        if (queries.Length == 0)
        {
            logger.LogDebug("Query transformation returned no queries, using direct input");
            return await SearchMultiTypeAsync(state.CurrentTurn?.Input ?? "", profileId, typeLimits, ct);
        }

        logger.LogDebug("Generated {Count} semantic queries for search", queries.Length);

        // Get embeddings for all queries in a single batch call
        var embeddings = await geminiClient.EmbedBatchAsync([.. queries], cancellationToken: ct);

        // Aggregate results across all queries with score tracking
        var aggregatedResults = new Dictionary<DataType, Dictionary<int, (ContextData Data, float MaxScore)>>();

        foreach (var (dataType, limit) in typeLimits)
        {
            if (limit <= 0 || !SemanticEligibleTypes.Contains(dataType))
                continue;

            aggregatedResults[dataType] = [];

            // Search with each query embedding
            for (var i = 0; i < embeddings.Count; i++)
            {
                var queryResults = await SearchWithEmbeddingAsync(
                    embeddings[i],
                    profileId,
                    dataType,
                    limit,
                    ct);

                // Aggregate results, keeping maximum score per data entry
                foreach (var result in queryResults)
                {
                    if (!aggregatedResults[dataType].TryGetValue(result.Id, out var existing) ||
                        (float)result.ProcessWeight > existing.MaxScore)
                    {
                        aggregatedResults[dataType][result.Id] = (result, (float)result.ProcessWeight);
                    }
                }
            }
        }

        // Convert to final results, ordered by score and limited
        var results = new Dictionary<DataType, List<ContextData>>();

        foreach (var (dataType, dataMap) in aggregatedResults)
        {
            var limit = typeLimits[dataType];
            var orderedList = dataMap.Values
                .OrderByDescending(x => x.MaxScore)
                .Take(limit)
                .Select(x =>
                {
                    x.Data.ProcessWeight = (decimal)x.MaxScore;
                    return x.Data;
                })
                .ToList();

            results[dataType] = orderedList;
        }

        var totalResults = results.Values.Sum(l => l.Count);
        logger.LogInformation("Multi-query search found {Total} results across {Types} types",
            totalResults, results.Count);

        return results;
    }

    /// <summary>
    /// Generates multiple semantic search queries from the conversation state using LLM.
    /// Returns 6 queries capturing different facets: self-reflection, observation, narrative, dialogue, and metaphor.
    /// </summary>
    private async Task<string[]> GenerateSemanticQueriesAsync(ConversationState state, CancellationToken ct)
    {
        if (state.CurrentTurn == null || string.IsNullOrWhiteSpace(state.CurrentTurn.Input))
            return [];

        try
        {
            // Build context for query transformation
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(state.PreviousResponse))
                sb.AppendLine($"{state.PersonaName[0]}: {state.PreviousResponse}");
            sb.AppendLine($"{state.UserName[0]}: {state.CurrentTurn.Input}");

            var request = new GeminiMessageBuilder()
                .WithSystemInstruction(DefaultTechnicalMessages.QuoteQueryTransformer)
                .AddUserMessage(sb.ToString())
                .Build();

            var (success, response) = await geminiClient.GenerateContentAsync(request, technical: true, cancellationToken: ct);

            if (!success)
            {
                logger.LogWarning("LLM query transformation failed, falling back to direct search");
                return [state.CurrentTurn.Input];
            }

            // Parse JSON array from response
            var jsonMatch = MyRegex().Match(response);
            if (jsonMatch.Success)
            {
                var queries = JsonSerializer.Deserialize<string[]>(jsonMatch.Value);
                if (queries != null && queries.Length > 0)
                {
                    logger.LogDebug("Generated {Count} semantic queries via LLM", queries.Length);
                    return queries;
                }
            }

            logger.LogWarning("Failed to parse LLM response for query transformation");
            return [state.CurrentTurn.Input];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error generating semantic queries, using direct input");
            return [state.CurrentTurn.Input];
        }
    }

    #endregion Search Operations

    #region Collection Management

    public async Task EnsureCollectionsAsync(CancellationToken ct = default)
    {
        foreach (var (type, prefix) in CollectionPrefixes)
        {
            var qdrant = qdrantFactory.CreateService(prefix);
            await qdrant.EnsureCollectionAsync(ct);
            logger.LogInformation("Ensured collection {Collection} for {Type}", prefix, type);
        }
    }

    public string GetCollectionName(DataType type)
    {
        return CollectionPrefixes.TryGetValue(type, out var name)
            ? name
            : throw new ArgumentException($"DataType {type} is not eligible for semantic operations");
    }

    public async Task<SemanticStats> GetStatsAsync(int profileId, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var allSemantic = await db.ContextData
            .Where(d => d.ProfileId == profileId &&
                       d.Availability == AvailabilityType.Semantic &&
                       d.IsEnabled &&
                       !d.IsArchived &&
                       (d.Type == DataType.Quote ||
                        d.Type == DataType.Memory ||
                        d.Type == DataType.Insight ||
                        d.Type == DataType.PersonaVoiceSample))
            .Select(d => new { d.Type, d.InVectorDb })
            .ToListAsync(ct);

        var embeddedByType = allSemantic
            .Where(d => d.InVectorDb)
            .GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        var pendingByType = allSemantic
            .Where(d => !d.InVectorDb)
            .GroupBy(d => d.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return new SemanticStats(
            TotalEmbedded: allSemantic.Count(d => d.InVectorDb),
            TotalPending: allSemantic.Count(d => !d.InVectorDb),
            EmbeddedByType: embeddedByType,
            PendingByType: pendingByType);
    }

    #endregion Collection Management

    #region Private Helpers

    private static bool IsSemanticEligible(ContextData data)
    {
        return SemanticEligibleTypes.Contains(data.Type) &&
               data.Availability == AvailabilityType.Semantic;
    }

    private static List<SemanticChunk> GenerateChunks(ContextData data)
    {
        var chunks = new List<SemanticChunk>();
        var displayContent = data.GetDisplayContent();
        var formattedText = FormatForEmbedding(data, displayContent);
        var baseId = GetStableHash(formattedText);

        // Full content chunk
        chunks.Add(new SemanticChunk
        {
            Id = baseId,
            Text = formattedText,
            PayloadId = $"{data.Type.ToString().ToLowerInvariant()}#{data.Id}#full",
            ChunkType = "full",
            Speaker = data.Speaker ?? string.Empty,
            SourceSessionId = data.SourceSessionId,
            DataId = data.Id,
            ProfileId = data.ProfileId,
            Tags = data.Tags
        });

        // Tag-augmented chunk for enhanced semantic search
        if (data.Tags.Count > 0)
        {
            var tagPrefix = string.Join(", ", data.Tags);
            var semanticText = $"{tagPrefix}. {formattedText}";

            chunks.Add(new SemanticChunk
            {
                Id = GetStableHash(semanticText),
                Text = semanticText,
                PayloadId = $"{data.Type.ToString().ToLowerInvariant()}#{data.Id}#semantic",
                ChunkType = "semantic",
                Speaker = data.Speaker ?? string.Empty,
                SourceSessionId = data.SourceSessionId,
                DataId = data.Id,
                ProfileId = data.ProfileId,
                Tags = data.Tags
            });
        }

        // Relevance-augmented chunk if relevance reason exists
        if (!string.IsNullOrWhiteSpace(data.RelevanceReason))
        {
            var relevanceText = $"{data.RelevanceReason}. {formattedText}";

            chunks.Add(new SemanticChunk
            {
                Id = GetStableHash(relevanceText),
                Text = relevanceText,
                PayloadId = $"{data.Type.ToString().ToLowerInvariant()}#{data.Id}#relevance",
                ChunkType = "relevance",
                Speaker = data.Speaker ?? string.Empty,
                SourceSessionId = data.SourceSessionId,
                DataId = data.Id,
                ProfileId = data.ProfileId,
                Tags = data.Tags
            });
        }

        return chunks;
    }

    private static string FormatForEmbedding(ContextData data, string content)
    {
        // Use the extension methods for consistent formatting across the system
        return data.Type switch
        {
            // Quote format: [sXX] S: (nonverbal) Line - uses extension method
            DataType.Quote => data.FormatAsQuote(),

            // PersonaVoiceSample: [sXX] S: (nonverbal) Line - uses extension method
            DataType.PersonaVoiceSample => data.FormatAsVoiceSampleQuote(),

            // Memory: include name as context
            DataType.Memory => !string.IsNullOrEmpty(data.Name)
                ? $"[Memory: {data.Name}] {content.FlattenMarkdownToPlainString()}"
                : content.FlattenMarkdownToPlainString(),

            // Insight: include name as context
            DataType.Insight => !string.IsNullOrEmpty(data.Name)
                ? $"[Insight: {data.Name}] {content.FlattenMarkdownToPlainString()}"
                : content.FlattenMarkdownToPlainString(),

            _ => content.FlattenMarkdownToPlainString()
        };
    }

    private static int GetStableHash(string text)
    {
        unchecked
        {
            var hash = 17;
            foreach (var c in text)
            {
                hash = hash * 31 + c;
            }
            return Math.Abs(hash);
        }
    }

    [GeneratedRegex(@"\[.*\]", RegexOptions.Singleline)]
    private static partial Regex MyRegex();

    #endregion Private Helpers
}

/// <summary>
/// Represents a chunk of content for semantic embedding
/// </summary>
internal class SemanticChunk
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string PayloadId { get; set; } = string.Empty;
    public string ChunkType { get; set; } = string.Empty;
    public string Speaker { get; set; } = string.Empty;
    public int? SourceSessionId { get; set; }
    public int DataId { get; set; }
    public int ProfileId { get; set; }
    public List<string> Tags { get; set; } = [];
}