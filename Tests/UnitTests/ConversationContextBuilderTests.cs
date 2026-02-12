using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests;

/// <summary>
/// Tests for ConversationStateBuilder, which builds the basic conversation context.
///
/// ConversationStateBuilder is responsible for:
/// - Setting CurrentTurn, Session, and CancellationToken
/// - Loading RecentTurnsCount and MaxDialogueLogTurns settings
/// - Setting the IsOOCRequest flag
/// - Loading Persona and PersonaName
///
/// ConversationStateBuilder is NOT responsible for:
/// - RecentTurns, PreviousTurn, PreviousResponse (handled by TurnHistoryEnricher)
/// - DialogueLog (handled by DialogueLogEnricher)
/// - AlwaysOnMemories (handled by AlwaysOnMemoryEnricher)
/// - TriggeredContexts (handled by TriggeredContextEnricher)
/// - Flags (handled by FlagEnricher)
/// - Quotes (handled by QuoteEnricher)
/// - Perceptions (handled by PerceptionEnricher)
///
/// See the enricher test files for tests of enricher behavior.
/// </summary>
[TestFixture]
public class ConversationContextBuilderTests
{
    private Mock<ISystemMessageService> _systemMessageService = null!;
    private Mock<ISettingService> _settingService = null!;
    private Mock<ILogger<ConversationStateBuilder>> _logger = null!;
    private Mock<IContextDataService> _contextDataService = null!;

    [SetUp]
    public void Setup()
    {
        _systemMessageService = new Mock<ISystemMessageService>();
        _settingService = new Mock<ISettingService>();
        _logger = new Mock<ILogger<ConversationStateBuilder>>();
        _contextDataService = new Mock<IContextDataService>();
    }

    private ConversationStateBuilder CreateBuilder()
    {
        return new ConversationStateBuilder(
            _systemMessageService.Object,
            _settingService.Object,
            _contextDataService.Object,
            _logger.Object);
    }

    [Test]
    public async Task BuildContextAsync_WithBasicData_PopulatesSettings()
    {
        // Arrange
        _settingService.Setup(s => s.GetIntAsync(SettingsKeys.PreviousTurnsCount, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _settingService.Setup(s => s.GetIntAsync(SettingsKeys.MaxDialogueLogTurns, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100);

        var session = new Session { Id = 1, Name = "Test Session" };
        var turn = new Turn { Id = 1, Input = "Hello", SessionId = session.Id };

        var builder = CreateBuilder();

        // Act
        var context = await builder.BuildContextAsync(turn, session, CancellationToken.None);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(context.CurrentTurn, Is.EqualTo(turn));
            Assert.That(context.Session, Is.EqualTo(session));
            Assert.That(context.RecentTurnsCount, Is.EqualTo(10));
            Assert.That(context.MaxDialogueLogTurns, Is.EqualTo(100));
        });
    }

    [Test]
    public async Task BuildContextAsync_WithPersona_PopulatesName()
    {
        // Arrange
        var persona = new SystemMessage
        {
            Id = 1,
            Name = "TestBot",
            Type = SystemMessage.SystemMessageType.Persona,
            IsActive = true
        };

        _systemMessageService.Setup(s => s.GetActivePersonaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(persona);

        var session = new Session { Id = 1, Name = "Test Session" };
        var turn = new Turn { Id = 1, Input = "Hello", SessionId = session.Id };

        var builder = CreateBuilder();

        // Act
        var context = await builder.BuildContextAsync(turn, session, CancellationToken.None);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(context.Persona, Is.EqualTo(persona));
            Assert.That(context.PersonaName, Is.EqualTo("TestBot"));
        });
    }

    [Test]
    public async Task BuildContextAsync_WithSettingsFailure_UsesDefaults()
    {
        // Arrange
        _settingService.Setup(s => s.GetIntAsync(It.IsAny<SettingsKeys>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Settings service error"));

        var session = new Session { Id = 1, Name = "Test Session" };
        var turn = new Turn { Id = 1, Input = "Hello", SessionId = session.Id };

        var builder = CreateBuilder();

        // Act
        var context = await builder.BuildContextAsync(turn, session, CancellationToken.None);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(context.RecentTurnsCount, Is.EqualTo(6), "Should use default value");
            Assert.That(context.MaxDialogueLogTurns, Is.EqualTo(50), "Should use default value");
        });
    }

    [Test]
    public async Task BuildContextAsync_DetectsOOCRequest()
    {
        // Arrange
        var session = new Session { Id = 1, Name = "Test Session" };
        var oocTurn = new Turn { Id = 1, Input = "[ooc] This is out of character", SessionId = session.Id };
        var normalTurn = new Turn { Id = 2, Input = "Hello", SessionId = session.Id };

        var builder = CreateBuilder();

        // Act
        var oocContext = await builder.BuildContextAsync(oocTurn, session, CancellationToken.None);
        var normalContext = await builder.BuildContextAsync(normalTurn, session, CancellationToken.None);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(oocContext.IsOOCRequest, Is.True, "Should detect OOC prefix");
            Assert.That(normalContext.IsOOCRequest, Is.False, "Should not detect OOC for normal input");
        });
    }
}