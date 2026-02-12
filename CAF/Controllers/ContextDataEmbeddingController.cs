using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

/// <summary>
/// Controller for context data embedding, tag generation, and vector database operations
/// </summary>
[ApiController]
[Route("api/contextdata")]
public class ContextDataEmbeddingController(
    IContextDataService contextDataService,
    ISemanticService semanticService,
    IGeminiClient geminiClient,
    ISystemMessageService systemMessageService,
    ILogger<ContextDataEmbeddingController> logger) : ControllerBase
{
    private const int EmbeddingBatchSize = 96;

    #region Tag Generation

    /// <summary>
    /// Generate tags for multiple items in parallel
    /// </summary>
    [HttpPost("generate-tags-bulk")]
    public async Task<ActionResult<BulkTagGenerationResult>> GenerateTagsInBulk(
        [FromBody] List<int>? ids = null,
        [FromQuery] DataType? type = null,
        [FromQuery] int maxParallel = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get items to process
            List<ContextData> items;
            if (ids != null && ids.Count > 0)
            {
                items = await GetItemsByIdsAsync(ids, cancellationToken);
            }
            else
            {
                var allData = await contextDataService.GetAllAsync(type, AvailabilityType.Semantic, includeArchived: false, cancellationToken);
                items = [.. allData.Where(d => d.Tags.Count == 0)];
            }

            if (items.Count == 0)
            {
                return Ok(new BulkTagGenerationResult
                {
                    Message = "No items to process",
                    TotalCount = 0
                });
            }

            // Get QuoteMapper technical message
            var quoteMapper = await systemMessageService.GetTechnicalMessageByNameAsync(
                ConversationConstants.TechnicalMessages.QuoteMapper,
                cancellationToken);

            if (quoteMapper == null)
            {
                return StatusCode(500, new BulkTagGenerationResult
                {
                    Message = "QuoteMapper technical message not found",
                    TotalCount = items.Count,
                    FailedCount = items.Count
                });
            }

            var result = new BulkTagGenerationResult { TotalCount = items.Count };
            var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            var tasks = new List<Task>();

            logger.LogInformation("Starting parallel tag generation for {Count} items with {MaxParallel} parallel tasks", items.Count, maxParallel);

            foreach (var item in items)
            {

                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var request = ContextDataHelper.CreateTagGenerationRequest(quoteMapper.Content, item);
                        var (success, response) = await geminiClient.GenerateContentAsync(request, technical: true, cancellationToken: cancellationToken);

                        if (!success)
                        {
                            lock (result)
                            {
                                result.FailedCount++;
                                result.Errors.Add($"Item {item.Id} ({item.Name}): LLM call failed - {response}");
                            }
                            return;
                        }

                        var (tags, relevanceScore, relevanceReason) = ContextDataHelper.ParseQuoteMapperResponse(response);

                        item.Tags = tags;
                        item.RelevanceScore = relevanceScore;
                        item.RelevanceReason = relevanceReason;
                        await contextDataService.UpdateAsync(item.Id, item, cancellationToken);

                        result.SuccessCount++;
                        result.ProcessedItems.Add(new ProcessedTagItem
                        {
                            Id = item.Id,
                            Name = item.Name,
                            TagCount = tags.Count,
                            RelevanceScore = relevanceScore
                        });

                        logger.LogInformation("Generated tags for item {Id}: {TagCount} tags, relevance score {Score}",
                            item.Id, tags.Count, relevanceScore);
                    }
                    catch (Exception ex)
                    {
                        lock (result)
                        {
                            result.FailedCount++;
                            result.Errors.Add($"Item {item.Id} ({item.Name}): {ex.Message}");
                        }
                        logger.LogError(ex, "Error generating tags for item {Id}", item.Id);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            result.Message = $"Successfully generated tags for {result.SuccessCount}/{result.TotalCount} items";
            logger.LogInformation("Completed parallel tag generation: {Success}/{Total} successful", result.SuccessCount, result.TotalCount);

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Bulk tag generation cancelled");
            return StatusCode(499, new BulkTagGenerationResult { Message = "Operation was cancelled" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in bulk tag generation");
            return StatusCode(500, new BulkTagGenerationResult
            {
                Message = $"Error: {ex.Message}",
                Errors = [ex.ToString()]
            });
        }
    }

    /// <summary>
    /// Generate tags for a single item
    /// </summary>
    [HttpPost("{id}/generate-tags")]
    public async Task<ActionResult<TagGenerationResult>> GenerateTags(int id, CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        try
        {
            var quoteMapper = await systemMessageService.GetTechnicalMessageByNameAsync(
                ConversationConstants.TechnicalMessages.QuoteMapper,
                cancellationToken);

            if (quoteMapper == null)
            {
                return StatusCode(500, new TagGenerationResult
                {
                    Success = false,
                    Message = "QuoteMapper technical message not found"
                });
            }

            var request = ContextDataHelper.CreateTagGenerationRequest(quoteMapper.Content, data);
            var (success, response) = await geminiClient.GenerateContentAsync(request, technical: true, cancellationToken: cancellationToken);

            if (!success)
            {
                return StatusCode(500, new TagGenerationResult
                {
                    Success = false,
                    Message = $"LLM call failed: {response}"
                });
            }

            var (tags, relevanceScore, relevanceReason) = ContextDataHelper.ParseQuoteMapperResponse(response);

            // Update the data
            data.Tags = tags;
            data.RelevanceScore = relevanceScore;
            data.RelevanceReason = relevanceReason;
            await contextDataService.UpdateAsync(id, data, cancellationToken);

            return Ok(new TagGenerationResult
            {
                Success = true,
                Tags = tags,
                RelevanceScore = relevanceScore,
                RelevanceReason = relevanceReason,
                Message = $"Generated {tags.Count} tags and relevance score {relevanceScore}"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating tags for {Id}", id);
            return StatusCode(500, new TagGenerationResult
            {
                Success = false,
                Message = $"Error generating tags: {ex.Message}"
            });
        }
    }

    #endregion

    #region Single Item Embedding

    /// <summary>
    /// Embed a single item in the vector database
    /// </summary>
    [HttpPost("{id}/embed")]
    public async Task<ActionResult<EmbedResult>> Embed(int id, CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        if (data.Availability != AvailabilityType.Semantic)
            return BadRequest(new EmbedResult
            {
                Success = false,
                Message = "Item must have Semantic availability to be embedded"
            });

        if (data.Tags.Count == 0)
            return BadRequest(new EmbedResult
            {
                Success = false,
                Message = "Item must have tags before embedding. Generate tags first."
            });

        try
        {
            await semanticService.EmbedAsync(data, cancellationToken);
            return Ok(new EmbedResult
            {
                Success = true,
                VectorId = data.VectorId,
                Message = "Successfully embedded in vector database"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error embedding {Id}", id);
            return StatusCode(500, new EmbedResult
            {
                Success = false,
                Message = $"Error embedding: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Remove a single item from the vector database
    /// </summary>
    [HttpPost("{id}/unembed")]
    public async Task<ActionResult<EmbedResult>> Unembed(int id, CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        if (!data.InVectorDb)
            return Ok(new EmbedResult
            {
                Success = true,
                Message = "Item is not embedded"
            });

        try
        {
            await semanticService.UnembedAsync(data, cancellationToken);
            return Ok(new EmbedResult
            {
                Success = true,
                Message = "Successfully removed from vector database"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error unembedding {Id}", id);
            return StatusCode(500, new EmbedResult
            {
                Success = false,
                Message = $"Error unembedding: {ex.Message}"
            });
        }
    }

    #endregion

    #region Bulk Embedding

    /// <summary>
    /// Embed all eligible items (Semantic with tags, not yet embedded)
    /// </summary>
    [HttpPost("embed-all")]
    public async Task<ActionResult<BulkEmbedResult>> EmbedAll(
        [FromQuery] DataType? type = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = await contextDataService.GetAllAsync(type, AvailabilityType.Semantic, includeArchived: false, cancellationToken);
            var items = data.Where(d => d.Tags.Count > 0 && !d.InVectorDb).ToList();

            return await ProcessBulkEmbedding(items, "bulk embed", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to embed all context data");
            return StatusCode(500, new BulkEmbedResult
            {
                Message = ex.Message,
                Errors = [ex.ToString()]
            });
        }
    }

    /// <summary>
    /// Embed selected items by IDs
    /// </summary>
    [HttpPost("embed-selected")]
    public async Task<ActionResult<BulkEmbedResult>> EmbedSelected(
        [FromBody] List<int> ids,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return BadRequest(new BulkEmbedResult
                {
                    Message = "No IDs provided",
                    TotalCount = 0
                });
            }

            var allItems = await GetItemsByIdsAsync(ids, cancellationToken);
            var items = allItems
                .Where(d => d.Availability == AvailabilityType.Semantic && d.Tags.Count > 0 && !d.InVectorDb)
                .ToList();

            return items.Count == 0
                ? (ActionResult<BulkEmbedResult>)Ok(new BulkEmbedResult
                {
                    Message = "No eligible items to embed (must be Semantic with tags and not already embedded)",
                    TotalCount = ids.Count,
                    FailedCount = ids.Count - allItems.Count
                })
                : await ProcessBulkEmbedding(items, "bulk embed of selected", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to embed selected context data");
            return StatusCode(500, new BulkEmbedResult
            {
                Message = ex.Message,
                Errors = [ex.ToString()]
            });
        }
    }

    /// <summary>
    /// Unembed selected items by IDs (bulk operation)
    /// </summary>
    [HttpPost("unembed-selected")]
    public async Task<ActionResult<BulkEmbedResult>> UnembedSelected(
        [FromBody] List<int> ids,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return BadRequest(new BulkEmbedResult
                {
                    Message = "No IDs provided",
                    TotalCount = 0
                });
            }

            var allItems = await GetItemsByIdsAsync(ids, cancellationToken);
            var itemsToUnembed = allItems.Where(d => d.InVectorDb).ToList();

            if (itemsToUnembed.Count == 0)
            {
                return Ok(new BulkEmbedResult
                {
                    Message = "No embedded items to unembed",
                    TotalCount = ids.Count,
                    SuccessCount = 0,
                    FailedCount = 0
                });
            }

            var result = new BulkEmbedResult { TotalCount = ids.Count };

            try
            {
                // Use batch unembed from semantic service
                await semanticService.UnembedBatchAsync(itemsToUnembed, cancellationToken);

                result.SuccessCount = itemsToUnembed.Count;
                result.Message = $"Successfully unembedded {itemsToUnembed.Count} items";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to unembed batch");
                result.FailedCount = itemsToUnembed.Count;
                result.Errors.Add($"Batch unembed failed: {ex.Message}");
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to unembed selected context data");
            return StatusCode(500, new BulkEmbedResult
            {
                Message = ex.Message,
                Errors = [ex.ToString()]
            });
        }
    }

    /// <summary>
    /// Index context data (similar to embed-all but with force option)
    /// </summary>
    [HttpPost("index")]
    public async Task<ActionResult<BulkEmbedResult>> IndexContextData(
        [FromQuery] DataType? type = null,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = await contextDataService.GetAllAsync(type, AvailabilityType.Semantic, includeArchived: false, cancellationToken);
            var items = query.Where(d => d.Tags.Count > 0 && (force || !d.InVectorDb)).ToList();

            return await ProcessBulkEmbedding(items, "bulk index", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to index context data");
            return StatusCode(500, new BulkEmbedResult
            {
                Message = ex.Message,
                Errors = [ex.ToString()]
            });
        }
    }

    #endregion

    #region Helper Methods

    private async Task<List<ContextData>> GetItemsByIdsAsync(List<int> ids, CancellationToken cancellationToken)
    {
        var items = new List<ContextData>();
        foreach (var id in ids)
        {
            var item = await contextDataService.GetByIdAsync(id, cancellationToken);
            if (item != null)
            {
                items.Add(item);
            }
        }
        return items;
    }

    private async Task<ActionResult<BulkEmbedResult>> ProcessBulkEmbedding(
        List<ContextData> items,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return Ok(new BulkEmbedResult
            {
                Message = "No items to embed",
                TotalCount = 0
            });
        }

        var result = new BulkEmbedResult { TotalCount = items.Count };
        var indexed = 0;
        var offset = 0;

        logger.LogInformation("Starting {Operation} of {Count} context data items", operationName, items.Count);

        while (offset < items.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = items.Skip(offset).Take(EmbeddingBatchSize).ToList();
            if (batch.Count == 0)
                break;

            try
            {
                await semanticService.IndexContextDataBatchAsync(batch, EmbeddingBatchSize, cancellationToken);

                // Mark as embedded and update database
                foreach (var item in batch)
                {
                    item.InVectorDb = true;
                    item.VectorId = $"{item.Type.ToString().ToLowerInvariant()}#{item.Id}#full";
                    await contextDataService.UpdateAsync(item.Id, item, cancellationToken);
                }

                indexed += batch.Count;
                result.SuccessCount += batch.Count;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to embed batch at offset {Offset}", offset);
                result.FailedCount += batch.Count;
                result.Errors.Add($"Batch at offset {offset}: {ex.Message}");
            }

            offset += EmbeddingBatchSize;
            logger.LogInformation("Embedded {Current}/{Total} context data items", indexed, items.Count);

            // Memory management
            batch.Clear();
            if (offset % (EmbeddingBatchSize * 4) == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        result.Message = $"Successfully embedded {result.SuccessCount}/{result.TotalCount} items";
        return Ok(result);
    }

    #endregion
}

/// <summary>
/// Helper class for context data operations
/// </summary>
public static class ContextDataHelper
{
    public static GeminiRequest CreateTagGenerationRequest(string systemPrompt, ContextData item)
    {
        var userPrompt = $"""
            Quote Analysis:
            Session: {item.SourceSessionId ?? 0}
            Speaker: {item.Speaker ?? "Unknown"}
            Content: {item.Content}
            Nonverbal: {item.NonverbalBehavior??""}
            """;

        return new GeminiRequest
        {
            SystemInstruction = new SystemInstruction
            {
                Parts = [new Part { Text = systemPrompt }]
            },
            Contents =
            [
                new Content
                {
                    Role = "user",
                    Parts = [new Part { Text = userPrompt }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 1000,
                Temperature = 0.3f,
                ThinkingConfig = new ThinkingConfig { IncludeThoughts = false }
            }
        };
    }

    public static (List<string> Tags, int RelevanceScore, string RelevanceReason) ParseQuoteMapperResponse(string response)
    {
        try
        {
            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = response[start..(end + 1)];
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tags = new List<string>();
                if (root.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
                {
                    tags = [.. tagsProp.EnumerateArray()
                        .Select(t => t.GetString())
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => t!.ToLowerInvariant())];
                }

                var score = 0;
                if (root.TryGetProperty("relevanceScore", out var scoreProp))
                {
                    score = Math.Clamp(scoreProp.GetInt32(), 0, 100);
                }

                var reason = "";
                if (root.TryGetProperty("relevanceReason", out var reasonProp))
                {
                    reason = reasonProp.GetString() ?? "";
                }

                return (tags, score, reason);
            }
        }
        catch { }

        return ([], 0, "Could not parse QuoteMapper response");
    }
}

#region Response Models

public class TagGenerationResult
{
    public bool Success { get; set; }
    public List<string> Tags { get; set; } = [];
    public int RelevanceScore { get; set; }
    public string? RelevanceReason { get; set; }
    public string Message { get; set; } = "";
}

public class EmbedResult
{
    public bool Success { get; set; }
    public string? VectorId { get; set; }
    public string Message { get; set; } = "";
}

public class BulkEmbedResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = "";
    public List<string> Errors { get; set; } = [];
}

public class BulkTagGenerationResult
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public string Message { get; set; } = "";
    public List<string> Errors { get; set; } = [];
    public ConcurrentBag<ProcessedTagItem> ProcessedItems { get; set; } = [];
}

public class ProcessedTagItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TagCount { get; set; }
    public int RelevanceScore { get; set; }
}

#endregion