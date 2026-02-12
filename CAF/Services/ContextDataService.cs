namespace CAF.Services;

/// <summary>
/// Service for managing unified context data across all data types and availability mechanisms.
/// </summary>
public class ContextDataService(
    IDbContextFactory<GeneralDbContext> dbContextFactory,
    IProfileService profileService,
    ISemanticService semanticService,
    ILogger<ContextDataService> logger) : IContextDataService
{
    private readonly int _profileId = profileService.GetActiveProfileId();

    #region Basic CRUD

    public async Task<ContextData?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ContextData.FindAsync([id], cancellationToken);
    }

    public async Task<List<ContextData>> GetAllAsync(
        DataType? type = null,
        AvailabilityType? availability = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ContextData.Where(d => d.ProfileId == _profileId);

        if (type.HasValue)
            query = query.Where(d => d.Type == type.Value);

        if (availability.HasValue)
            query = query.Where(d => d.Availability == availability.Value);

        if (!includeArchived)
            query = query.Where(d => !d.IsArchived);

        return await query
            .OrderBy(d => d.Type)
            .ThenByDescending(d => d.UsedLastOnTurnId)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContextData> CreateAsync(ContextData data, CancellationToken cancellationToken = default)
    {
        if (!data.IsValidCombination())
        {
            throw new InvalidOperationException(
                $"Invalid combination: DataType.{data.Type} cannot have AvailabilityType.{data.Availability}");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        data.ProfileId = _profileId;
        data.CreatedAt = DateTime.UtcNow;

        db.ContextData.Add(data);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created ContextData {Id} ({Type}/{Availability}): {Name}",
            data.Id, data.Type, data.Availability, data.Name);

        return data;
    }

    public async Task<ContextData?> UpdateAsync(int id, ContextData data, CancellationToken cancellationToken = default)
    {
        if (!data.IsValidCombination())
        {
            throw new InvalidOperationException(
                $"Invalid combination: DataType.{data.Type} cannot have AvailabilityType.{data.Availability}");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.ContextData.FindAsync([id], cancellationToken);
        if (existing == null)
            return null;

        existing.Name = data.Name;
        existing.Content = data.Content;
        existing.Summary = data.Summary;
        existing.CoreFacts = data.CoreFacts;
        existing.Type = data.Type;
        existing.Availability = data.Availability;
        existing.IsUser = data.IsUser;
        existing.IsEnabled = data.IsEnabled;
        existing.UseNextTurnOnly = data.UseNextTurnOnly;
        existing.UseEveryTurn = data.UseEveryTurn;
        existing.TriggerKeywords = data.TriggerKeywords;
        existing.TriggerLookbackTurns = data.TriggerLookbackTurns;
        existing.TriggerMinMatchCount = data.TriggerMinMatchCount;
        existing.Display = data.Display;
        existing.SortOrder = data.SortOrder;
        existing.Description = data.Description;
        existing.Tags = data.Tags;
        existing.Notes = data.Notes;
        existing.Speaker = data.Speaker;
        existing.Subtype = data.Subtype;
        existing.Path = data.Path;
        existing.TokenCount = data.TokenCount;
        existing.TokenCountUpdatedAt = data.TokenCountUpdatedAt;
        existing.ModifiedAt = DateTime.UtcNow;
        existing.ModifiedBy = data.ModifiedBy;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated ContextData {Id}: {Name}", id, existing.Name);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([id], cancellationToken);
        if (data == null)
            return false;

        db.ContextData.Remove(data);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted ContextData {Id}: {Name}", id, data.Name);
        return true;
    }

    public async Task<bool> ArchiveAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([id], cancellationToken);
        if (data == null)
            return false;

        data.IsArchived = true;
        data.Availability = AvailabilityType.Archive;
        data.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([id], cancellationToken);
        if (data == null)
            return false;

        data.IsArchived = false;
        data.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> UpdateTokenCountAsync(int id, int tokenCount, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([id], cancellationToken);
        if (data == null)
            return false;

        data.TokenCount = tokenCount;
        data.TokenCountUpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated token count for ContextData {Id}: {TokenCount}", id, tokenCount);

        return true;
    }

    #endregion Basic CRUD

    #region Availability-Based Retrieval

    public async Task<List<ContextData>> GetAlwaysOnDataAsync(
        DataType? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ContextData
            .Where(d => d.ProfileId == _profileId &&
                       d.Availability == AvailabilityType.AlwaysOn &&
                       d.IsEnabled &&
                       !d.IsArchived);

        if (typeFilter.HasValue)
            query = query.Where(d => d.Type == typeFilter.Value);

        return await query
            .OrderBy(d => d.Type)
            .ThenBy(d => d.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ContextData>> GetActiveManualDataAsync(
        DataType? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.ContextData
            .Where(d => d.ProfileId == _profileId &&
                       d.Availability == AvailabilityType.Manual &&
                       d.IsEnabled &&
                       !d.IsArchived &&
                       (d.UseNextTurnOnly || d.UseEveryTurn));

        if (typeFilter.HasValue)
            query = query.Where(d => d.Type == typeFilter.Value);

        return await query
            .OrderBy(d => d.Type)
            .ThenBy(d => d.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContextData?> GetUserProfileAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        logger.LogDebug("GetUserProfileAsync: Looking for user profile with ProfileId={ProfileId}, Type=CharacterProfile, IsUser=true", _profileId);

        var userProfile = await db.ContextData
            .Where(d => d.ProfileId == _profileId &&
                       d.Type == DataType.CharacterProfile &&
                       d.IsUser &&
                       d.IsEnabled &&
                       !d.IsArchived)
            .FirstOrDefaultAsync(cancellationToken);

        if (userProfile == null)
        {
            logger.LogWarning("GetUserProfileAsync: No user profile found for ProfileId={ProfileId}. Check that: 1) Profile {ProfileId} is active, 2) A CharacterProfile with IsUser=true exists for this profile", _profileId, _profileId);

            // Additional diagnostic: check if ANY user profiles exist
            var anyUserProfiles = await db.ContextData
                .Where(d => d.Type == DataType.CharacterProfile && d.IsUser && d.IsEnabled && !d.IsArchived)
                .Select(d => new { d.Id, d.Name, d.ProfileId, d.IsUser })
                .ToListAsync(cancellationToken);

            if (anyUserProfiles.Any())
            {
                logger.LogInformation("Found {Count} user profiles in database but none match active ProfileId {ActiveProfileId}: {Profiles}",
                    anyUserProfiles.Count, _profileId, JsonSerializer.Serialize(anyUserProfiles));
            }
            else
            {
                logger.LogWarning("No user profiles (IsUser=true) found in entire database!");
            }
        }
        else
        {
            logger.LogDebug("GetUserProfileAsync: Found user profile '{Name}' (Id={Id}, ProfileId={ProfileId})",
                userProfile.Name, userProfile.Id, userProfile.ProfileId);
        }

        return userProfile;
    }

    #endregion Availability-Based Retrieval

    #region Trigger-Based Retrieval

    public async Task<List<ContextData>> GetTriggerDataAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ContextData
            .Where(d => d.ProfileId == _profileId &&
                       d.Availability == AvailabilityType.Trigger &&
                       d.IsEnabled &&
                       !d.IsArchived &&
                       !string.IsNullOrEmpty(d.TriggerKeywords))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ContextData>> EvaluateTriggersAsync(
        string recentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recentText))
            return [];

        var triggerData = await GetTriggerDataAsync(cancellationToken);
        var matchedData = new List<ContextData>();
        var lowerText = recentText.ToLowerInvariant();

        foreach (var data in triggerData)
        {
            var keywords = data.GetKeywordList();
            var matchCount = keywords.Count(keyword =>
                Regex.IsMatch(lowerText, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));

            if (matchCount >= data.TriggerMinMatchCount)
            {
                matchedData.Add(data);
                logger.LogDebug("Trigger matched for {Name} with {Count} keywords", data.Name, matchCount);
            }
        }

        return matchedData;
    }

    public async Task RecordTriggerActivationAsync(int dataId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data != null)
        {
            data.TriggerCount++;
            data.LastTriggeredAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion Trigger-Based Retrieval

    #region Semantic Search

    public async Task<List<ContextData>> SearchSemanticAsync(
        string query,
        DataType? typeFilter = null,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        return await semanticService.SearchAsync(query, _profileId, typeFilter, limit, cancellationToken);
    }

    public async Task UpdateEmbeddingAsync(int dataId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data == null || data.Availability != AvailabilityType.Semantic)
            return;

        await semanticService.EmbedAsync(data, cancellationToken);
        logger.LogInformation("Updated embedding for ContextData {Id}: {Name}", dataId, data.Name);
    }

    public async Task UpdateAllEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        await semanticService.SyncAllAsync(_profileId, cancellationToken);
        logger.LogInformation("Synced all embeddings for profile {ProfileId}", _profileId);
    }

    #endregion Semantic Search

    #region Manual Toggle Management

    public async Task<bool> SetUseNextTurnAsync(int dataId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data == null)
            return false;

        // Store previous availability if not already in Manual mode
        if (data.Availability != AvailabilityType.Manual)
        {
            data.PreviousAvailability = data.Availability;
            data.Availability = AvailabilityType.Manual;
        }

        data.UseNextTurnOnly = true;
        data.UseEveryTurn = false;
        data.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Set UseNextTurn for ContextData {Id}: {Name}", dataId, data.Name);
        return true;
    }

    public async Task<bool> SetUseEveryTurnAsync(int dataId, bool enabled, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data == null)
            return false;

        if (enabled)
        {
            // Store previous availability if not already in Manual mode
            if (data.Availability != AvailabilityType.Manual)
            {
                data.PreviousAvailability = data.Availability;
                data.Availability = AvailabilityType.Manual;
            }

            data.UseEveryTurn = true;
            data.UseNextTurnOnly = false;
        }
        else
        {
            data.UseEveryTurn = false;
            // If we have a previous availability stored, restore it
            if (data.PreviousAvailability.HasValue)
            {
                data.Availability = data.PreviousAvailability.Value;
                data.PreviousAvailability = null;
            }
        }

        data.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Set UseEveryTurn={Enabled} for ContextData {Id}: {Name}", enabled, dataId, data.Name);
        return true;
    }

    public async Task<bool> ClearManualFlagsAsync(int dataId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data == null)
            return false;

        data.UseNextTurnOnly = false;
        data.UseEveryTurn = false;

        // If we have a previous availability stored, restore it
        if (data.PreviousAvailability.HasValue)
        {
            data.Availability = data.PreviousAvailability.Value;
            data.PreviousAvailability = null;
            logger.LogInformation("Cleared manual flags and restored {Name} to {Availability}",
                data.Name, data.Availability);
        }
        else
        {
            logger.LogInformation("Cleared manual flags for {Name}", data.Name);
        }

        data.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task ProcessPostTurnAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Find all "use next turn only" entries that were used
        var usedNextTurn = await db.ContextData
            .Where(d => d.ProfileId == _profileId &&
                       d.Availability == AvailabilityType.Manual &&
                       d.UseNextTurnOnly)
            .ToListAsync(cancellationToken);

        foreach (var data in usedNextTurn)
        {
            data.UseNextTurnOnly = false;

            // Restore previous availability if stored
            if (data.PreviousAvailability.HasValue)
            {
                data.Availability = data.PreviousAvailability.Value;
                data.PreviousAvailability = null;
                logger.LogInformation("Restored {Name} to {Availability} after use-next-turn",
                    data.Name, data.Availability);
            }

            data.ModifiedAt = DateTime.UtcNow;
        }

        if (usedNextTurn.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Processed {Count} use-next-turn entries", usedNextTurn.Count);
        }
    }

    #endregion Manual Toggle Management

    #region Availability Changes

    public async Task<bool> ChangeAvailabilityAsync(
        int dataId,
        AvailabilityType newAvailability,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data == null)
            return false;

        // Temporarily set to check validity
        var oldAvailability = data.Availability;
        data.Availability = newAvailability;

        if (!data.IsValidCombination())
        {
            logger.LogWarning("Invalid combination: {Type} cannot have {Availability}",
                data.Type, newAvailability);
            return false;
        }

        // Clear manual toggle flags if not Manual
        if (newAvailability != AvailabilityType.Manual)
        {
            data.UseNextTurnOnly = false;
            data.UseEveryTurn = false;
            data.PreviousAvailability = null;
        }

        data.ModifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Changed ContextData {Id} availability from {Old} to {New}",
            dataId, oldAvailability, newAvailability);
        return true;
    }

    #endregion Availability Changes

    #region Data Type-Based Retrieval

    public async Task<List<ContextData>> GetActiveDataByTypeAsync(
        DataType type,
        string? triggerText = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ContextData>();
        var seenIds = new HashSet<int>();

        // Get AlwaysOn data for this type
        var alwaysOn = await GetAlwaysOnDataAsync(type, cancellationToken);
        foreach (var data in alwaysOn.Where(d => seenIds.Add(d.Id)))
            results.Add(data);

        // Get active Manual data for this type (if applicable for this type)
        if (CanHaveManualAvailability(type))
        {
            var manual = await GetActiveManualDataAsync(type, cancellationToken);
            foreach (var data in manual.Where(d => seenIds.Add(d.Id)))
                results.Add(data);
        }

        // Get triggered data for this type (if applicable and trigger text provided)
        if (CanHaveTriggerAvailability(type) && !string.IsNullOrWhiteSpace(triggerText))
        {
            var triggered = await EvaluateTriggersAsync(triggerText, cancellationToken);
            foreach (var data in triggered.Where(d => d.Type == type && seenIds.Add(d.Id)))
                results.Add(data);
        }

        return results;
    }

    public async Task<List<ContextData>> GetDataByTypeAndAvailabilityAsync(
        DataType type,
        AvailabilityType availability,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ContextData
            .Where(d => d.ProfileId == _profileId &&
                       d.Type == type &&
                       d.Availability == availability &&
                       d.IsEnabled &&
                       !d.IsArchived)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if a data type can have Manual availability
    /// Manual: Quote|Memory|Insight|CharacterProfile|Data
    /// </summary>
    private static bool CanHaveManualAvailability(DataType type) => type switch
    {
        DataType.Quote => true,
        DataType.Memory => true,
        DataType.Insight => true,
        DataType.CharacterProfile => true,
        DataType.Generic => true,
        DataType.PersonaVoiceSample => false,
        _ => false
    };

    /// <summary>
    /// Checks if a data type can have Trigger availability
    /// Trigger: CharacterProfile|Data|Memory|Insight
    /// </summary>
    private static bool CanHaveTriggerAvailability(DataType type) => type switch
    {
        DataType.CharacterProfile => true,
        DataType.Generic => true,
        DataType.Memory => true,
        DataType.Insight => true,
        DataType.Quote => false,
        DataType.PersonaVoiceSample => false,
        _ => false
    };

    /// <summary>
    /// Checks if a data type can have Semantic availability
    /// Semantic: Quote|Memory|Insight|PersonaVoiceSample
    /// </summary>
    private static bool CanHaveSemanticAvailability(DataType type) => type switch
    {
        DataType.Quote => true,
        DataType.Memory => true,
        DataType.Insight => true,
        DataType.PersonaVoiceSample => true,
        DataType.CharacterProfile => false,
        DataType.Generic => false,
        _ => false
    };

    #endregion Data Type-Based Retrieval

    #region Usage Tracking

    public async Task RecordUsageAsync(int dataId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var data = await db.ContextData.FindAsync([dataId], cancellationToken);
        if (data != null)
        {
            data.UsageCount++;
            data.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RecordUsageBatchAsync(IEnumerable<int> dataIds, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ids = dataIds.ToList();
        var dataItems = await db.ContextData
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(cancellationToken);

        foreach (var data in dataItems)
        {
            data.UsageCount++;
            data.LastUsedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    #endregion Usage Tracking
}