namespace CAF.Interfaces;

public interface IFlagService
{
    Task<List<Flag>> GetAllAsync(bool? active = null, CancellationToken cancellationToken = default);

    Task<Flag?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Flag> CreateAsync(string value, CancellationToken cancellationToken = default);

    Task<Flag?> UpdateAsync(int id, string value, CancellationToken cancellationToken = default);

    Task<Flag?> ToggleActiveAsync(int id, CancellationToken cancellationToken = default);

    Task<Flag?> ToggleConstantAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task ResetNonConstantFlagsAsync(CancellationToken cancellationToken = default);
}