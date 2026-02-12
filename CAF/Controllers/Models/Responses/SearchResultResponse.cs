namespace CAF.Controllers.Models.Responses;

public class SearchResultResponse
{
    public List<object> DynamicQuotes { get; set; } = [];
    public List<object> CanonQuotes { get; set; } = [];
}