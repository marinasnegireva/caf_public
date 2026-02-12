namespace CAF.DB.Entities;

public class SystemMessage
{
    public int Id { get; set; }

    /// <summary>
    /// Profile this system message belongs to. 0 for global system messages.
    /// </summary>
    public int ProfileId { get; set; } = 0;

    /// <summary>
    /// Navigation property to the profile
    /// </summary>
    [JsonIgnore]
    public Profile? Profile { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SystemMessageType Type { get; set; } = SystemMessageType.Persona;

    public int Version { get; set; } = 1;
    public int? ParentId { get; set; } // For version history
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? ModifiedBy { get; set; }
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];

    public string? Notes { get; set; }

    public enum SystemMessageType
    {
        Persona,          // Main character/assistant persona
        Perception,       // How the system perceives/interprets things
        Technical         // Technical instructions (format, constraints, etc.)
    }
}