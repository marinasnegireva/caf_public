using CAF.Interfaces;

namespace Tests.Infrastructure;

/// <summary>
/// Helper class for configuring LLM client mocks in integration tests.
/// </summary>
public class LLMMockConfigurator(Mock<IGeminiClient> geminiMock, Mock<IClaudeClient> claudeMock)
{

    #region Gemini Configuration

    /// <summary>
    /// Configures the default Gemini response for general requests.
    /// </summary>
    public void ConfigureDefaultGeminiResponse(string response = "This is a test response from the mocked LLM.")
    {
        geminiMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response));
    }

    /// <summary>
    /// Configures default embeddings for semantic search operations.
    /// </summary>
    public void ConfigureDefaultEmbeddings()
    {
        geminiMock
            .Setup(x => x.EmbedBatchAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<string> texts, CancellationToken ct) =>
            {
                return [.. texts.Select(_ => new float[] { 0.1f, 0.2f, 0.3f })];
            });
    }

    /// <summary>
    /// Configures perception-specific Gemini responses.
    /// </summary>
    public void ConfigurePerceptionResponses(params string[] responses)
    {
        var callCount = 0;
        geminiMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                true,  // technical = true for perception
                null,  // turnId = null for perception
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var response = callCount < responses.Length ? responses[callCount] : "[]";
                callCount++;
                return (true, response);
            });
    }

    /// <summary>
    /// Configures the final (non-perception) Gemini response.
    /// </summary>
    public void ConfigureFinalResponse(string response)
    {
        geminiMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                false,  // technical = false for main response
                It.IsAny<int?>(),  // turnId will be set
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, response));
    }

    /// <summary>
    /// Configures Gemini to capture the final request for assertions.
    /// </summary>
    public void ConfigureFinalResponseWithCapture(string response, Action<GeminiRequest> captureCallback)
    {
        geminiMock
            .Setup(x => x.GenerateContentAsync(
                It.Is<GeminiRequest>(r => !r.Contents.Any(c => c.Parts.Any(p => p.Text.Contains("Perception")))),
                false,
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, bool, int?, CancellationToken>((req, tech, turnId, ct) =>
            {
                captureCallback(req);
            })
            .ReturnsAsync((true, response));
    }

    /// <summary>
    /// Configures Gemini to return an error response.
    /// </summary>
    public void ConfigureGeminiError(string errorMessage = "Error: API rate limit exceeded")
    {
        geminiMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, errorMessage));
    }

    #endregion Gemini Configuration

    #region Claude Configuration

    /// <summary>
    /// Configures the default Claude response.
    /// </summary>
    public void ConfigureDefaultClaudeResponse(string response = "This is a test response from Claude.")
    {
        claudeMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<ClaudeRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync((true, response));
    }

    /// <summary>
    /// Configures Claude to capture the request for assertions.
    /// </summary>
    public void ConfigureClaudeResponseWithCapture(string response, Action<ClaudeRequest> captureCallback)
    {
        claudeMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<ClaudeRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .Callback<ClaudeRequest, CancellationToken, int?>((req, ct, turnId) =>
            {
                captureCallback(req);
            })
            .ReturnsAsync((true, response));
    }

    /// <summary>
    /// Configures Claude to return an error response.
    /// </summary>
    public void ConfigureClaudeError(string errorMessage = "Error: Claude API error")
    {
        claudeMock
            .Setup(x => x.GenerateContentAsync(
                It.IsAny<ClaudeRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()))
            .ReturnsAsync((false, errorMessage));
    }

    #endregion Claude Configuration

    #region Verification

    /// <summary>
    /// Verifies that Gemini was called the expected number of times.
    /// </summary>
    public void VerifyGeminiCalled(Times times)
    {
        geminiMock.Verify(
            x => x.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            times);
    }

    /// <summary>
    /// Verifies that Claude was called the expected number of times.
    /// </summary>
    public void VerifyClaudeCalled(Times times)
    {
        claudeMock.Verify(
            x => x.GenerateContentAsync(
                It.IsAny<ClaudeRequest>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>()),
            times);
    }

    #endregion Verification
}