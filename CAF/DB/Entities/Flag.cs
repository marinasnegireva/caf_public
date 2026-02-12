namespace CAF.DB.Entities;

public class Flag
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool Active { get; set; } = false;
    public bool Constant { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Link to a profile for organizing flags. 0 for global flags.
    /// </summary>
    public int ProfileId { get; set; } = 0;

    public Profile? Profile { get; set; }
}