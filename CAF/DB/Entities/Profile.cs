namespace CAF.DB.Entities;

/// <summary>
/// Represents a collection of related contexts, triggers, memories, and system messages.
/// Allows switching between different configurations for different scenarios or characters.
/// </summary>
public class Profile
{
    public int Id { get; set; }

    /// <summary>
    /// Display name for this profile (e.g., "Zorasis - Main Timeline", "Testing Profile")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this profile contains
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is the currently active profile
    /// </summary>
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Optional color or icon for UI display
    /// </summary>
    public string? Color { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public DateTime? LastActivatedAt { get; set; }

    /// <summary>
    /// Navigation properties
    /// </summary>
    public List<SystemMessage> SystemMessages { get; set; } = [];

    public List<Session> Sessions { get; set; } = [];
    public List<Setting> Settings { get; set; } = [];
    public List<Flag> Flags { get; set; } = [];
}