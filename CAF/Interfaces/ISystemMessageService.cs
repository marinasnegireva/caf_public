namespace CAF.Interfaces;

public interface ISystemMessageService
{
    // Basic CRUD
    Task<List<SystemMessage>> GetAllAsync(SystemMessage.SystemMessageType? type = null, bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<SystemMessage?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<List<SystemMessage>> GetByIdsAsync(List<int> ids, CancellationToken cancellationToken = default);

    Task<List<SystemMessage>> GetByTypeAsync(SystemMessage.SystemMessageType type, bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<SystemMessage> CreateAsync(SystemMessage message, CancellationToken cancellationToken = default);

    Task<SystemMessage?> UpdateAsync(int id, SystemMessage message, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    // Version management
    Task<SystemMessage?> CreateNewVersionAsync(int sourceId, string? modifiedBy = null, CancellationToken cancellationToken = default);

    Task<List<SystemMessage>> GetVersionHistoryAsync(int id, CancellationToken cancellationToken = default);

    Task<SystemMessage?> GetLatestVersionAsync(int parentId, CancellationToken cancellationToken = default);

    Task<bool> SetActiveVersionAsync(int id, CancellationToken cancellationToken = default);

    // Get active messages
    Task<SystemMessage?> GetActivePersonaAsync(CancellationToken cancellationToken = default);

    Task<List<SystemMessage>> GetActivePerceptionsAsync(CancellationToken cancellationToken = default);

    Task<List<SystemMessage>> GetActiveTechnicalMessagesAsync(CancellationToken cancellationToken = default);

    // Build complete system message
    Task<string> BuildCompleteSystemMessageAsync(int? personaId = null, CancellationToken cancellationToken = default);

    Task<string> BuildPersonaOnlySystemMessageAsync(int? personaId = null, CancellationToken cancellationToken = default);

    // Archive/Restore
    Task<bool> ArchiveAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(int id, CancellationToken cancellationToken = default);

    Task EnsureDefaultsInitializedAsync(CancellationToken cancellationToken = default);

    // Technical messages
    Task<SystemMessage?> GetTechnicalMessageByNameAsync(string name, CancellationToken cancellationToken = default);

    // Token counting
    Task<int> CalculateTokenCountAsync(string text, CancellationToken cancellationToken = default);
}
