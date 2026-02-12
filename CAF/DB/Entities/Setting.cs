namespace CAF.DB.Entities;

public class Setting
{
    public long Id { get; set; }

    /// <summary>
    /// Setting name. Can have duplicates across different profiles.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Link to a profile for profile-specific settings. 0 for global settings.
    /// </summary>
    public int ProfileId { get; set; } = 0;

    public Profile? Profile { get; set; }
}