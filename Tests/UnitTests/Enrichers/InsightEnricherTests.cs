using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for InsightEnricher - handles Insight data type.
/// Supports: AlwaysOn, Manual, Trigger (Semantic handled by SemanticDataEnricher)
/// </summary>
[TestFixture]
public class InsightEnricherTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<InsightEnricher>> _mockLogger = null!;
    private InsightEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockLogger = new Mock<ILogger<InsightEnricher>>();

        _enricher = new InsightEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockLogger.Object);
    }

    [Test]
    public async Task EnrichAsync_LoadsAlwaysOnInsights()
    {
        // Arrange
        var state = CreateMinimalState();

        var alwaysOnData = new List<ContextData>
        {
            new() { Id = 1, Name = "Insight 1", Type = DataType.Insight, Availability = AvailabilityType.AlwaysOn, Content = "Key insight" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOnData);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Insights, Has.Count.EqualTo(1));
        Assert.That(state.Insights.ElementAt(0).Name, Is.EqualTo("Insight 1"));
    }

    [Test]
    public async Task EnrichAsync_LoadsManualInsights()
    {
        // Arrange
        var state = CreateMinimalState();

        var manualData = new List<ContextData>
        {
            new() { Id = 2, Name = "Manual Insight", Type = DataType.Insight, Availability = AvailabilityType.Manual, UseEveryTurn = true }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manualData);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Insights, Has.Count.EqualTo(1));
        Assert.That(state.Insights.ElementAt(0).Name, Is.EqualTo("Manual Insight"));
    }

    [Test]
    public async Task EnrichAsync_DoesNotLoadTriggeredInsights()
    {
        // Arrange - InsightEnricher only handles AlwaysOn and Manual
        // Trigger data is handled by the separate TriggerEnricher
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Tell me about emotions" };

        var triggeredData = new List<ContextData>
        {
            new() { Id = 3, Name = "Emotional Insight", Type = DataType.Insight, Availability = AvailabilityType.Trigger, TriggerKeywords = "emotion,feelings" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Triggers are NOT loaded by InsightEnricher
        Assert.That(state.Insights, Is.Empty);
        _mockContextDataService.Verify(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockContextDataService.Verify(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_CombinesAlwaysOnAndManual()
    {
        // Arrange - InsightEnricher only handles AlwaysOn and Manual
        // Trigger data is handled by the separate TriggerEnricher
        var state = CreateMinimalState();

        var alwaysOn = new ContextData { Id = 1, Name = "AlwaysOn", Type = DataType.Insight };
        var manual = new ContextData { Id = 2, Name = "Manual", Type = DataType.Insight };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alwaysOn]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([manual]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Insights, Has.Count.EqualTo(2));
        Assert.That(state.Insights.Select(i => i.Name), Is.EquivalentTo(["AlwaysOn", "Manual"]));
    }

    [Test]
    public async Task EnrichAsync_DeduplicatesData()
    {
        // Arrange
        var state = CreateMinimalState();

        var duplicate = new ContextData { Id = 1, Name = "Duplicate", Type = DataType.Insight };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Insights, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EnrichAsync_WithNullState_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _enricher.EnrichAsync(null!));
    }

    [Test]
    public async Task EnrichAsync_WithException_LogsErrorAndContinues()
    {
        // Arrange
        var state = CreateMinimalState();
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw
        Assert.That(state.Insights, Is.Empty);
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