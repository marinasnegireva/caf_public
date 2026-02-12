using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for MemoryDataEnricher - handles Memory data type.
/// Supports: AlwaysOn, Manual
/// Does NOT support: Semantic (handled by SemanticDataEnricher), Trigger (handled by separate TriggerEnricher)
/// </summary>
[TestFixture]
public class MemoryDataEnricherTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<MemoryDataEnricher>> _mockLogger = null!;
    private MemoryDataEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockLogger = new Mock<ILogger<MemoryDataEnricher>>();

        _enricher = new MemoryDataEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockLogger.Object);
    }

    [Test]
    public async Task EnrichAsync_LoadsAlwaysOnData()
    {
        // Arrange
        var state = CreateMinimalState();

        var alwaysOnData = new List<ContextData>
        {
            new() { Id = 1, Name = "Always On Memory", Type = DataType.Memory, Availability = AvailabilityType.AlwaysOn }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOnData);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Memories, Has.Count.EqualTo(1));
        Assert.That(state.Memories.ElementAt(0).Name, Is.EqualTo("Always On Memory"));
    }

    [Test]
    public async Task EnrichAsync_LoadsManualData()
    {
        // Arrange
        var state = CreateMinimalState();

        var manualData = new List<ContextData>
        {
            new() { Id = 2, Name = "Manual Memory", Type = DataType.Memory, Availability = AvailabilityType.Manual, UseEveryTurn = true }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manualData);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Memories, Has.Count.EqualTo(1));
        Assert.That(state.Memories.ElementAt(0).Name, Is.EqualTo("Manual Memory"));
    }

    [Test]
    public async Task EnrichAsync_DoesNotLoadTriggeredData()
    {
        // Arrange - MemoryDataEnricher only handles AlwaysOn and Manual
        // Trigger data is handled by the separate TriggerEnricher
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Hello world" };

        var triggeredData = new List<ContextData>
        {
            new() { Id = 3, Name = "Triggered Memory", Type = DataType.Memory, Availability = AvailabilityType.Trigger, TriggerKeywords = "hello" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Triggers are NOT loaded by MemoryDataEnricher
        Assert.That(state.Memories, Is.Empty);
        _mockContextDataService.Verify(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockContextDataService.Verify(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_CombinesAlwaysOnAndManual()
    {
        // Arrange - MemoryDataEnricher only handles AlwaysOn and Manual
        // Trigger data is handled by the separate TriggerEnricher
        var state = CreateMinimalState();

        var alwaysOn = new ContextData { Id = 1, Name = "AlwaysOn", Type = DataType.Memory };
        var manual = new ContextData { Id = 2, Name = "Manual", Type = DataType.Memory };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alwaysOn]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([manual]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Memories, Has.Count.EqualTo(2));
        Assert.That(state.Memories.Select(m => m.Name), Is.EquivalentTo(["AlwaysOn", "Manual"]));
    }

    [Test]
    public async Task EnrichAsync_DeduplicatesData()
    {
        // Arrange
        var state = CreateMinimalState();

        var duplicateData = new ContextData { Id = 1, Name = "Duplicate", Type = DataType.Memory };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicateData]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicateData]); // Same item returned
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should only have one entry despite being returned twice
        Assert.That(state.Memories, Has.Count.EqualTo(1));
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
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw
        Assert.That(state.Memories, Is.Empty);
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