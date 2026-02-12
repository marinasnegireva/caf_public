namespace CAF.Interfaces;

public interface IProfileService
{
    Task<List<ProfileResponse>> GetAllProfilesAsync();

    Task<Profile> CreateProfileAsync(Profile profile);

    Task<Profile> UpdateProfileAsync(Profile profile);

    Task DeleteProfileAsync(int id);

    Task<Profile> ActivateProfileAsync(int id);

    Task<Profile> DuplicateProfileAsync(int sourceId, string newName);

    Task<int> MoveEntitiesToProfileAsync(int profileId, List<int>? systemMessageIds = null);

    Task<Profile> CreateDefaultProfileAsync(string name = "Default Profile", CancellationToken cancellationToken = default);

    Task<int> GetActiveProfileIdAsync();

    int GetActiveProfileId();
}