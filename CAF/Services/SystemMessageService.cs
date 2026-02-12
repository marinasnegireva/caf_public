namespace CAF.Services;

public class SystemMessageService(
    IDbContextFactory<GeneralDbContext> dbContextFactory,
    ILogger<SystemMessageService> logger,
    IProfileService profileService,
    IGeminiClient geminiClient) : ISystemMessageService
{
    private readonly int profileId = profileService.GetActiveProfileId();

    public async Task<List<SystemMessage>> GetAllAsync(
        SystemMessage.SystemMessageType? type = null,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.SystemMessages.Where(m => m.ProfileId == profileId);

        if (type.HasValue)
            query = query.Where(m => m.Type == type.Value);

        if (!includeArchived)
            query = query.Where(m => !m.IsArchived);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<SystemMessage?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SystemMessages.FindAsync([id], cancellationToken);
    }

    public async Task<List<SystemMessage>> GetByIdsAsync(List<int> ids, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SystemMessages
            .Where(m => ids.Contains(m.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SystemMessage>> GetByTypeAsync(
        SystemMessage.SystemMessageType type,
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.SystemMessages.Where(m => m.Type == type);

        if (!includeArchived)
            query = query.Where(m => !m.IsArchived);

        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<SystemMessage> CreateAsync(SystemMessage message, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        message.CreatedAt = DateTime.UtcNow;
        message.Version = 1;
        message.ProfileId = profileId;

        dbContext.SystemMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created system message {Id} of type {Type}", message.Id, message.Type);

        return message;
    }

    public async Task<SystemMessage?> UpdateAsync(int id, SystemMessage message, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await dbContext.SystemMessages.FindAsync([id], cancellationToken);
        if (existing == null)
            return null;

        // Get the root parent ID
        var rootParentId = existing.ParentId ?? existing.Id;

        // Find the highest version number in the version chain
        var maxVersion = await dbContext.SystemMessages
            .Where(m => m.Id == rootParentId || m.ParentId == rootParentId)
            .MaxAsync(m => m.Version, cancellationToken);

        // Deactivate all previous versions in the chain first
        var allVersions = await dbContext.SystemMessages
            .Where(m => m.Id == rootParentId || m.ParentId == rootParentId)
            .ToListAsync(cancellationToken);

        foreach (var version in allVersions)
        {
            version.IsActive = false;
        }

        // Create a new version with updated content - this should be the active one
        var newVersion = new SystemMessage
        {
            Name = message.Name,
            Content = message.Content,
            Type = message.Type,
            ParentId = rootParentId,
            Version = maxVersion + 1,
            IsActive = message.IsActive,
            Description = message.Description,
            Tags = message.Tags ?? [],
            Notes = message.Notes,
            ProfileId = existing.ProfileId, // Preserve ProfileId from existing message
            CreatedBy = message.ModifiedBy,
            ModifiedBy = message.ModifiedBy,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        // Add the new version
        dbContext.SystemMessages.Add(newVersion);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created version {Version} of system message {ParentId} via update operation", newVersion.Version, rootParentId);

        return newVersion;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await dbContext.SystemMessages.FindAsync([id], cancellationToken);
        if (message == null)
            return false;

        // Delete all versions
        var versions = await dbContext.SystemMessages
            .Where(m => m.ParentId == id || m.Id == id)
            .ToListAsync(cancellationToken);

        dbContext.SystemMessages.RemoveRange(versions);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted system message {Id} and {Count} versions", id, versions.Count);
        return true;
    }

    public async Task<SystemMessage?> CreateNewVersionAsync(int sourceId, string? modifiedBy = null, CancellationToken cancellationToken = default)
    {
        if (sourceId <= 0)
            return null;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var source = await dbContext.SystemMessages.FindAsync([sourceId], cancellationToken);
        if (source == null)
            return null;

        // Get the root parent ID
        var rootParentId = source.ParentId ?? source.Id;

        // Find the highest version number
        var maxVersion = await dbContext.SystemMessages
            .Where(m => m.Id == rootParentId || m.ParentId == rootParentId)
            .MaxAsync(m => m.Version, cancellationToken);

        var newVersion = new SystemMessage
        {
            Name = source.Name,
            Content = source.Content,
            Type = source.Type,
            ParentId = rootParentId,
            Version = maxVersion + 1,
            IsActive = false, // New versions start inactive
            Description = source.Description,
            Tags = [.. source.Tags],
            CreatedBy = modifiedBy,
            CreatedAt = DateTime.UtcNow,
            ProfileId = source.ProfileId
        };

        dbContext.SystemMessages.Add(newVersion);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created version {Version} of system message {ParentId}", newVersion.Version, rootParentId);
        return newVersion;
    }

    public async Task<List<SystemMessage>> GetVersionHistoryAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await dbContext.SystemMessages.FindAsync([id], cancellationToken);
        if (message == null)
            return [];

        var rootParentId = message.ParentId ?? message.Id;

        return await dbContext.SystemMessages
            .Where(m => m.Id == rootParentId || m.ParentId == rootParentId)
            .OrderBy(m => m.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<SystemMessage?> GetLatestVersionAsync(int parentId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SystemMessages
            .Where(m => m.Id == parentId || m.ParentId == parentId)
            .OrderByDescending(m => m.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> SetActiveVersionAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await dbContext.SystemMessages.FindAsync([id], cancellationToken);
        if (message == null)
            return false;

        var rootParentId = message.ParentId ?? message.Id;

        // Deactivate all versions and activate the specified one
        var allVersions = await dbContext.SystemMessages
            .Where(m => m.Id == rootParentId || m.ParentId == rootParentId)
            .ToListAsync(cancellationToken);

        foreach (var version in allVersions)
        {
            version.IsActive = version.Id == id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Set version {Id} as active", id);
        return true;
    }

    public async Task<SystemMessage?> GetActivePersonaAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Get the active profile (if any)
        var activeProfile = await dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive, cancellationToken);

        var query = dbContext.SystemMessages
            .Where(m => m.Type == SystemMessage.SystemMessageType.Persona &&
                       m.IsActive &&
                       !m.IsArchived);

        // If there's an active profile, include profile-specific entities
        // If there's no active profile, only include global entities
        query = activeProfile != null ? query.Where(m => m.ProfileId == activeProfile.Id) : query.Where(m => m.ProfileId == 0);

        // Get all matching messages, group by Name, and take the latest one per Name
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return messages
            .GroupBy(m => m.Name)
            .Select(g => g.First())
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<List<SystemMessage>> GetActivePerceptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Get the active profile (if any)
        var activeProfile = await dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive, cancellationToken);

        var query = dbContext.SystemMessages
            .Where(m => m.Type == SystemMessage.SystemMessageType.Perception &&
                       m.IsActive &&
                       !m.IsArchived);

        query = activeProfile != null ? query.Where(m => m.ProfileId == activeProfile.Id) : query.Where(m => m.ProfileId == 0);

        // Get all matching messages, group by Name, and take the latest one per Name
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. messages
            .GroupBy(m => m.Name)
            .Select(g => g.First())
            .OrderByDescending(m => m.CreatedAt)];
    }

    public async Task<List<SystemMessage>> GetActiveTechnicalMessagesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        // Get the active profile (if any)
        var activeProfile = await dbContext.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive, cancellationToken);

        var query = dbContext.SystemMessages
            .Where(m => m.Type == SystemMessage.SystemMessageType.Technical &&
                       m.IsActive &&
                       !m.IsArchived);

        query = activeProfile != null ? query.Where(m => m.ProfileId == activeProfile.Id) : query.Where(m => m.ProfileId == 0);

        // Get all matching messages, group by Name, and take the latest one per Name
        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return [.. messages
            .GroupBy(m => m.Name)
            .Select(g => g.First())
            .OrderByDescending(m => m.CreatedAt)];
    }

    public async Task<string> BuildCompleteSystemMessageAsync(int? personaId = null, CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();

        // 1. Get persona (either specified or active)
        SystemMessage? persona = null;
        if (personaId.HasValue)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            persona = await dbContext.SystemMessages.FindAsync([personaId.Value], cancellationToken);
        }
        else
        {
            persona = await GetActivePersonaAsync(cancellationToken);
        }

        if (persona != null)
        {
            parts.Add($"# PERSONA\n{persona.Content}");
        }

        // 2. Get active perception messages
        var perceptions = await GetActivePerceptionsAsync(cancellationToken);
        foreach (var perception in perceptions)
        {
            parts.Add($"# PERCEPTION\n{perception.Content}");
        }

        // 4. Get active technical messages
        var technicals = await GetActiveTechnicalMessagesAsync(cancellationToken);
        foreach (var technical in technicals)
        {
            parts.Add($"# TECHNICAL: {technical.Name}\n{technical.Content}");
        }

        return string.Join("\n\n---\n\n", parts);
    }

    public async Task<string> BuildPersonaOnlySystemMessageAsync(int? personaId = null, CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();

        // Get persona (either specified or active)
        SystemMessage? persona = null;
        if (personaId.HasValue)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            persona = await dbContext.SystemMessages.FindAsync([personaId.Value], cancellationToken);
        }
        else
        {
            persona = await GetActivePersonaAsync(cancellationToken);
        }

        if (persona != null)
        {
            parts.Add($"# PERSONA\n{persona.Content}");
        }

        return string.Join("\n\n---\n\n", parts);
    }

    public async Task<bool> ArchiveAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await dbContext.SystemMessages.FindAsync([id], cancellationToken);
        if (message == null)
            return false;

        message.IsArchived = true;
        message.ModifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> RestoreAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var message = await dbContext.SystemMessages.FindAsync([id], cancellationToken);
        if (message == null)
            return false;

        message.IsArchived = false;
        message.ModifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<SystemMessage?> GetTechnicalMessageByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SystemMessages
            .Where(m => m.Type == SystemMessage.SystemMessageType.Technical &&
                       m.Name == name &&
                       m.IsActive &&
                       !m.IsArchived)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures all required default system messages exist for the current profile.
    /// Creates missing messages using templates from DefaultSystemMessageRegistry.
    /// This should be called on application startup.
    /// </summary>
    public async Task EnsureDefaultsInitializedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ensuring default system messages exist for profile {ProfileId}", profileId);

        // Ensure technical messages exist
        foreach (var (name, defaultContent) in DefaultSystemMessageRegistry.TechnicalMessages)
        {
            await EnsureSystemMessageExistsAsync(
                name,
                defaultContent,
                SystemMessage.SystemMessageType.Technical,
                isActive: true,
                cancellationToken);
        }

        // Ensure perception messages exist
        foreach (var (name, defaultContent) in DefaultSystemMessageRegistry.PerceptionMessages)
        {
            await EnsureSystemMessageExistsAsync(
                name,
                defaultContent,
                SystemMessage.SystemMessageType.Perception,
                isActive: true,
                cancellationToken);
        }

        logger.LogInformation("Default system messages initialization complete for profile {ProfileId}", profileId);
    }

    /// <summary>
    /// Checks if a system message exists for the current profile, and creates it if missing.
    /// The defaultContent is used as a template/fallback only when creating a new message.
    /// </summary>
    private async Task EnsureSystemMessageExistsAsync(
        string name,
        string defaultContent,
        SystemMessage.SystemMessageType type,
        bool isActive,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await dbContext.SystemMessages
            .AnyAsync(m => m.Type == type &&
                          m.Name == name &&
                          m.ProfileId == profileId,
                      cancellationToken);

        if (exists)
        {
            logger.LogDebug("System message '{Name}' (type: {Type}) already exists for profile {ProfileId}",
                name, type, profileId);
            return;
        }

        dbContext.SystemMessages.Add(new SystemMessage
        {
            Name = name,
            Content = defaultContent,
            Type = type,
            IsActive = isActive,
            IsArchived = false,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Created default system message '{Name}' (type: {Type}) for profile {ProfileId}",
            name, type, profileId);
    }

    /// <summary>
    /// Calculate token count using Gemini's native API.
    /// Falls back to approximation (1 token ≈ 4 characters) if API call fails.
    /// </summary>
    public async Task<int> CalculateTokenCountAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        try
        {
            var tokenCount = await geminiClient.CountTokensAsync(text, cancellationToken);

            if (tokenCount < 0)
            {
                logger.LogWarning("Gemini token counting API failed, falling back to approximation");
                tokenCount = (int)Math.Ceiling(text.Length / 4.0);
            }

            return tokenCount;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error calling Gemini token counting API, using approximation");
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}

/// <summary>
/// Constants for perception message names
/// </summary>
public static class PerceptionMessageNames
{
    public const string EpistemicPerception = "epistemic perception";
}

/// <summary>
/// Registry of default system messages that should exist for each profile.
/// These are templates/fallbacks that will be created if missing.
/// </summary>
public static class DefaultSystemMessageRegistry
{
    /// <summary>
    /// Technical messages with their default content
    /// </summary>
    public static readonly Dictionary<string, string> TechnicalMessages = new()
    {
        [ConversationConstants.TechnicalMessages.TurnStripperInstructions] = DefaultTechnicalMessages.TurnStripperInstructions,
        [ConversationConstants.TechnicalMessages.MemorySummaryInstructions] = DefaultTechnicalMessages.MemorySummaryInstructions,
        [ConversationConstants.TechnicalMessages.MemoryCoreFactsInstructions] = DefaultTechnicalMessages.MemoryCoreFactsInstructions,
        [ConversationConstants.TechnicalMessages.QuoteQueryTransformer] = DefaultTechnicalMessages.QuoteQueryTransformer,
        [ConversationConstants.TechnicalMessages.QuoteMapper] = DefaultTechnicalMessages.QuoteMapper
    };

    /// <summary>
    /// Perception messages with their default content
    /// </summary>
    public static readonly Dictionary<string, string> PerceptionMessages = new()
    {
        [PerceptionMessageNames.EpistemicPerception] = DefaultTechnicalMessages.EpistemicPerception
    };
}