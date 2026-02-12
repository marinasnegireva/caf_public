using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

[TestFixture]
public class TurnHistoryEnricherTests
{
    private ServiceProvider _serviceProvider = null!;
    private GeneralDbContext _dbContext = null!;
    private Mock<ILogger<TurnHistoryEnricher>> _loggerMock = null!;
    private TurnHistoryEnricher _enricher = null!;
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

        _loggerMock = new Mock<ILogger<TurnHistoryEnricher>>();
        _enricher = new TurnHistoryEnricher(_serviceProvider, _loggerMock.Object);

        _state = new ConversationState
        {
            CurrentTurn = new Turn { Id = 10, Input = "Current input", SessionId = 1 },
            Session = new Session { Id = 1, Name = "Test Session" },
            RecentTurnsCount = 3
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
    public async Task EnrichAsync_WithNoAcceptedTurns_LogsDebugAndReturns()
    {
        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No accepted turns")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_LoadsRecentTurns()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, Input = "Turn 1", Response = "Response 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = true, Input = "Turn 2", Response = "Response 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, Input = "Turn 3", Response = "Response 3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) },
            new Turn { Id = 4, SessionId = 1, Accepted = true, Input = "Turn 4", Response = "Response 4", CreatedAt = DateTime.UtcNow.AddMinutes(-7) },
            new Turn { Id = 5, SessionId = 1, Accepted = true, Input = "Turn 5", Response = "Response 5", CreatedAt = DateTime.UtcNow.AddMinutes(-6) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert - verify the 3 most recent turns (IDs 3, 4, 5) are loaded
        Assert.That(_state.RecentTurns, Has.Count.EqualTo(3));
        var loadedIds = _state.RecentTurns.Select(t => t.Id).ToHashSet();
        Assert.That(loadedIds, Is.EquivalentTo([3, 4, 5]));
    }

    [Test]
    public async Task EnrichAsync_SetsPreviousTurnToMostRecent()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, Input = "Turn 1", Response = "Response 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = true, Input = "Turn 2", Response = "Response 2", CreatedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.PreviousTurn, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(_state.PreviousTurn!.Id, Is.EqualTo(2));
            Assert.That(_state.PreviousResponse, Is.EqualTo("Response 2"));
        });
    }

    [Test]
    public async Task EnrichAsync_IgnoresNonAcceptedTurns()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, Input = "Accepted 1", Response = "Response 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = false, Input = "Not accepted", Response = "Response 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) },
            new Turn { Id = 3, SessionId = 1, Accepted = true, Input = "Accepted 2", Response = "Response 3", CreatedAt = DateTime.UtcNow.AddMinutes(-8) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.RecentTurns, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(_state.RecentTurns.All(t => t.Accepted), Is.True);
            Assert.That(_state.RecentTurns.Any(t => t.Input == "Not accepted"), Is.False);
        });
    }

    [Test]
    public async Task EnrichAsync_OnlyLoadsFromCurrentSession()
    {
        // Arrange
        var session1Turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, Input = "Session 1", Response = "Response 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
        };
        var session2Turns = new[]
        {
            new Turn { Id = 2, SessionId = 2, Accepted = true, Input = "Session 2", Response = "Response 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) }
        };

        _dbContext.Turns.AddRange(session1Turns);
        _dbContext.Turns.AddRange(session2Turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.RecentTurns, Has.Count.EqualTo(1));
        Assert.That(_state.RecentTurns.ElementAt(0).SessionId, Is.EqualTo(1));
    }

    [Test]
    public async Task EnrichAsync_WithFewerTurnsThanLimit_LoadsAllTurns()
    {
        // Arrange
        _state.RecentTurnsCount = 10; // Request 10 turns
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, Input = "Turn 1", Response = "Response 1", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Turn { Id = 2, SessionId = 1, Accepted = true, Input = "Turn 2", Response = "Response 2", CreatedAt = DateTime.UtcNow.AddMinutes(-9) }
        };

        _dbContext.Turns.AddRange(turns);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.RecentTurns, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EnrichAsync_LogsDebugWithTurnCount()
    {
        // Arrange
        var turns = new[]
        {
            new Turn { Id = 1, SessionId = 1, Accepted = true, CreatedAt = DateTime.UtcNow }
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Loaded") && v.ToString()!.Contains("recent turns")),
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
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to get previous turns")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}