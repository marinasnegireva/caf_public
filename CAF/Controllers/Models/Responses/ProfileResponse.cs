namespace CAF.Controllers.Models.Responses;

public class ProfileResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? LastActivatedAt { get; set; }

    // Entity counts only - not full collections
    public int SystemMessagesCount { get; set; }

    public int SessionsCount { get; set; }
    public int SettingsCount { get; set; }
    public int FlagsCount { get; set; }
}