namespace CAF.Controllers.Models.Responses;

public class BulkOperationResponse
{
    public bool Success { get; set; }
    public int Processed { get; set; }
    public string Message { get; set; } = string.Empty;
}