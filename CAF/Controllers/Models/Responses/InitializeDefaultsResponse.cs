namespace CAF.Controllers.Models.Responses;

public class InitializeDefaultsResponse
{
    public List<InitializeResult> Created { get; set; } = [];
    public List<InitializeResult> Skipped { get; set; } = [];
    public List<InitializeResult> Errors { get; set; } = [];
}

public class InitializeResult
{
    public string Name { get; set; } = string.Empty;
    public int? Id { get; set; }
    public string Message { get; set; } = string.Empty;
}