namespace CAF.Controllers.Models.Responses;

public class BulkOperationResult
{
    public int Processed { get; set; }
    public int Reloaded { get; set; }
    public int Errors { get; set; }
}