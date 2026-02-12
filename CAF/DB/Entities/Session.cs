namespace CAF.DB.Entities;

public class Session
{
    public int Id { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Link to a profile for organizing sessions. 0 for global sessions.
    /// </summary>
    public int ProfileId { get; set; } = 0;

    public Profile? Profile { get; set; }

    public List<Turn> Turns { get; set; } = [];
}