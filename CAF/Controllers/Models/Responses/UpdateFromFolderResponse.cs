namespace CAF.Controllers.Models.Responses;

public class UpdateFromFolderResponse
{
    public List<UpdateResult> Updated { get; set; } = [];
    public List<UpdateResult> NotFound { get; set; } = [];
    public List<UpdateResult> Skipped { get; set; } = [];
    public List<UpdateResult> Errors { get; set; } = [];
}

public class UpdateResult
{
    public string ContextFileName { get; set; } = string.Empty;
    public string? MdFileName { get; set; }
    public int? NewVersion { get; set; }
    public string Message { get; set; } = string.Empty;
}