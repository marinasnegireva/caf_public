using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

[TestFixture]
public class DialogueLogEnricherTests
{
    private ServiceProvider _serviceProvider = null!;
    private GeneralDbContext _dbContext = null!;
    private Mock<ILogger<DialogueLogEnricher>> _loggerMock = null!;
    private DialogueLogEnricher _enricher = null!;
    private ConversationState _state = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new GeneralDbContext(options);

        var services = new ServiceCollection();
        services.AddScoped(_ => _dbContext);
        _serviceProvider = services.BuildServiceProvider();

        _loggerMock = new Mock<ILogger<DialogueLogEnricher>>();
        _enricher = new DialogueLogEnricher(_serviceProvider, _loggerMock.Object);

        _state = new ConversationState
        {
            CurrentTurn = new Turn { Id = 10, Input = "Current input", SessionId = 1 },
            Session = new Session { Id = 1, Name = "Test Session" },
            RecentTurnsCount = 3,
            MaxDialogueLogTurns = 5
        };
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            _dbContext.Database.EnsureDeleted();
        }
        catch (ObjectDisposedException)
        {
            // Context was already disposed in the test
        }

        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task EnrichAsync_WithNullSession_LogsDebugAndSkips()
    {
        // Arrange
        var stateWithoutSession = new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test" },
            Session = null!
        };

        // Act
        await _enricher.EnrichAsync(stateWithoutSession);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("skipped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithNoAcceptedTurns_DoesNotSetDialogueLog()
    {
        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Is.Null);
    }

    [Test]
    public async Task EnrichAsync_WithFewerTurnsThanRecent_DoesNotSetDialogueLog()
    {
        // Arrange - Only 2 turns, but RecentTurnsCount is 3
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, StrippedTurn = "Turn 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = true, StrippedTurn = "Turn 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Is.Null);
    }

    [Test]
    public async Task EnrichAsync_BuildsDialogueLogFromOlderTurns()
    {
        // Arrange - 8 turns total, recent=3, so 5 should go into dialogue log
        var turns = new List<Turn>();
        for (var i = 1; i <= 8; i++)
        {
            turns.Add(new Turn
            {
                Id = i,
                SessionId = 1,
                Accepted = true,
                StrippedTurn = $"Stripped Turn {i}",
                Input = $"Input {i}",
                Response = $"Response {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-60 + i)
            });
        }

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Is.Not.Null);
        Assert.That(_state.DialogueLog, Does.Contain("Stripped Turn 1"));
        Assert.That(_state.DialogueLog, Does.Contain("Stripped Turn 2"));
        Assert.That(_state.DialogueLog, Does.Not.Contain("Stripped Turn 6")); // Recent turns excluded
    }

    [Test]
    public async Task EnrichAsync_UsesStrippedTurnWhenAvailable()
    {
        // Arrange
        var turns = new[]
        {
            new Turn
            {
                Id = 1,
                SessionId = 1,
                Accepted = true,
                StrippedTurn = "Stripped content 1",
                Input = "Original Input 1",
                Response = "Original Response 1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Turn { Id = 2, SessionId = 1, Accepted = true, StrippedTurn = "Stripped 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, StrippedTurn = "Stripped 3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, StrippedTurn = "Stripped 4", CreatedAt = DateTime.UtcNow.AddMinutes(-7) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Does.Contain("Stripped content 1"));
        Assert.That(_state.DialogueLog, Does.Not.Contain("Original Input 1"));
    }

    [Test]
    public async Task EnrichAsync_FallsBackToInputAndResponseWhenStrippedTurnEmpty()
    {
        // Arrange
        var turns = new[]
        {
            new Turn
            {
                Id = 1,
                SessionId = 1,
                Accepted = true,
                StrippedTurn = "", // Empty stripped turn
                Input = "Fallback Input",
                Response = "Fallback Response",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Turn { Id = 2, SessionId = 1, Accepted = true, StrippedTurn = "S2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, StrippedTurn = "S3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, StrippedTurn = "S4", CreatedAt = DateTime.UtcNow.AddMinutes(-7) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Does.Contain("Fallback Input"));
        Assert.That(_state.DialogueLog, Does.Contain("Fallback Response"));
    }

    [Test]
    public async Task EnrichAsync_IncludesMetaHeader()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, StrippedTurn = "Old Turn", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = true, StrippedTurn = "Turn 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, StrippedTurn = "Turn 3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, StrippedTurn = "Turn 4", CreatedAt = DateTime.UtcNow.AddMinutes(-7) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Does.Contain("[meta]"));
        Assert.That(_state.DialogueLog, Does.Contain("Older events this session"));
    }

    [Test]
    public async Task EnrichAsync_TruncatesVeryOldTurns()
    {
        // Arrange - Create many turns exceeding MaxDialogueLogTurns
        _state.MaxDialogueLogTurns = 3;
        _state.RecentTurnsCount = 2;

        var turns = new List<Turn>();
        for (var i = 1; i <= 10; i++)
        {
            turns.Add(new Turn
            {
                Id = i,
                SessionId = 1,
                Accepted = true,
                StrippedTurn = $"Turn {i}",
                CreatedAt = DateTime.UtcNow.AddMinutes(-100 + i)
            });
        }

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Does.Contain("Truncated"));
        Assert.That(_state.DialogueLog, Does.Contain("earlier turns"));
    }

    [Test]
    public async Task EnrichAsync_LogsWarningWhenStrippedTurnMissing()
    {
        // Arrange
        var turns = new[]
        {
            new Turn
            {
                Id = 1,
                SessionId = 1,
                Accepted = true,
                StrippedTurn = "   ", // Whitespace-only stripped turn
                Input = "Input",
                Response = "Response",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new Turn { Id = 2, SessionId = 1, Accepted = true, StrippedTurn = "S2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, StrippedTurn = "S3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, StrippedTurn = "S4", CreatedAt = DateTime.UtcNow.AddMinutes(-7) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("missing StrippedTurn")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_LogsDebugWithTurnCount()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, StrippedTurn = "T1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = true, StrippedTurn = "T2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, StrippedTurn = "T3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, StrippedTurn = "T4", CreatedAt = DateTime.UtcNow.AddMinutes(-7) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Built dialogue log")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_OnException_LogsWarning()
    {
        // Arrange - dispose context to cause exception
        _dbContext.Dispose();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to build dialogue log")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_IgnoresNonAcceptedTurns()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, StrippedTurn = "Accepted 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = false, StrippedTurn = "Not accepted", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, StrippedTurn = "Accepted 2", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, StrippedTurn = "Accepted 3", CreatedAt = DateTime.UtcNow.AddMinutes(-7) },
            new Turn { Id = 5, SessionId = 1, Accepted = true, StrippedTurn = "Accepted 4", CreatedAt = DateTime.UtcNow.AddMinutes(-6) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.DialogueLog, Does.Not.Contain("Not accepted"));
    }
}