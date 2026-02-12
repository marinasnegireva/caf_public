namespace CAF.Services;

public class SessionService(GeneralDbContext context, IProfileService profileService) : ISessionService
{
    private async Task<int> GetProfileIdAsync()
    {
        return await profileService.GetActiveProfileIdAsync();
    }

    public async Task<Session?> GetByIdAsync(int id)
    {
        var session = await context.Sessions
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session?.Turns != null)
        {
            session.Turns = [.. session.Turns.OrderBy(t => t.CreatedAt)];
        }

        return session;
    }

    public async Task<Session?> GetActiveSessionAsync()
    {
        var profileId = await GetProfileIdAsync();
        var session = await context.Sessions
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.IsActive && s.ProfileId == profileId);

        if (session?.Turns != null)
        {
            session.Turns = [.. session.Turns.OrderBy(t => t.CreatedAt)];
        }

        return session;
    }

    public async Task<List<Session>> GetAllSessionsAsync()
    {
        var profileId = await GetProfileIdAsync();
        var query = context.Sessions
            .Include(s => s.Turns)
            .AsQueryable();

        query = query.Where(s => s.ProfileId == profileId);

        return await query

            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<Session> CreateSessionAsync(string name)
    {
        var profileId = await GetProfileIdAsync();
        var maxNumber = await context.Sessions.MaxAsync(s => (int?)s.Number) ?? 0;

        var session = new Session
        {
            Number = maxNumber + 1,
            Name = name,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ProfileId = profileId
        };

        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        return session;
    }

    public async Task<Session> CreateSessionWithDuplicateTurnsAsync(string name, int sourceSessionId, int turnCount)
    {
        var profileId = await GetProfileIdAsync();

        // Get source session with turns
        var sourceSession = await context.Sessions
            .Include(s => s.Turns)
            .FirstOrDefaultAsync(s => s.Id == sourceSessionId);

        if (sourceSession == null)
        {
            throw new KeyNotFoundException($"Source session with id {sourceSessionId} not found");
        }

        // Create new session
        var maxNumber = await context.Sessions.MaxAsync(s => (int?)s.Number) ?? 0;
        var newSession = new Session
        {
            Number = maxNumber + 1,
            Name = name,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            ProfileId = profileId
        };

        context.Sessions.Add(newSession);
        await context.SaveChangesAsync(); // Save to get the new session ID

        // Get the last N turns from source session
        var turnsToClone = sourceSession.Turns
            .OrderByDescending(t => t.CreatedAt)
            .Where(t => t.Accepted)
            .Take(turnCount)
            .OrderBy(t => t.CreatedAt) // Restore chronological order
            .ToList();

        // Clone the turns to the new session
        foreach (var sourceTurn in turnsToClone)
        {
            var newTurn = new Turn
            {
                Input = sourceTurn.Input,
                JsonInput = sourceTurn.JsonInput,
                Response = sourceTurn.Response,
                StrippedTurn = sourceTurn.StrippedTurn,
                Accepted = sourceTurn.Accepted,
                SessionId = newSession.Id,
                CreatedAt = DateTime.UtcNow
            };

            context.Turns.Add(newTurn);
        }

        await context.SaveChangesAsync();

        // Reload session with turns to return complete object
        return await GetByIdAsync(newSession.Id)
            ?? throw new InvalidOperationException("Failed to retrieve newly created session");
    }

    public async Task<Session> UpdateSessionAsync(int id, string name, bool isActive)
    {
        var profileId = await GetProfileIdAsync();
        var session = await context.Sessions.FindAsync(id) ?? throw new KeyNotFoundException($"Session with id {id} not found");

        session.Name = name;
        session.IsActive = isActive;
        session.ModifiedAt = DateTime.UtcNow;
        session.ProfileId = profileId;

        await context.SaveChangesAsync();

        return session;
    }

    public async Task<bool> DeleteSessionAsync(int id)
    {
        var session = await context.Sessions.FindAsync(id);
        if (session == null)
        {
            return false;
        }

        context.Sessions.Remove(session);
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SetActiveSessionAsync(int id)
    {
        var targetSession = await context.Sessions.FindAsync(id);
        if (targetSession == null)
        {
            return false;
        }

        var allSessions = await context.Sessions.ToListAsync();
        foreach (var session in allSessions)
        {
            session.IsActive = session.Id == id;
            if (session.Id == id)
            {
                session.ModifiedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();

        return true;
    }
}