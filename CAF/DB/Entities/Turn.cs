using System.ComponentModel.DataAnnotations.Schema;

namespace CAF.DB.Entities;

public class Turn
{
    public int Id { get; set; }
    public string Input { get; set; } = string.Empty;
    public string JsonInput { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string StrippedTurn { get; set; } = string.Empty;
    public bool Accepted { get; set; } = false;
    public int SessionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SessionId))]
    public Session? Session { get; set; }

    /// <summary>
    /// Gets the display-only portion of the response (before the separator).
    /// If no separator is found, returns the full response.
    /// </summary>
    [NotMapped]
    public string DisplayResponse
    {
        get
        {
            if (string.IsNullOrEmpty(Response))
                return string.Empty;

            var separatorIndex = Response.IndexOf(ConversationConstants.ResponseSeparator, StringComparison.Ordinal);
            return separatorIndex < 0 ? Response : Response[..separatorIndex].TrimEnd();
        }
    }
}