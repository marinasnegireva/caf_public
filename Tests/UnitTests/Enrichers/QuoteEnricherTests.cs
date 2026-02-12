using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for QuoteEnricher - handles Quote data type.
/// Supports: AlwaysOn, Manual (Semantic handled by SemanticDataEnricher)
/// Does NOT support: Trigger
/// </summary>
[TestFixture]
public class QuoteEnricherTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<QuoteEnricher>> _mockLogger = null!;
    private QuoteEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockLogger = new Mock<ILogger<QuoteEnricher>>();

        _enricher = new QuoteEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockLogger.Object);
    }

    [Test]
    public async Task EnrichAsync_LoadsAlwaysOnQuotes()
    {
        // Arrange
        var state = CreateMinimalState();

        var alwaysOnData = new List<ContextData>
        {
            new() { Id = 1, Name = "Quote 1", Type = DataType.Quote, Availability = AvailabilityType.AlwaysOn, Content = "Always on quote" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOnData);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Quotes, Has.Count.EqualTo(1));
        Assert.That(state.Quotes.ElementAt(0).Name, Is.EqualTo("Quote 1"));
    }

    [Test]
    public async Task EnrichAsync_LoadsManualQuotes()
    {
        // Arrange
        var state = CreateMinimalState();

        var manualData = new List<ContextData>
        {
            new() { Id = 2, Name = "Manual Quote", Type = DataType.Quote, Availability = AvailabilityType.Manual, UseEveryTurn = true, Content = "Manual quote content" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manualData);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Quotes, Has.Count.EqualTo(1));
        Assert.That(state.Quotes.ElementAt(0).Name, Is.EqualTo("Manual Quote"));
    }

    [Test]
    public async Task EnrichAsync_CombinesAlwaysOnAndManual()
    {
        // Arrange
        var state = CreateMinimalState();

        var alwaysOn = new ContextData { Id = 1, Name = "AlwaysOn Quote", Type = DataType.Quote };
        var manual = new ContextData { Id = 2, Name = "Manual Quote", Type = DataType.Quote };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alwaysOn]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([manual]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Quotes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EnrichAsync_DoesNotEvaluateTriggers()
    {
        // Arrange
        var state = CreateMinimalState();

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Triggers should NOT be evaluated for Quotes
        _mockContextDataService.Verify(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_DeduplicatesData()
    {
        // Arrange
        var state = CreateMinimalState();

        var duplicate = new ContextData { Id = 1, Name = "Duplicate", Type = DataType.Quote };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Quotes, Has.Count.EqualTo(1));
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
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Quote, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw
        Assert.That(state.Quotes, Is.Empty);
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