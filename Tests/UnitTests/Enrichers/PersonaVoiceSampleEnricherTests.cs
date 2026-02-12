using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for PersonaVoiceSampleEnricher - handles PersonaVoiceSample data type.
/// Supports: AlwaysOn (Semantic handled by SemanticDataEnricher)
/// Does NOT support: Manual, Trigger
/// </summary>
[TestFixture]
public class PersonaVoiceSampleEnricherTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<PersonaVoiceSampleEnricher>> _mockLogger = null!;
    private PersonaVoiceSampleEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockLogger = new Mock<ILogger<PersonaVoiceSampleEnricher>>();

        _enricher = new PersonaVoiceSampleEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockLogger.Object);
    }

    [Test]
    public async Task EnrichAsync_LoadsAlwaysOnVoiceSamples()
    {
        // Arrange
        var state = CreateMinimalState();

        var alwaysOnData = new List<ContextData>
        {
            new() { Id = 1, Name = "Voice Sample 1", Type = DataType.PersonaVoiceSample, Availability = AvailabilityType.AlwaysOn, Content = "Sample dialogue" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOnData);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.PersonaVoiceSamples, Has.Count.EqualTo(1));
        Assert.That(state.PersonaVoiceSamples.ElementAt(0).Name, Is.EqualTo("Voice Sample 1"));
    }

    [Test]
    public async Task EnrichAsync_DoesNotLoadManualData()
    {
        // Arrange
        var state = CreateMinimalState();

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Manual data should NOT be requested for PersonaVoiceSample
        _mockContextDataService.Verify(s => s.GetActiveManualDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_DoesNotEvaluateTriggers()
    {
        // Arrange
        var state = CreateMinimalState();

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Triggers should NOT be evaluated for PersonaVoiceSample
        _mockContextDataService.Verify(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_LoadsMultipleVoiceSamples()
    {
        // Arrange
        var state = CreateMinimalState();

        var samples = new List<ContextData>
        {
            new() { Id = 1, Name = "Sample 1", Type = DataType.PersonaVoiceSample, Content = "Dialogue 1" },
            new() { Id = 2, Name = "Sample 2", Type = DataType.PersonaVoiceSample, Content = "Dialogue 2" },
            new() { Id = 3, Name = "Sample 3", Type = DataType.PersonaVoiceSample, Content = "Narration" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()))
            .ReturnsAsync(samples);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.PersonaVoiceSamples, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task EnrichAsync_DeduplicatesData()
    {
        // Arrange
        var state = CreateMinimalState();

        var duplicate = new ContextData { Id = 1, Name = "Duplicate", Type = DataType.PersonaVoiceSample };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate, duplicate]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.PersonaVoiceSamples, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EnrichAsync_WithNullState_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _enricher.EnrichAsync(null!));
    }

    [Test]
    public async Task EnrichAsync_WithNullSession_DoesNotThrow()
    {
        // Arrange
        var state = new ConversationState { CurrentTurn = new Turn { Id = 1, Input = "Test" } };

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _enricher.EnrichAsync(state));
    }

    [Test]
    public async Task EnrichAsync_WithException_LogsErrorAndContinues()
    {
        // Arrange
        var state = CreateMinimalState();
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.PersonaVoiceSample, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw
        Assert.That(state.PersonaVoiceSamples, Is.Empty);
    }

    private static ConversationState CreateMinimalState()
    {
        return new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test input" },
            Session = new Session { Id = 1, Name = "Test Session" }
        };
    }

    private class TestDbContextFactory(DbContextOptions<GeneralDbContext> options)
        : IDbContextFactory<GeneralDbContext>
    {
        public GeneralDbContext CreateDbContext() => new(options);

        public Task<GeneralDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}