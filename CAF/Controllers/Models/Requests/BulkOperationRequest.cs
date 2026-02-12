namespace CAF.Controllers.Models.Requests;

public class BulkOperationRequest
{
    public List<int> QuoteIds { get; set; } = [];
    public List<int> Ids { get; set; } = [];
}