using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for GenericDataEnricher - handles generic Data type.
/// Supports: AlwaysOn, Manual
/// Does NOT support: Semantic, Trigger (handled by separate TriggerEnricher)
/// </summary>
[TestFixture]
public class GenericDataEnricherTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<GenericDataEnricher>> _mockLogger = null!;
    private GenericDataEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockLogger = new Mock<ILogger<GenericDataEnricher>>();

        _enricher = new GenericDataEnricher(
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
            new() { Id = 1, Name = "Reference Data", Type = DataType.Generic, Availability = AvailabilityType.AlwaysOn, Content = "Important reference" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOnData);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Data, Has.Count.EqualTo(1));
        Assert.That(state.Data.ElementAt(0).Name, Is.EqualTo("Reference Data"));
    }

    [Test]
    public async Task EnrichAsync_LoadsManualData()
    {
        // Arrange
        var state = CreateMinimalState();

        var manualData = new List<ContextData>
        {
            new() { Id = 2, Name = "Manual Data", Type = DataType.Generic, Availability = AvailabilityType.Manual, UseNextTurnOnly = true }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manualData);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Data, Has.Count.EqualTo(1));
        Assert.That(state.Data.ElementAt(0).Name, Is.EqualTo("Manual Data"));
    }

    [Test]
    public async Task EnrichAsync_DoesNotLoadTriggeredData()
    {
        // Arrange - GenericDataEnricher only handles AlwaysOn and Manual
        // Trigger data is handled by the separate TriggerEnricher
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Tell me about the castle" };

        var triggeredData = new List<ContextData>
        {
            new() { Id = 3, Name = "Castle Info", Type = DataType.Generic, Availability = AvailabilityType.Trigger, TriggerKeywords = "castle,fortress" }
        };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Triggers are NOT loaded by GenericDataEnricher
        Assert.That(state.Data, Is.Empty);
        _mockContextDataService.Verify(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockContextDataService.Verify(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_CombinesAlwaysOnAndManual()
    {
        // Arrange - GenericDataEnricher only handles AlwaysOn and Manual
        // Trigger data is handled by the separate TriggerEnricher
        var state = CreateMinimalState();

        var alwaysOn = new ContextData { Id = 1, Name = "AlwaysOn", Type = DataType.Generic };
        var manual = new ContextData { Id = 2, Name = "Manual", Type = DataType.Generic };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([alwaysOn]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([manual]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Data, Has.Count.EqualTo(2));
        Assert.That(state.Data.Select(d => d.Name), Is.EquivalentTo(["AlwaysOn", "Manual"]));
    }

    [Test]
    public async Task EnrichAsync_FiltersManualDataByType()
    {
        // Arrange - Test that only Generic type data is loaded from Manual source
        var state = CreateMinimalState();

        var dataManual = new ContextData { Id = 1, Name = "Data Manual", Type = DataType.Generic };
        var memoryManual = new ContextData { Id = 2, Name = "Memory Manual", Type = DataType.Memory }; // Wrong type

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([dataManual, memoryManual]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Only Generic type should be added (ConversationState.AddContextData filters by type)
        Assert.That(state.Data, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(state.Data.ElementAt(0).Type, Is.EqualTo(DataType.Generic));
            Assert.That(state.Data.ElementAt(0).Name, Is.EqualTo("Data Manual"));
        });
    }

    [Test]
    public async Task EnrichAsync_DeduplicatesData()
    {
        // Arrange
        var state = CreateMinimalState();

        var duplicate = new ContextData { Id = 1, Name = "Duplicate", Type = DataType.Generic };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Data, Has.Count.EqualTo(1));
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
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw
        Assert.That(state.Data, Is.Empty);
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