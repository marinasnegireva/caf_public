namespace CAF.Services;

public class FlagService(GeneralDbContext context, ILogger<FlagService> logger, IProfileService profileService) : IFlagService
{
    private readonly int profileId = profileService.GetActiveProfileId();

    public async Task<List<Flag>> GetAllAsync(bool? active = null, CancellationToken cancellationToken = default)
    {
        var query = context.Flags.Where(f => f.ProfileId == profileId);

        if (active.HasValue)
        {
            query = query.Where(f => f.Active == active.Value || f.Constant);
        }

        return await query
            .OrderByDescending(f => f.Active)
            .ThenByDescending(f => f.LastUsedAt ?? f.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Flag?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Flags.FindAsync([id], cancellationToken);
    }

    public async Task<Flag> CreateAsync(string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Flag value cannot be empty", nameof(value));
        }

        var flag = new Flag
        {
            Value = value.Trim(),
            Active = true,
            Constant = false,
            CreatedAt = DateTime.UtcNow,
            ProfileId = profileId
        };

        context.Flags.Add(flag);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created flag {FlagId} with value: {Value}", flag.Id, flag.Value);

        return flag;
    }

    public async Task<Flag?> UpdateAsync(int id, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Flag value cannot be empty", nameof(value));
        }

        var flag = await GetByIdAsync(id, cancellationToken);
        if (flag == null)
        {
            return null;
        }

        flag.Value = value.Trim();
        flag.ModifiedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated flag {FlagId} with value: {Value}", flag.Id, flag.Value);

        return flag;
    }

    public async Task<Flag?> ToggleActiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var flag = await GetByIdAsync(id, cancellationToken);
        if (flag == null)
        {
            return null;
        }

        flag.Active = !flag.Active;
        flag.ModifiedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Toggled flag {FlagId} active to: {Active}", flag.Id, flag.Active);

        return flag;
    }

    public async Task<Flag?> ToggleConstantAsync(int id, CancellationToken cancellationToken = default)
    {
        var flag = await GetByIdAsync(id, cancellationToken);
        if (flag == null)
        {
            return null;
        }

        flag.Constant = !flag.Constant;
        flag.ModifiedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Toggled flag {FlagId} constant to: {Constant}", flag.Id, flag.Constant);

        return flag;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var flag = await GetByIdAsync(id, cancellationToken);
        if (flag == null)
        {
            return false;
        }

        context.Flags.Remove(flag);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted flag {FlagId}", flag.Id);

        return true;
    }

    public async Task ResetNonConstantFlagsAsync(CancellationToken cancellationToken = default)
    {
        var nonConstantFlags = await context.Flags
            .Where(f => f.Active && !f.Constant)
            .ToListAsync(cancellationToken);

        foreach (var flag in nonConstantFlags)
        {
            flag.Active = false;
            flag.ModifiedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Reset {Count} non-constant flags", nonConstantFlags.Count);
    }
}