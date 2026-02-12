using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

/// <summary>
/// Controller for basic CRUD operations, availability management, triggers, imports, and statistics
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContextDataController(
IContextDataService contextDataService,
ISemanticService semanticService,
IGeminiClient geminiClient,
ILogger<ContextDataController> logger) : ControllerBase
{
    #region Basic CRUD

    [HttpGet]
    public async Task<ActionResult<List<ContextData>>> GetAll(
        [FromQuery] DataType? type = null,
        [FromQuery] AvailabilityType? availability = null,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetAllAsync(type, availability, includeArchived, cancellationToken);
        return Ok(data);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ContextData>> GetById(int id, CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        return data == null ? NotFound() : Ok(data);
    }

    [HttpPost]
    public async Task<ActionResult<ContextData>> Create([FromBody] ContextData data, CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await contextDataService.CreateAsync(data, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating context data");
            return StatusCode(500, new { error = "An error occurred while creating context data" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ContextData>> Update(int id, [FromBody] ContextData data, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await contextDataService.UpdateAsync(id, data, cancellationToken);
            return updated == null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating context data {Id}", id);
            return StatusCode(500, new { error = "An error occurred while updating context data" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await contextDataService.DeleteAsync(id, cancellationToken);
        return !deleted ? NotFound() : NoContent();
    }

    [HttpPost("{id}/archive")]
    public async Task<ActionResult> Archive(int id, CancellationToken cancellationToken = default)
    {
        var success = await contextDataService.ArchiveAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult> Restore(int id, CancellationToken cancellationToken = default)
    {
        var success = await contextDataService.RestoreAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    #endregion

    #region Availability Management

    [HttpPost("{id}/availability")]
    public async Task<ActionResult<AvailabilityChangeResult>> ChangeAvailability(
        int id,
        [FromBody] ChangeAvailabilityRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        var result = new AvailabilityChangeResult
        {
            OldAvailability = data.Availability,
            NewAvailability = request.Availability,
            RequiresUnembed = false,
            WasEmbedded = data.InVectorDb
        };

        // Check if changing from Semantic to something else requires unembed
        if (data.Availability == AvailabilityType.Semantic &&
            request.Availability != AvailabilityType.Semantic &&
            data.InVectorDb)
        {
            result.RequiresUnembed = true;

            if (!request.ConfirmUnembed)
            {
                result.Success = false;
                result.Message = "This item is embedded in the vector database. Changing availability will remove the embedding. Set confirmUnembed to true to proceed.";
                return Ok(result);
            }

            // Unembed the item
            await semanticService.UnembedAsync(data, cancellationToken);
            result.WasUnembedded = true;
        }

        var success = await contextDataService.ChangeAvailabilityAsync(id, request.Availability, cancellationToken);
        if (!success)
        {
            result.Success = false;
            result.Message = $"Invalid combination: {data.Type} cannot have {request.Availability} availability";
            return BadRequest(result);
        }

        result.Success = true;
        result.Message = "Availability changed successfully";
        return Ok(result);
    }

    [HttpPost("{id}/use-next-turn")]
    public async Task<ActionResult> SetUseNextTurn(int id, CancellationToken cancellationToken = default)
    {
        var success = await contextDataService.SetUseNextTurnAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    [HttpPost("{id}/use-every-turn")]
    public async Task<ActionResult> SetUseEveryTurn(int id, [FromQuery] bool enabled = true, CancellationToken cancellationToken = default)
    {
        var success = await contextDataService.SetUseEveryTurnAsync(id, enabled, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    [HttpPost("{id}/clear-manual")]
    public async Task<ActionResult> ClearManualFlags(int id, CancellationToken cancellationToken = default)
    {
        var success = await contextDataService.ClearManualFlagsAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    #endregion

    #region Trigger Management

    [HttpPut("{id}/trigger")]
    public async Task<ActionResult<ContextData>> UpdateTrigger(
        int id,
        [FromBody] UpdateTriggerRequest request,
        CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        if (data.Availability != AvailabilityType.Trigger)
            return BadRequest(new { error = "Item must have Trigger availability to update trigger settings" });

        data.TriggerKeywords = request.Keywords;
        data.TriggerLookbackTurns = request.LookbackTurns ?? data.TriggerLookbackTurns;
        data.TriggerMinMatchCount = request.MinMatchCount ?? data.TriggerMinMatchCount;

        var updated = await contextDataService.UpdateAsync(id, data, cancellationToken);
        return Ok(updated);
    }

    #endregion

    #region Import Operations

    [HttpPost("import/tsv")]
    public async Task<ActionResult<ImportResult>> ImportFromTsv(
        [FromBody] TsvImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ImportResult { Message = "TSV content is required" });

        var result = new ImportResult();
        var lines = request.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var hasHeader = request.HasHeader;
        var startIndex = hasHeader ? 1 : 0;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var columns = line.Split('\t');
                
                // Full format: session\tspeaker\tcontent
                if (columns.Length == 3)
                {
                    var sessionStr = columns[0].Trim();
                    var speaker = columns[1].Trim();
                    var content = columns[2].Trim();
                    
                    int? sessionId = null;
                    if (int.TryParse(sessionStr, out var sid))
                    {
                        sessionId = sid;
                    }

                    var data = new ContextData
                    {
                        Type = request.DataType,
                        Availability = request.DefaultAvailability,
                        Name = $"{speaker} - Session {sessionStr}, Line {i - startIndex + 1}",
                        Content = content,
                        Speaker = speaker,
                        SourceSessionId = sessionId,
                        IsEnabled = true
                    };

                    await contextDataService.CreateAsync(data, cancellationToken);
                    result.SuccessCount++;
                }
                // Simple format: session\tcontent (with speaker provided in request)
                else if (columns.Length == 2 && !string.IsNullOrWhiteSpace(request.Speaker))
                {
                    var sessionStr = columns[0].Trim();
                    var content = columns[1].Trim();
                    
                    int? sessionId = null;
                    if (int.TryParse(sessionStr, out var sid))
                    {
                        sessionId = sid;
                    }

                    var data = new ContextData
                    {
                        Type = request.DataType,
                        Availability = request.DefaultAvailability,
                        Name = $"{request.Speaker} - Session {sessionStr}, Line {i - startIndex + 1}",
                        Content = content,
                        Speaker = request.Speaker,
                        SourceSessionId = sessionId,
                        IsEnabled = true
                    };

                    await contextDataService.CreateAsync(data, cancellationToken);
                    result.SuccessCount++;
                }
                else
                {
                    result.Errors.Add($"Line {i + 1}: Invalid format. Expected either 3 columns (Session, Speaker, Content) or 2 columns (Session, Content) with Speaker field filled");
                    result.FailedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Line {i + 1}: {ex.Message}");
                result.FailedCount++;
            }
        }

        result.Message = $"Imported {result.SuccessCount} items, {result.FailedCount} failed";
        return Ok(result);
    }

    [HttpPost("import/voice-samples-tsv")]
    public async Task<ActionResult<ImportResult>> ImportVoiceSamplesFromTsv(
        [FromBody] VoiceSamplesTsvImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ImportResult { Message = "TSV content is required" });

        var result = new ImportResult();
        var lines = request.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var hasHeader = request.HasHeader;
        var startIndex = hasHeader ? 1 : 0;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var columns = line.Split('\t');
                
                // Expected format: Dialogue\tNonverbal Behavior
                if (columns.Length >= 2)
                {
                    var dialogue = columns[0].Trim();
                    var nonverbalBehavior = columns[1].Trim();
                    
                    if (string.IsNullOrWhiteSpace(dialogue))
                    {
                        result.Errors.Add($"Line {i + 1}: Dialogue is empty");
                        result.FailedCount++;
                        continue;
                    }

                    // Create name from first few words of dialogue (max 50 chars)
                    var name = dialogue.Length > 50 
                        ? string.Concat(dialogue.AsSpan(0, 47), "...")
                        : dialogue;
                    
                    // Add speaker prefix if provided
                    if (!string.IsNullOrWhiteSpace(request.Speaker))
                    {
                        name = $"{request.Speaker}: {name}";
                    }

                    var data = new ContextData
                    {
                        Type = DataType.PersonaVoiceSample,
                        Availability = request.DefaultAvailability,
                        Name = name,
                        Content = dialogue,
                        Speaker = request.Speaker,
                        NonverbalBehavior = nonverbalBehavior,
                        IsEnabled = true
                    };

                    await contextDataService.CreateAsync(data, cancellationToken);
                    result.SuccessCount++;
                }
                else
                {
                    result.Errors.Add($"Line {i + 1}: Invalid format. Expected 2 columns (Dialogue, Nonverbal Behavior)");
                    result.FailedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Line {i + 1}: {ex.Message}");
                result.FailedCount++;
            }
        }

        result.Message = $"Imported {result.SuccessCount} voice samples, {result.FailedCount} failed";
        return Ok(result);
    }

    [HttpPost("import/canon-quotes-tsv")]
    public async Task<ActionResult<ImportResult>> ImportCanonQuotesFromTsv(
        [FromBody] CanonQuotesTsvImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ImportResult { Message = "TSV content is required" });

        var result = new ImportResult();
        var lines = request.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var hasHeader = request.HasHeader;
        var startIndex = hasHeader ? 1 : 0;

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var columns = line.Split('\t');
                
                // Expected format: Dialogue\tNonverbal Behavior
                if (columns.Length >= 2)
                {
                    var dialogue = columns[0].Trim();
                    var nonverbalBehavior = columns[1].Trim();
                    
                    if (string.IsNullOrWhiteSpace(dialogue))
                    {
                        result.Errors.Add($"Line {i + 1}: Dialogue is empty");
                        result.FailedCount++;
                        continue;
                    }

                    // Create name from first few words of dialogue (max 50 chars)
                    var name = dialogue.Length > 50 
                        ? string.Concat(dialogue.AsSpan(0, 47), "...")
                        : dialogue;

                    // Store dialogue as content, nonverbal behavior in dedicated field
                    var content = dialogue;

                    var data = new ContextData
                    {
                        Type = DataType.Quote,
                        Availability = request.DefaultAvailability,
                        Name = name,
                        Content = content,
                        Speaker = request.Speaker, // Optional speaker parameter
                        NonverbalBehavior = nonverbalBehavior, // Store in dedicated field
                        IsEnabled = true
                    };

                    await contextDataService.CreateAsync(data, cancellationToken);
                    result.SuccessCount++;
                }
                else
                {
                    result.Errors.Add($"Line {i + 1}: Invalid format. Expected 2 columns (Dialogue, Nonverbal Behavior)");
                    result.FailedCount++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Line {i + 1}: {ex.Message}");
                result.FailedCount++;
            }
        }

        result.Message = $"Imported {result.SuccessCount} canon quotes, {result.FailedCount} failed";
        return Ok(result);
    }

    [HttpPost("import/markdown")]
    public async Task<ActionResult<ImportResult>> ImportFromMarkdown(
        [FromBody] MarkdownImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new ImportResult { Message = "Markdown content is required" });

        var result = new ImportResult();

        try
        {
            // Split by headers (## or ###)
            var sections = SplitMarkdownSections(request.Content);

            foreach (var section in sections)
            {
                if (string.IsNullOrWhiteSpace(section.Title) || string.IsNullOrWhiteSpace(section.Content))
                    continue;

                try
                {
                    var data = new ContextData
                    {
                        Type = request.DataType,
                        Availability = request.DefaultAvailability,
                        Name = section.Title,
                        Content = section.Content.Trim(),
                        IsEnabled = true
                    };

                    await contextDataService.CreateAsync(data, cancellationToken);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Section '{section.Title}': {ex.Message}");
                    result.FailedCount++;
                }
            }

            result.Message = $"Imported {result.SuccessCount} sections, {result.FailedCount} failed";
        }
        catch (Exception ex)
        {
            result.Message = $"Error parsing markdown: {ex.Message}";
        }

        return Ok(result);
    }

    [HttpPost("import/folder")]
    public async Task<ActionResult<ImportResult>> ImportFromFolder(
        [FromBody] FolderImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FolderPath))
            return BadRequest(new ImportResult { Message = "Folder path is required" });

        if (!Directory.Exists(request.FolderPath))
            return BadRequest(new ImportResult { Message = $"Folder not found: {request.FolderPath}" });

        var result = new ImportResult { ProcessedItems = [] };
        var extensions = request.Extensions ?? [".md", ".txt"];

        try
        {
            var files = extensions
                .SelectMany(ext => Directory.GetFiles(request.FolderPath, $"*{ext}", SearchOption.AllDirectories))
                .ToList();

            // If UpdateExisting is true, load all existing items once for efficiency
            var existingItems = request.UpdateExisting 
                ? (await contextDataService.GetAllAsync(includeArchived: true, cancellationToken: cancellationToken))
                    .ToDictionary(d => d.Name.ToLowerInvariant(), d => d, StringComparer.OrdinalIgnoreCase)
                : null;

            foreach (var file in files)
            {
                try
                {
                    var content = await System.IO.File.ReadAllTextAsync(file, cancellationToken);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var relativePath = Path.GetRelativePath(request.FolderPath, file);

                    if (request.UpdateExisting && existingItems != null)
                    {
                        // Try to find existing item by name
                        if (existingItems.TryGetValue(fileName, out var existingItem))
                        {
                            // Update the existing item's content
                            existingItem.Content = content;
                            existingItem.ModifiedAt = DateTime.UtcNow;
                            
                            // Optionally update description with file path
                            if (string.IsNullOrEmpty(existingItem.Description))
                                existingItem.Description = relativePath;

                            await contextDataService.UpdateAsync(existingItem.Id, existingItem, cancellationToken);
                            result.Updated++;
                            result.ProcessedItems?.Add($"Updated: {fileName}");
                        }
                        else
                        {
                            result.NotFound++;
                            result.Errors.Add($"Not found: '{fileName}' (from file '{relativePath}')");
                        }
                    }
                    else
                    {
                        // Create new item
                        var data = new ContextData
                        {
                            Type = request.DataType,
                            Availability = request.DefaultAvailability,
                            Name = fileName,
                            Content = content,
                            Description = relativePath,
                            IsEnabled = true
                        };

                        await contextDataService.CreateAsync(data, cancellationToken);
                        result.SuccessCount++;
                        result.ProcessedItems?.Add($"Created: {fileName}");
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"File '{Path.GetFileName(file)}': {ex.Message}");
                    result.FailedCount++;
                }
            }

            result.Message = request.UpdateExisting
                ? $"Updated {result.Updated} items, {result.NotFound} not found, {result.FailedCount} failed"
                : $"Imported {result.SuccessCount} files, {result.FailedCount} failed";
        }
        catch (Exception ex)
        {
            result.Message = $"Error reading folder: {ex.Message}";
        }

        return Ok(result);
    }

    #endregion

    #region Statistics

    [HttpGet("stats")]
    public async Task<ActionResult<ContextDataStats>> GetStats(CancellationToken cancellationToken = default)
    {
        var allData = await contextDataService.GetAllAsync(includeArchived: true, cancellationToken: cancellationToken);

        var stats = new ContextDataStats
        {
            TotalCount = allData.Count,
            ActiveCount = allData.Count(d => d.IsEnabled && !d.IsArchived),
            ArchivedCount = allData.Count(d => d.IsArchived),
            EmbeddedCount = allData.Count(d => d.InVectorDb),
            ByType = allData
                .GroupBy(d => d.Type)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
            ByAvailability = allData
                .Where(d => !d.IsArchived)
                .GroupBy(d => d.Availability)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };

        return Ok(stats);
    }

    #endregion

    #region Reload from Disk

    [HttpPost("{id}/reload-from-disk")]
    public async Task<IActionResult> ReloadFromDisk(int id, CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(data.Path))
            return BadRequest("No path set for this item");

        if (!System.IO.File.Exists(data.Path))
            return BadRequest($"File not found: {data.Path}");

        try
        {
            var content = await System.IO.File.ReadAllTextAsync(data.Path, cancellationToken);
            data.Content = content;
            data.ModifiedAt = DateTime.UtcNow;

            await contextDataService.UpdateAsync(id, data, cancellationToken);

            logger.LogInformation("Reloaded content from disk for item {Id} from path {Path}", id, data.Path);
            return Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload content from disk for item {Id} from path {Path}", id, data.Path);
            return StatusCode(500, $"Failed to read file: {ex.Message}");
        }
    }

    [HttpPost("bulk-reload-from-disk")]
    public async Task<ActionResult<BulkOperationResult>> BulkReloadFromDisk(CancellationToken cancellationToken = default)
    {
        var allData = await contextDataService.GetAllAsync(includeArchived: true, cancellationToken: cancellationToken);
        var itemsWithPaths = allData.Where(d => !string.IsNullOrWhiteSpace(d.Path)).ToList();

        var reloadedCount = 0;
        var errorCount = 0;

        foreach (var item in itemsWithPaths)
        {
            if (System.IO.File.Exists(item.Path))
            {
                try
                {
                    var content = await System.IO.File.ReadAllTextAsync(item.Path, cancellationToken);
                    item.Content = content;
                    item.ModifiedAt = DateTime.UtcNow;
                    await contextDataService.UpdateAsync(item.Id, item, cancellationToken);
                    reloadedCount++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to reload content from disk for item {Id} from path {Path}", item.Id, item.Path);
                    errorCount++;
                }
            }
            else
            {
                logger.LogWarning("File not found for item {Id}: {Path}", item.Id, item.Path);
                errorCount++;
            }
        }

        logger.LogInformation("Bulk reload from disk completed: {Reloaded} reloaded, {Errors} errors", reloadedCount, errorCount);

        return Ok(new BulkOperationResult
        {
            Processed = itemsWithPaths.Count,
            Reloaded = reloadedCount,
            Errors = errorCount
        });
    }

    [HttpPost("bulk-reload-selected-from-disk")]
    public async Task<ActionResult<BulkOperationResult>> BulkReloadSelectedFromDisk([FromBody] BulkOperationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids == null || request.Ids.Count == 0)
            return BadRequest("No IDs provided");

        var reloadedCount = 0;
        var errorCount = 0;

        foreach (var id in request.Ids)
        {
            var item = await contextDataService.GetByIdAsync(id, cancellationToken);
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
            {
                errorCount++;
                continue;
            }

            if (System.IO.File.Exists(item.Path))
            {
                try
                {
                    var content = await System.IO.File.ReadAllTextAsync(item.Path, cancellationToken);
                    item.Content = content;
                    item.ModifiedAt = DateTime.UtcNow;
                    await contextDataService.UpdateAsync(item.Id, item, cancellationToken);
                    reloadedCount++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to reload content from disk for item {Id} from path {Path}", item.Id, item.Path);
                    errorCount++;
                }
            }
            else
            {
                logger.LogWarning("File not found for item {Id}: {Path}", item.Id, item.Path);
                errorCount++;
            }
        }

        logger.LogInformation("Bulk reload selected from disk completed: {Reloaded} reloaded, {Errors} errors", reloadedCount, errorCount);

        return Ok(new BulkOperationResult
        {
            Processed = request.Ids.Count,
            Reloaded = reloadedCount,
            Errors = errorCount
        });
    }

    #endregion

    #region Helper Methods

    private static List<(string Title, string Content)> SplitMarkdownSections(string markdown)
    {
        var sections = new List<(string Title, string Content)>();
        var lines = markdown.Split('\n');
        string? currentTitle = null;
        var currentContent = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Check if this is a separator (---)
            if (trimmedLine == "---" || trimmedLine.StartsWith("---"))
            {
                // Save current section
                if (currentTitle != null && currentContent.Count > 0)
                {
                    sections.Add((currentTitle, string.Join("\n", currentContent).Trim()));
                    currentTitle = null;
                    currentContent.Clear();
                }
            }
            // Check if this is a top-level header (exactly ## but not ### or deeper)
            else if (line.StartsWith("## ") && !line.StartsWith("### "))
            {
                // If there's a current section without a separator, save it first
                if (currentTitle != null && currentContent.Count > 0)
                {
                    sections.Add((currentTitle, string.Join("\n", currentContent).Trim()));
                    currentContent.Clear();
                }

                // Start new section - remove only the "## " prefix
                currentTitle = line.Substring(3).Trim();
            }
            else if (currentTitle != null)
            {
                // Include everything else (including ### and deeper) as content
                currentContent.Add(line);
            }
        }

        // Add last section if there's one without a closing separator
        if (currentTitle != null && currentContent.Count > 0)
        {
            sections.Add((currentTitle, string.Join("\n", currentContent).Trim()));
        }

        return sections;
    }

    #endregion

    #region Token Counting

    [HttpPost("{id}/count-tokens")]
    public async Task<ActionResult<TokenCountResult>> CountTokens(int id, CancellationToken cancellationToken = default)
    {
        var data = await contextDataService.GetByIdAsync(id, cancellationToken);
        if (data == null)
            return NotFound();

        var displayContent = data.GetDisplayContent();
        var tokenCount = await geminiClient.CountTokensAsync(displayContent, cancellationToken);

        // Save token count to database using dedicated method
        await contextDataService.UpdateTokenCountAsync(id, tokenCount, cancellationToken);

        return Ok(new TokenCountResult
        {
            Id = id,
            Name = data.Name,
            TokenCount = tokenCount,
            ContentLength = displayContent.Length
        });
    }

    [HttpPost("count-tokens-bulk")]
    public async Task<ActionResult<List<TokenCountResult>>> CountTokensBulk(
        [FromBody] List<int> ids,
        CancellationToken cancellationToken = default)
    {
        // Limit concurrent API calls to avoid overwhelming the Gemini API
        const int maxConcurrency = 5;
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = ids.Select(async id =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var data = await contextDataService.GetByIdAsync(id, cancellationToken);
                if (data == null)
                    return null;

                var displayContent = data.GetDisplayContent();
                var tokenCount = await geminiClient.CountTokensAsync(displayContent, cancellationToken);

                // Save token count to database using dedicated method
                await contextDataService.UpdateTokenCountAsync(id, tokenCount, cancellationToken);

                return new TokenCountResult
                {
                    Id = id,
                    Name = data.Name,
                    TokenCount = tokenCount,
                    ContentLength = displayContent.Length
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return Ok(results.Where(r => r != null).ToList());
    }

    #endregion
}

#region Request/Response Models

public record ChangeAvailabilityRequest(AvailabilityType Availability, bool ConfirmUnembed = false);

public record UpdateTriggerRequest(string Keywords, int? LookbackTurns = null, int? MinMatchCount = null);

public record TsvImportRequest(
string Content,
DataType DataType = DataType.Quote,
AvailabilityType DefaultAvailability = AvailabilityType.Semantic,
bool HasHeader = false,
string? Speaker = null);

public record CanonQuotesTsvImportRequest(
    string Content,
    AvailabilityType DefaultAvailability = AvailabilityType.AlwaysOn,
    bool HasHeader = true,
    string? Speaker = null);

public record VoiceSamplesTsvImportRequest(
    string Content,
    AvailabilityType DefaultAvailability = AvailabilityType.Semantic,
    bool HasHeader = false,
    string? Speaker = null);

public record MarkdownImportRequest(
    string Content,
    DataType DataType = DataType.Memory,
    AvailabilityType DefaultAvailability = AvailabilityType.Semantic);

public record FolderImportRequest(
string FolderPath,
DataType DataType = DataType.Generic,
AvailabilityType DefaultAvailability = AvailabilityType.AlwaysOn,
List<string>? Extensions = null,
bool UpdateExisting = false);

public class AvailabilityChangeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public AvailabilityType OldAvailability { get; set; }
    public AvailabilityType NewAvailability { get; set; }
    public bool RequiresUnembed { get; set; }
    public bool WasEmbedded { get; set; }
    public bool WasUnembedded { get; set; }
}

public class ImportResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int Updated { get; set; }
    public int NotFound { get; set; }
    public string Message { get; set; } = "";
    public List<string> Errors { get; set; } = [];
    public List<string>? ProcessedItems { get; set; }
}

public class ContextDataStats
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int ArchivedCount { get; set; }
    public int EmbeddedCount { get; set; }
    public Dictionary<string, int> ByType { get; set; } = [];
    public Dictionary<string, int> ByAvailability { get; set; } = [];
}

public class TokenCountResult
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int TokenCount { get; set; }
    public int ContentLength { get; set; }
}

#endregion
