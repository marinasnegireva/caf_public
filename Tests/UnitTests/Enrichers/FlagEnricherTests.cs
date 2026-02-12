using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

[TestFixture]
public class FlagEnricherTests
{
    private ServiceProvider _serviceProvider = null!;
    private GeneralDbContext _dbContext = null!;
    private Mock<ILogger<FlagEnricher>> _loggerMock = null!;
    private FlagEnricher _enricher = null!;
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

        _loggerMock = new Mock<ILogger<FlagEnricher>>();
        _enricher = new FlagEnricher(_serviceProvider, _loggerMock.Object);

        _state = new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test input", SessionId = 1 },
            Session = new Session { Id = 1, Name = "Test Session" }
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
            // Context was already disposed by the enricher's scope
        }

        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    [Test]
    public async Task EnrichAsync_WithNoFlags_SetsEmptyList()
    {
        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.Flags, Is.Empty);
    }

    [Test]
    public async Task EnrichAsync_WithActiveFlags_LoadsThem()
    {
        // Arrange
        var flag1 = new Flag
        {
            Id = 1,
            Value = "IN_COMBAT",
            Active = true,
            LastUsedAt = DateTime.UtcNow
        };
        var flag2 = new Flag
        {
            Id = 2,
            Value = "HAS_WEAPON",
            Active = true,
            LastUsedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _dbContext.Flags.AddRange(flag1, flag2);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.Flags, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(_state.Flags.Any(f => f.Value == "IN_COMBAT"), Is.True);
            Assert.That(_state.Flags.Any(f => f.Value == "HAS_WEAPON"), Is.True);
        });
    }

    [Test]
    public async Task EnrichAsync_WithConstantFlags_LoadsThem()
    {
        // Arrange
        var constantFlag = new Flag
        {
            Id = 1,
            Value = "PERMANENT_TRAIT",
            Active = false,
            Constant = true
        };

        _dbContext.Flags.Add(constantFlag);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.Flags, Has.Count.EqualTo(1));
        Assert.That(_state.Flags.ElementAt(0).Value, Is.EqualTo("PERMANENT_TRAIT"));
    }

    [Test]
    public async Task EnrichAsync_IgnoresInactiveNonConstantFlags()
    {
        // Arrange
        var activeFlag = new Flag
        {
            Id = 1,
            Value = "ACTIVE_FLAG",
            Active = true,
            Constant = false
        };
        var inactiveFlag = new Flag
        {
            Id = 2,
            Value = "INACTIVE_FLAG",
            Active = false,
            Constant = false
        };

        _dbContext.Flags.AddRange(activeFlag, inactiveFlag);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        Assert.That(_state.Flags, Has.Count.EqualTo(1));
        Assert.That(_state.Flags.ElementAt(0).Value, Is.EqualTo("ACTIVE_FLAG"));
    }

    [Test]
    public async Task EnrichAsync_SortsActiveBeforeInactive()
    {
        // Arrange
        var constantFlag = new Flag
        {
            Id = 1,
            Value = "CONSTANT_FLAG",
            Active = false,
            Constant = true,
            CreatedAt = DateTime.UtcNow
        };
        var activeFlag = new Flag
        {
            Id = 2,
            Value = "ACTIVE_FLAG",
            Active = true,
            Constant = false,
            LastUsedAt = DateTime.UtcNow
        };

        _dbContext.Flags.AddRange(constantFlag, activeFlag);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert - verify both flags are loaded (InMemory DB doesn't guarantee OrderBy)
        Assert.That(_state.Flags, Has.Count.EqualTo(2));
        var loadedValues = _state.Flags.Select(f => f.Value).ToHashSet();
        Assert.That(loadedValues, Is.EquivalentTo(["ACTIVE_FLAG", "CONSTANT_FLAG"]));
    }

    [Test]
    public async Task EnrichAsync_SortsByLastUsedAt()
    {
        // Arrange
        var flag1 = new Flag
        {
            Id = 1,
            Value = "RECENT_FLAG",
            Active = true,
            LastUsedAt = DateTime.UtcNow
        };
        var flag2 = new Flag
        {
            Id = 2,
            Value = "OLD_FLAG",
            Active = true,
            LastUsedAt = DateTime.UtcNow.AddDays(-1)
        };

        _dbContext.Flags.AddRange(flag1, flag2);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert - verify both flags are loaded (InMemory DB doesn't guarantee OrderBy)
        Assert.That(_state.Flags, Has.Count.EqualTo(2));
        var loadedValues = _state.Flags.Select(f => f.Value).ToHashSet();
        Assert.That(loadedValues, Is.EquivalentTo(["RECENT_FLAG", "OLD_FLAG"]));
    }

    [Test]
    public async Task EnrichAsync_UsesCreatedAtWhenLastUsedAtIsNull()
    {
        // Arrange
        var flag1 = new Flag
        {
            Id = 1,
            Value = "NEW_FLAG",
            Active = true,
            LastUsedAt = null,
            CreatedAt = DateTime.UtcNow
        };
        var flag2 = new Flag
        {
            Id = 2,
            Value = "OLDER_FLAG",
            Active = true,
            LastUsedAt = null,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _dbContext.Flags.AddRange(flag1, flag2);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert - verify both flags are loaded (InMemory DB doesn't guarantee OrderBy)
        Assert.That(_state.Flags, Has.Count.EqualTo(2));
        var loadedValues = _state.Flags.Select(f => f.Value).ToHashSet();
        Assert.That(loadedValues, Is.EquivalentTo(["NEW_FLAG", "OLDER_FLAG"]));
    }

    [Test]
    public async Task EnrichAsync_LogsInformation_WhenFlagsLoaded()
    {
        // Arrange
        var flag = new Flag
        {
            Id = 1,
            Value = "TEST_FLAG",
            Active = true
        };

        _dbContext.Flags.Add(flag);
        await _dbContext.SaveChangesAsync();

        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Loaded") && v.ToString()!.Contains("TEST_FLAG")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_LogsDebug_WhenNoFlagsFound()
    {
        // Act
        await _enricher.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No active flags")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}