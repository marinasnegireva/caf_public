namespace CAF.LLM.Gemini;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "global";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string ApiKey { get; set; } = string.Empty;

    // Technical tasks model (for structured output, mapping, etc.)
    public string TechnicalModel { get; set; } = "gemini-3-flash-preview";

    // Embedding configuration
    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public int EmbeddingDimension { get; set; } = 3072;

    public GenerationConfigOptions GenerationConfig { get; set; } = new();
    public List<SafetySettingOptions> SafetySettings { get; set; } = [];
}

public class GenerationConfigOptions
{
    public int MaxOutputTokens { get; set; } = 4200;
    public double Temperature { get; set; } = 1.0;
}

public class SafetySettingOptions
{
    public string Category { get; set; } = string.Empty;
    public string Threshold { get; set; } = string.Empty;
}