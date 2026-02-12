namespace CAF.Interfaces;

public interface ISessionService
{
    Task<Session?> GetByIdAsync(int id);

    Task<Session?> GetActiveSessionAsync();

    Task<List<Session>> GetAllSessionsAsync();

    Task<Session> CreateSessionAsync(string name);

    Task<Session> CreateSessionWithDuplicateTurnsAsync(string name, int sourceSessionId, int turnCount);

    Task<Session> UpdateSessionAsync(int id, string name, bool isActive);

    Task<bool> DeleteSessionAsync(int id);

    Task<bool> SetActiveSessionAsync(int id);
}