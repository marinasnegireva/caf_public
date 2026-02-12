namespace CAF.Interfaces;

/// <summary>
/// Service for managing unified context data across all data types and availability mechanisms.
/// </summary>
public interface IContextDataService
{
    #region Basic CRUD

    Task<ContextData?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<List<ContextData>> GetAllAsync(DataType? type = null, AvailabilityType? availability = null, bool includeArchived = false, CancellationToken cancellationToken = default);

    Task<ContextData> CreateAsync(ContextData data, CancellationToken cancellationToken = default);

    Task<ContextData?> UpdateAsync(int id, ContextData data, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ArchiveAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> RestoreAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> UpdateTokenCountAsync(int id, int tokenCount, CancellationToken cancellationToken = default);

    #endregion Basic CRUD

    #region Availability-Based Retrieval

    /// <summary>
    /// Gets all AlwaysOn data for the current profile
    /// </summary>
    Task<List<ContextData>> GetAlwaysOnDataAsync(DataType? typeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Manual data that should be included (UseEveryTurn or UseNextTurnOnly)
    /// </summary>
    Task<List<ContextData>> GetActiveManualDataAsync(DataType? typeFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the user's character profile (IsUser = true, always loaded)
    /// </summary>
    Task<ContextData?> GetUserProfileAsync(CancellationToken cancellationToken = default);

    #endregion Availability-Based Retrieval

    #region Trigger-Based Retrieval

    /// <summary>
    /// Gets all data with Trigger availability for evaluation
    /// </summary>
    Task<List<ContextData>> GetTriggerDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates triggers against recent text and returns matching data
    /// </summary>
    Task<List<ContextData>> EvaluateTriggersAsync(string recentText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a trigger was activated
    /// </summary>
    Task RecordTriggerActivationAsync(int dataId, CancellationToken cancellationToken = default);

    #endregion Trigger-Based Retrieval

    #region Semantic Search

    /// <summary>
    /// Gets data via semantic similarity search
    /// </summary>
    Task<List<ContextData>> SearchSemanticAsync(string query, DataType? typeFilter = null, int limit = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the vector embedding for a data entry
    /// </summary>
    Task UpdateEmbeddingAsync(int dataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch updates embeddings for all Semantic availability data
    /// </summary>
    Task UpdateAllEmbeddingsAsync(CancellationToken cancellationToken = default);

    #endregion Semantic Search

    #region Manual Toggle Management

    /// <summary>
    /// Sets a data entry to be used on the next turn only, then reverts to previous availability
    /// </summary>
    Task<bool> SetUseNextTurnAsync(int dataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a data entry to be used on every turn until toggled off
    /// </summary>
    Task<bool> SetUseEveryTurnAsync(int dataId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears both UseNextTurnOnly and UseEveryTurn flags and restores previous availability if stored
    /// </summary>
    Task<bool> ClearManualFlagsAsync(int dataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes "use next turn" entries after a turn completes - reverts them to previous availability
    /// </summary>
    Task ProcessPostTurnAsync(CancellationToken cancellationToken = default);

    #endregion Manual Toggle Management

    #region Availability Changes

    /// <summary>
    /// Changes the availability type of a data entry, validating the combination is allowed
    /// </summary>
    Task<bool> ChangeAvailabilityAsync(int dataId, AvailabilityType newAvailability, CancellationToken cancellationToken = default);

    #endregion Availability Changes

    #region Data Type-Based Retrieval

    /// <summary>
    /// Gets all active data for a specific data type across all applicable availability mechanisms.
    /// Combines AlwaysOn, Manual (if active), Trigger (if matched), but NOT Semantic.
    /// </summary>
    Task<List<ContextData>> GetActiveDataByTypeAsync(
        DataType type,
        string? triggerText = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets data for a specific type filtered by availability
    /// </summary>
    Task<List<ContextData>> GetDataByTypeAndAvailabilityAsync(
        DataType type,
        AvailabilityType availability,
        CancellationToken cancellationToken = default);

    #endregion Data Type-Based Retrieval

    #region Usage Tracking

    /// <summary>
    /// Records that data was loaded into context
    /// </summary>
    Task RecordUsageAsync(int dataId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records usage for multiple data entries
    /// </summary>
    Task RecordUsageBatchAsync(IEnumerable<int> dataIds, CancellationToken cancellationToken = default);

    #endregion Usage Tracking
}