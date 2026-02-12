namespace CAF.Controllers.Models.Responses;

public class PreviewResponse
{
    public string CompleteMessage { get; set; } = string.Empty;
    public bool HasPersona { get; set; }
    public int PerceptionCount { get; set; }
    public bool HasTechnical { get; set; }
    public int ContextCount { get; set; }
    public int TokenCount { get; set; }
    public List<PreviewItem> Items { get; set; } = [];
}

public class PreviewItem
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int TokenCount { get; set; }
}