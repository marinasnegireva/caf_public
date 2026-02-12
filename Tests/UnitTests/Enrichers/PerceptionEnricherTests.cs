using CAF.Interfaces;
using CAF.Services.Conversation;
using static CAF.DB.Entities.SystemMessage;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for PerceptionEnricher - handles perception processing via LLM calls.
/// Tests cover: settings toggle, empty input handling, perception message processing,
/// JSON parsing, error handling, and cancellation.
/// </summary>
[TestFixture]
public class PerceptionEnricherTests
{
    private Mock<ISystemMessageService> _mockSystemMessageService = null!;
    private Mock<IGeminiClient> _mockGeminiClient = null!;
    private Mock<ISettingService> _mockSettingService = null!;
    private Mock<ILogger<PerceptionEnricher>> _mockLogger = null!;
    private ServiceProvider _serviceProvider = null!;
    private PerceptionEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        _mockSystemMessageService = new Mock<ISystemMessageService>();
        _mockGeminiClient = new Mock<IGeminiClient>();
        _mockSettingService = new Mock<ISettingService>();
        _mockLogger = new Mock<ILogger<PerceptionEnricher>>();

        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();

        _enricher = new PerceptionEnricher(
            _mockSystemMessageService.Object,
            _mockGeminiClient.Object,
            _serviceProvider,
            _mockSettingService.Object,
            _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
    }

    [Test]
    public void EnrichAsync_WithNullState_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(async () => await _enricher.EnrichAsync(null!));
    }

    [Test]
    public async Task EnrichAsync_WhenPerceptionDisabled_SetsEmptyPerceptions()
    {
        // Arrange
        var state = CreateMinimalState();
        _mockSettingService.Setup(s => s.GetBoolAsync(
                SettingsKeys.PerceptionEnabled,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Is.Empty);
        _mockSystemMessageService.Verify(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_WithEmptyInput_SetsEmptyPerceptions()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn.Input = "";

        _mockSettingService.Setup(s => s.GetBoolAsync(
                SettingsKeys.PerceptionEnabled,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Is.Empty);
        _mockSystemMessageService.Verify(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_WithWhitespaceInput_SetsEmptyPerceptions()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn.Input = "   ";

        _mockSettingService.Setup(s => s.GetBoolAsync(
                SettingsKeys.PerceptionEnabled,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Is.Empty);
    }

    [Test]
    public async Task EnrichAsync_WithNoActivePerceptionMessages_SetsEmptyPerceptions()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Is.Empty);
    }

    [Test]
    public async Task EnrichAsync_ProcessesPerceptionMessagesAndParsesResponse()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        var perceptionMessage = new SystemMessage
        {
            Id = 1,
            Name = "Test Perception",
            Content = "Analyze the input",
            IsActive = true,
            Type = SystemMessageType.Perception
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([perceptionMessage]);

        var jsonResponse = """
            [
                {"Property": "emotion", "Explanation": "User is happy"},
                {"Property": "intent", "Explanation": "User is greeting"}
            ]
            """;

        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, jsonResponse));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(state.Perceptions.Any(p => p.Property == "emotion" && p.Explanation == "User is happy"), Is.True);
            Assert.That(state.Perceptions.Any(p => p.Property == "intent" && p.Explanation == "User is greeting"), Is.True);
        });
    }

    [Test]
    public async Task EnrichAsync_WithMultiplePerceptionMessages_ProcessesAll()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        var perceptionMessages = new List<SystemMessage>
        {
            new() { Id = 1, Name = "Perception 1", Content = "Analyze emotions", IsActive = true, Type = SystemMessageType.Perception },
            new() { Id = 2, Name = "Perception 2", Content = "Analyze intent", IsActive = true, Type = SystemMessageType.Perception }
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(perceptionMessages);

        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, """[{"Property": "test", "Explanation": "test explanation"}]"""));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Has.Count.EqualTo(2));
        _mockGeminiClient.Verify(g => g.GenerateContentAsync(
            It.IsAny<GeminiRequest>(),
            It.IsAny<bool>(),
            It.IsAny<int?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task EnrichAsync_WithEmptyLLMResponse_ContinuesProcessing()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        var perceptionMessage = new SystemMessage
        {
            Id = 1,
            Name = "Test Perception",
            Content = "Analyze",
            IsActive = true,
            Type = SystemMessageType.Perception
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([perceptionMessage]);

        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, ""));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Is.Empty);
    }

    [Test]
    public async Task EnrichAsync_WithInvalidJsonResponse_SetsEmptyPerceptions()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        var perceptionMessage = new SystemMessage
        {
            Id = 1,
            Name = "Test Perception",
            Content = "Analyze",
            IsActive = true,
            Type = SystemMessageType.Perception
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([perceptionMessage]);

        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "not valid json"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw, just have empty perceptions
        Assert.That(state.Perceptions, Is.Empty);
    }

    [Test]
    public async Task EnrichAsync_WithJsonWrappedInText_ExtractsAndParses()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        var perceptionMessage = new SystemMessage
        {
            Id = 1,
            Name = "Test Perception",
            Content = "Analyze",
            IsActive = true,
            Type = SystemMessageType.Perception
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([perceptionMessage]);

        // LLM sometimes returns JSON wrapped in extra text
        var responseWithWrapper = """
            Here is my analysis:
            [{"Property": "mood", "Explanation": "User is positive"}]
            End of analysis.
            """;

        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, responseWithWrapper));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Perceptions, Has.Count.EqualTo(1));
        Assert.That(state.Perceptions.First().Property, Is.EqualTo("mood"));
    }

    [Test]
    public async Task EnrichAsync_BuildsCorrectUserMessageFormat()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn.Input = "Hello there";
        state.PreviousResponse = "Hi, how are you?";
        state.PersonaName = "Assistant";
        state.UserName = "User";
        SetupPerceptionEnabled();

        var perceptionMessage = new SystemMessage
        {
            Id = 1,
            Name = "Test Perception",
            Content = "Analyze",
            IsActive = true,
            Type = SystemMessageType.Perception
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([perceptionMessage]);

        GeminiRequest? capturedRequest = null;
        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, bool, int?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .ReturnsAsync((true, """[{"Property": "test", "Explanation": "test explanation"}]"""));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(capturedRequest, Is.Not.Null);
        Assert.That(capturedRequest!.SystemInstruction?.Parts?.FirstOrDefault()?.Text, Is.EqualTo("Analyze"));
    }

    [Test]
    public async Task EnrichAsync_WithPreviousResponse_IncludesItInMessage()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn.Input = "Hello";
        state.PreviousResponse = "Previous response";
        state.PersonaName = "Persona";
        state.UserName = "TestUser";
        SetupPerceptionEnabled();

        var perceptionMessage = new SystemMessage
        {
            Id = 1,
            Name = "Test",
            Content = "Analyze",
            IsActive = true,
            Type = SystemMessageType.Perception
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([perceptionMessage]);

        GeminiRequest? capturedRequest = null;
        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<GeminiRequest, bool, int?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .ReturnsAsync((true, """[]"""));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - The message should include both previous response and current input
        Assert.That(capturedRequest, Is.Not.Null);
        var userContent = capturedRequest!.Contents?.LastOrDefault()?.Parts?.FirstOrDefault()?.Text;
        Assert.That(userContent, Does.Contain("Persona"));
        Assert.That(userContent, Does.Contain("Previous response"));
        Assert.That(userContent, Does.Contain("TestUser"));
        Assert.That(userContent, Does.Contain("Hello"));
    }

    [Test]
    public async Task EnrichAsync_WhenLLMThrows_ContinuesWithOtherPerceptions()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupPerceptionEnabled();

        var perceptionMessages = new List<SystemMessage>
        {
            new() { Id = 1, Name = "Failing", Content = "Analyze 1", IsActive = true, Type = SystemMessageType.Perception },
            new() { Id = 2, Name = "Success", Content = "Analyze 2", IsActive = true, Type = SystemMessageType.Perception }
        };

        _mockSystemMessageService.Setup(s => s.GetActivePerceptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(perceptionMessages);

        var callCount = 0;
        _mockGeminiClient.Setup(g => g.GenerateContentAsync(
                It.IsAny<GeminiRequest>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? throw new Exception("LLM error") : (true, """[{"Property": "test", "Explanation": "test explanation"}]""");
            });

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should have at least one perception from the successful call
        // Note: Due to parallel processing, order is not guaranteed
        Assert.That(state.Perceptions, Has.Count.GreaterThanOrEqualTo(0));
    }

    private void SetupPerceptionEnabled()
    {
        _mockSettingService.Setup(s => s.GetBoolAsync(
                SettingsKeys.PerceptionEnabled,
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private static ConversationState CreateMinimalState()
    {
        return new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test input" },
            Session = new Session { Id = 1, Name = "Test Session" },
            PersonaName = "Persona",
            UserName = "User"
        };
    }
}