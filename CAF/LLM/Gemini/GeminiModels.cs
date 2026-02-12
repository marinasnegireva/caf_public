namespace CAF.LLM.Gemini;

/// <summary>
/// Wrapper for Gemini API request
/// </summary>
public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; }

    [JsonPropertyName("system_instruction")]
    public SystemInstruction SystemInstruction { get; set; }

    [JsonPropertyName("generationConfig")]
    public GenerationConfig GenerationConfig { get; set; } = new();

    [JsonPropertyName("safetySettings")]
    public List<SafetySetting> SafetySettings { get; set; }
}

public class SystemInstruction
{
    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; }
}

public class GenerationConfig
{
    [JsonPropertyName("responseMimeType")]
    public string ResponseMimeType { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; set; } = 4000;

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; } = 1;

    [JsonPropertyName("thinkingConfig")]
    public ThinkingConfig ThinkingConfig { get; set; } = new();
}

public class ThinkingConfig
{
    [JsonPropertyName("includeThoughts")]
    public bool IncludeThoughts { get; set; } = true;

    [JsonPropertyName("thinkingLevel")]
    public string ThinkingLevel { get; set; } = "high";
}

public class Content
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; }
}

public class Part
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("thought")]
    public bool? Thought { get; set; } = null;
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<Candidate> Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public UsageMetadata UsageMetadata { get; set; }
}

public class Candidate
{
    [JsonPropertyName("content")]
    public Content Content { get; set; }
}

public class UsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("cachedContentTokenCount")]
    public int? CachedContentTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("thoughtsTokenCount")]
    public int? ThoughtsTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}

/// <summary>
/// Request for counting tokens
/// </summary>
public class CountTokensRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; }

    [JsonPropertyName("system_instruction")]
    public SystemInstruction SystemInstruction { get; set; }
}

/// <summary>
/// Safety setting for blocking unsafe content
/// </summary>
public class SafetySetting
{
    [JsonPropertyName("category")]
    public string Category { get; set; }

    [JsonPropertyName("threshold")]
    public string Threshold { get; set; }
}

/// <summary>
/// Harm categories for safety filtering
/// </summary>
public static class HarmCategory
{
    public const string HateSpeech = "HARM_CATEGORY_HATE_SPEECH";
    public const string SexuallyExplicit = "HARM_CATEGORY_SEXUALLY_EXPLICIT";
    public const string DangerousContent = "HARM_CATEGORY_DANGEROUS_CONTENT";
    public const string Harassment = "HARM_CATEGORY_HARASSMENT";
    public const string CivicIntegrity = "HARM_CATEGORY_CIVIC_INTEGRITY";
}

/// <summary>
/// Harm block thresholds for safety filtering
/// </summary>
public static class HarmBlockThreshold
{
    public const string BlockNone = "BLOCK_NONE";
    public const string BlockOnlyHigh = "BLOCK_ONLY_HIGH";
    public const string BlockMediumAndAbove = "BLOCK_MEDIUM_AND_ABOVE";
    public const string BlockLowAndAbove = "BLOCK_LOW_AND_ABOVE";
    public const string Off = "OFF";
}

/// <summary>
/// Response from counting tokens
/// </summary>
public class CountTokensResponse
{
    [JsonPropertyName("totalTokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("cachedContentTokenCount")]
    public int? CachedContentTokenCount { get; set; }
}

/// <summary>
/// Response from batch creation (returns an Operation)
/// </summary>
public class BatchOperation
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("response")]
    public BatchOperationResponse Response { get; set; }

    [JsonPropertyName("error")]
    public BatchOperationError Error { get; set; }
}

/// <summary>
/// Response data from a completed batch operation
/// </summary>
public class BatchOperationResponse
{
    [JsonPropertyName("state")]
    public string State { get; set; }

    [JsonPropertyName("output")]
    public BatchOutput Output { get; set; }
}

/// <summary>
/// Output from batch processing
/// </summary>
public class BatchOutput
{
    [JsonPropertyName("inlinedResponses")]
    public BatchInlinedResponses InlinedResponses { get; set; }
}

/// <summary>
/// Collection of inlined responses from batch
/// </summary>
public class BatchInlinedResponses
{
    [JsonPropertyName("inlinedResponses")]
    public List<BatchInlinedResponse> InlinedResponses { get; set; } = [];
}

/// <summary>
/// A single response from batch processing
/// </summary>
public class BatchInlinedResponse
{
    [JsonPropertyName("response")]
    public GeminiResponse Response { get; set; }

    [JsonPropertyName("error")]
    public BatchOperationError Error { get; set; }
}

/// <summary>
/// Error information from batch operation
/// </summary>
public class BatchOperationError
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
}