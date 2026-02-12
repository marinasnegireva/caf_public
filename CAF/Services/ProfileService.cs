namespace CAF.Services;

public class ProfileService(GeneralDbContext context, ILogger<ProfileService> logger) : IProfileService
{
    public async Task<List<ProfileResponse>> GetAllProfilesAsync()
    {
        return await context.Profiles
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .Select(p => new ProfileResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Color = p.Color,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt,
                ModifiedAt = p.ModifiedAt,
                LastActivatedAt = p.LastActivatedAt,
                SystemMessagesCount = p.SystemMessages.Count,
                SessionsCount = p.Sessions.Count,
                SettingsCount = p.Settings.Count,
                FlagsCount = p.Flags.Count
            })
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetActiveProfileIdAsync()
    {
        return (await context.Profiles.FirstOrDefaultAsync(p => p.IsActive))?.Id ?? 0;
    }

    public int GetActiveProfileId()
    {
        return context.Profiles.FirstOrDefault(p => p.IsActive)?.Id ?? 0;
    }

    public async Task<Profile> CreateProfileAsync(Profile profile)
    {
        profile.CreatedAt = DateTime.UtcNow;
        context.Profiles.Add(profile);
        await context.SaveChangesAsync();

        logger.LogInformation("Created profile {ProfileId} '{ProfileName}'", profile.Id, profile.Name);

        return profile;
    }

    public async Task<Profile> UpdateProfileAsync(Profile profile)
    {
        var existing = await context.Profiles.FindAsync(profile.Id) ?? throw new InvalidOperationException($"Profile {profile.Id} not found");

        existing.Name = profile.Name;
        existing.Description = profile.Description;
        existing.Color = profile.Color;
        existing.ModifiedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("Updated profile {ProfileId} '{ProfileName}'", profile.Id, profile.Name);

        return existing;
    }

    public async Task DeleteProfileAsync(int id)
    {
        var profile = await context.Profiles.FindAsync(id) ?? throw new InvalidOperationException($"Profile {id} not found");

        if (profile.IsActive)
        {
            throw new InvalidOperationException("Cannot delete the active profile. Please activate another profile first.");
        }

        // Set all related entities' ProfileId to 0 (global) instead of deleting them
        await context.ContextData
            .Where(cd => cd.ProfileId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(cd => cd.ProfileId, 0));

        await context.SystemMessages
            .Where(sm => sm.ProfileId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(sm => sm.ProfileId, 0));

        await context.Sessions
            .Where(s => s.ProfileId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.ProfileId, 0));

        await context.Settings
            .Where(s => s.ProfileId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.ProfileId, 0));

        await context.Flags
            .Where(f => f.ProfileId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.ProfileId, 0));

        context.Profiles.Remove(profile);
        await context.SaveChangesAsync();

        logger.LogInformation("Deleted profile {ProfileId} '{ProfileName}'", id, profile.Name);
    }

    public async Task<Profile> ActivateProfileAsync(int id)
    {
        var profile = await context.Profiles.FindAsync(id) ?? throw new InvalidOperationException($"Profile {id} not found");

        // Deactivate all profiles
        await context.Profiles
            .Where(p => p.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));

        // Activate the selected profile
        profile.IsActive = true;
        profile.LastActivatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        logger.LogInformation("Activated profile {ProfileId} '{ProfileName}'", id, profile.Name);

        return profile;
    }

    public async Task<Profile> DuplicateProfileAsync(int sourceId, string newName)
    {
        var source = await context.Profiles
            .Include(p => p.SystemMessages)
            .FirstOrDefaultAsync(p => p.Id == sourceId) ?? throw new InvalidOperationException($"Source profile {sourceId} not found");

        var newProfile = new Profile
        {
            Name = newName,
            Description = $"Duplicated from {source.Name}",
            Color = source.Color,
            CreatedAt = DateTime.UtcNow
        };

        context.Profiles.Add(newProfile);
        await context.SaveChangesAsync();

        // Duplicate all related entities
        var systemMessageIds = source.SystemMessages.Select(sm => sm.Id).ToList();

        await MoveEntitiesToProfileAsync(newProfile.Id, systemMessageIds);

        logger.LogInformation("Duplicated profile {SourceId} to new profile {NewId} '{NewName}'",
            sourceId, newProfile.Id, newName);

        return newProfile;
    }

    public async Task<int> MoveEntitiesToProfileAsync(
        int profileId,
        List<int>? systemMessageIds = null)
    {
        var profile = await context.Profiles.FindAsync(profileId) ?? throw new InvalidOperationException($"Profile {profileId} not found");

        var count = 0;

        if (systemMessageIds != null && systemMessageIds.Count != 0)
        {
            count += await context.SystemMessages
                .Where(sm => systemMessageIds.Contains(sm.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(sm => sm.ProfileId, profileId));
        }

        logger.LogInformation("Moved {Count} entities to profile {ProfileId}", count, profileId);

        return count;
    }

    public async Task<Profile> CreateDefaultProfileAsync(string name = "Default Profile", CancellationToken cancellationToken = default)
    {
        var profile = new Profile
        {
            Name = name,
            Description = "Default profile containing all existing entities",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastActivatedAt = DateTime.UtcNow
        };

        context.Profiles.Add(profile);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created default profile {ProfileId}",
            profile.Id);

        return profile;
    }
}