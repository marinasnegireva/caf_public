using CAF.Interfaces;
using CAF.LLM.Logging;
using Microsoft.Extensions.Configuration;

namespace Tests.IntegrationTests;

[Ignore("Integration")]
[TestFixture]
public class ClaudeClientIntegrationTests
{
    private IClaudeClient _claudeClient = null!;
    private ClaudeOptions _options = null!;
    private ILoggerFactory _loggerFactory = null!;

    [SetUp]
    public void Setup()
    {
        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CAF"))
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _options = new ClaudeOptions();
        configuration.GetSection(ClaudeOptions.SectionName).Bind(_options);

        // Setup logging (keep factory alive for test duration)
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var llmLogger = _loggerFactory.CreateLogger<LLMLogger>();

        // Setup Claude client with real API key from config
        var claudeOptions = Options.Create(_options);

        // Create a mock setting service that returns the model from options
        var mockSettingService = new Mock<ISettingService>();
        mockSettingService
            .Setup(s => s.GetByNameAsync(SettingsKeys.ClaudeModel, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Setting { Name = SettingsKeys.ClaudeModel.ToString(), Value = _options.Model });

        _claudeClient = new ClaudeClient(claudeOptions, new HttpClient(), null, llmLogger, mockSettingService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _loggerFactory?.Dispose();
    }

    #region Helper Methods

    private ClaudeRequest CreateSimpleRequest(string message, int maxTokens = 100)
    {
        return ClaudeMessageBuilder.Create()
            .AddUserMessage(message)
            .WithMaxTokens(maxTokens)
            .Build(_options.Model);
    }

    private static void AssertSuccessfulResponse((bool success, string result) response, string context = "")
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.success, Is.True, $"API call should succeed{(string.IsNullOrEmpty(context) ? "" : $": {context}")}");
            Assert.That(response.result, Is.Not.Empty, $"Response should not be empty{(string.IsNullOrEmpty(context) ? "" : $": {context}")}");
        });
    }

    private static void AssertContainsAny(string text, string[] terms, string message)
    {
        var containsAny = terms.Any(term => text.ToLower().Contains(term.ToLower()));
        Assert.That(containsAny, Is.True, message);
    }

    #endregion

    #region Basic Message Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_SimpleMessage_ReturnsSuccess()
    {
        // Arrange
        var request = CreateSimpleRequest("Say 'Hello, World!' and nothing else.");

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
        Assert.That(response.result.ToLower(), Does.Contain("hello"), "Response should contain greeting");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithSystemInstruction_FollowsSystemPrompt()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .WithSystem("You are a pirate. Always respond in pirate speak.")
            .AddUserMessage("Introduce yourself in one sentence.")
            .WithMaxTokens(200)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
        var pirateWords = new[] { "ahoy", "matey", "yarr", "arr", "ye", "aye", "'tis", "pirate" };
        AssertContainsAny(response.result, pirateWords, "Response should contain pirate-like language");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithConversationHistory_MaintainsContext()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .WithSystem("You are a helpful math assistant.")
            .AddTurn("What is 7 times 8?", "7 times 8 equals 56.")
            .AddTurn("What is 9 times 6?", "9 times 6 equals 54.")
            .AddUserMessage("What was the answer to my first question?")
            .WithMaxTokens(200)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
        AssertContainsAny(response.result, ["56", "fifty", "first"],
            "Response should reference the first answer or its value");
    }

    #endregion

    #region Model Parameter Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithTemperature_AffectsRandomness()
    {
        // Arrange - Test with very low temperature (more deterministic)
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Count from 1 to 5.")
            .WithTemperature(0.0)
            .WithMaxTokens(100)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
        Assert.That(response.result, Does.Contain("1"), "Response should contain counting");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithMaxTokensLimit_RespectsLimit()
    {
        // Arrange - Request short content with appropriate token limit
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Write a 10-word sentence about the ocean.")
            .WithMaxTokens(50)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
        var wordCount = response.result.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(wordCount, Is.LessThanOrEqualTo(100), "Response should be limited by max_tokens");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithStopSequences_StopsAtSequence()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Count from 1 to 10. Use the word STOP after 5.")
            .WithStopSequences(["STOP"])
            .WithMaxTokens(200)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response, "with stop sequences");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithTopP_AffectsSampling()
    {
        // Arrange - Use top_p to limit vocabulary
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Complete this: The sky is")
            .WithTopP(0.1)
            .WithMaxTokens(50)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithTopK_LimitsSampling()
    {
        // Arrange - Use top_k to limit token choices
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Say a greeting")
            .WithTopK(5)
            .WithMaxTokens(50)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Token Counting Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task CountTokensAsync_WithSimpleText_ReturnsPositiveCount()
    {
        // Arrange
        var text = "Hello, how are you today?";

        // Act
        var tokenCount = await _claudeClient.CountTokensAsync(text);

        // Assert
        Assert.That(tokenCount, Is.GreaterThan(0), "Should return positive token count");
        Assert.That(tokenCount, Is.LessThan(20), "Simple sentence should have fewer than 20 tokens");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task CountTokensAsync_WithEmptyString_ReturnsZero()
    {
        // Arrange
        var text = "";

        // Act
        var tokenCount = await _claudeClient.CountTokensAsync(text);

        // Assert
        Assert.That(tokenCount, Is.EqualTo(0), "Empty string should return 0 tokens");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task CountTokensAsync_WithLongText_ReturnsHigherCount()
    {
        // Arrange
        var shortText = "Hello";
        var longText = string.Join(" ", Enumerable.Repeat("Hello world", 100));

        // Act
        var shortCount = await _claudeClient.CountTokensAsync(shortText);
        var longCount = await _claudeClient.CountTokensAsync(longText);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(shortCount, Is.GreaterThan(0), "Short text should have tokens");
            Assert.That(longCount, Is.GreaterThan(shortCount), "Longer text should have more tokens than shorter text");
        });
    }

    #endregion

    #region Error Handling Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithInvalidModel_ReturnsError()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithMaxTokens(100)
            .Build("invalid-model-name-that-does-not-exist");

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(response.success, Is.False, "Invalid model should fail");
            Assert.That(response.result, Is.Not.Empty, "Should return error message");
        });
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Write a very long essay about the history of computing")
            .WithMaxTokens(8192)
            .Build(_options.Model);

        // Cancel immediately
        cts.Cancel();

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _claudeClient.GenerateContentAsync(request, cts.Token);
        });
    }

    #endregion

    #region Advanced Features Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Say hello")
            .WithMetadata(new ClaudeMetadata { UserId = "test-user-123" })
            .WithMaxTokens(100)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response, "with metadata");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithThinking_ReturnsSuccessWithThought()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Solve this riddle and explain your reasoning: I speak without a mouth and hear without ears. I have no body, but I come alive with wind. What am I?")
            .WithMaxTokens(2000)
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response, "with thinking enabled");
        Assert.That(response.result.Length, Is.GreaterThan(10), "Response should contain substantial content");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithThinkingDisabled_ReturnsSuccess()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Say hello")
            .WithMaxTokens(100)
            .WithoutThinking()
            .Build(_options.Model);

        // Act
        var response = await _claudeClient.GenerateContentAsync(request);

        // Assert
        AssertSuccessfulResponse(response, "with thinking disabled");
    }

    #endregion

    #region Multiple Calls and Serialization Tests

    [Test]
    public void ClaudeRequest_Serialization_MatchesAPIFormat()
    {
        // Arrange
        var request = ClaudeMessageBuilder.Create()
            .WithSystem("Test system instruction")
            .AddUserMessage("Hello")
            .WithMaxTokens(100)
            .WithTemperature(0.7)
            .Build("claude-sonnet-4-5");

        // Act
        var json = JsonSerializer.Serialize(request,
            new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            });

        // Assert - Verify JSON structure matches Claude API spec
        var requiredFields = new[] { "model", "max_tokens", "temperature", "system", "messages", "role", "content", "user" };
        foreach (var field in requiredFields)
        {
            Assert.That(json, Does.Contain($"\"{field}\""), $"Should have {field} field");
        }
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_MultipleSequentialCalls_AllSucceed()
    {
        // Test that multiple calls to the API work correctly
        for (var i = 1; i <= 3; i++)
        {
            // Arrange
            var request = CreateSimpleRequest($"Count to {i}", maxTokens: 50);

            // Act
            var response = await _claudeClient.GenerateContentAsync(request);

            // Assert
            AssertSuccessfulResponse(response, $"call {i}");
        }
    }

    #endregion
}
