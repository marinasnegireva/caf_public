using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests specifically for data uniqueness during the enrichment process.
/// These tests verify:
/// 1. Deduplication within single enricher (AlwaysOn + Manual + Trigger have same ID)
/// 2. Deduplication across multiple enricher calls
/// 3. SeenIds tracking in DataTypeEnricherBase
/// 4. State.AddContextData uniqueness enforcement
/// </summary>
[TestFixture]
public class EnricherUniquenessTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<MemoryDataEnricher>> _mockMemoryLogger = null!;
    private Mock<ILogger<InsightEnricher>> _mockInsightLogger = null!;
    private Mock<ILogger<GenericDataEnricher>> _mockDataLogger = null!;
    private MemoryDataEnricher _memoryEnricher = null!;
    private InsightEnricher _insightEnricher = null!;
    private GenericDataEnricher _dataEnricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockMemoryLogger = new Mock<ILogger<MemoryDataEnricher>>();
        _mockInsightLogger = new Mock<ILogger<InsightEnricher>>();
        _mockDataLogger = new Mock<ILogger<GenericDataEnricher>>();

        _memoryEnricher = new MemoryDataEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockMemoryLogger.Object);

        _insightEnricher = new InsightEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockInsightLogger.Object);

        _dataEnricher = new GenericDataEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockDataLogger.Object);
    }

    #region Single Enricher Deduplication Tests

    [Test]
    public async Task MemoryEnricher_SameIdFromMultipleSources_OnlyAddsOnce()
    {
        // Arrange - Same memory appears in AlwaysOn AND Manual AND Trigger
        var state = CreateMinimalState();
        var sharedMemory = new ContextData { Id = 1, Name = "Shared Memory", Type = DataType.Memory, Content = "Important memory" };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]); // Same ID returned by Manual
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]); // Same ID returned by Trigger

        // Act
        await _memoryEnricher.EnrichAsync(state);

        // Assert - Should only be added once
        Assert.That(state.Memories, Has.Count.EqualTo(1));
        Assert.That(state.Memories.Count(m => m.Id == 1), Is.EqualTo(1));
    }

    [Test]
    public async Task InsightEnricher_SameIdFromAlwaysOnAndManual_OnlyAddsOnce()
    {
        // Arrange
        var state = CreateMinimalState();
        var sharedInsight = new ContextData { Id = 10, Name = "Shared Insight", Type = DataType.Insight, Content = "Important insight" };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedInsight]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedInsight]);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _insightEnricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Insights, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DataEnricher_SameIdFromAlwaysOnAndTrigger_OnlyAddsOnce()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Tell me about castle info" };

        var sharedData = new ContextData { Id = 5, Name = "Castle Info", Type = DataType.Generic, Content = "Info", TriggerKeywords = "castle" };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedData]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedData]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedData]);

        // Act
        await _dataEnricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Data, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Enricher_OnlyRecordsTriggerActivation_IfNotAlreadyFromOtherSource()
    {
        // Arrange - Same memory from AlwaysOn AND Trigger
        var state = CreateMinimalState();
        var sharedMemory = new ContextData { Id = 1, Name = "Memory", Type = DataType.Memory };

        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([sharedMemory]);

        // Act
        await _memoryEnricher.EnrichAsync(state);

        // Assert - Trigger activation should NOT be recorded because it was already added from AlwaysOn
        _mockContextDataService.Verify(s => s.RecordTriggerActivationAsync(1, It.IsAny<CancellationToken>()), Times.Never,
            "Trigger activation should not be recorded for items already loaded from other sources");
    }

    #endregion Single Enricher Deduplication Tests

    #region Multiple Enricher Deduplication Tests

    [Test]
    public async Task MultipleEnrichers_DifferentDataTypes_AllAddToState()
    {
        // Arrange
        var state = CreateMinimalState();

        var memory = new ContextData { Id = 1, Name = "Memory", Type = DataType.Memory };
        var insight = new ContextData { Id = 2, Name = "Insight", Type = DataType.Insight };
        var data = new ContextData { Id = 3, Name = "Data", Type = DataType.Generic };

        SetupMockForMemory([memory]);
        SetupMockForInsight([insight]);
        SetupMockForData([data]);

        // Act
        await _memoryEnricher.EnrichAsync(state);
        await _insightEnricher.EnrichAsync(state);
        await _dataEnricher.EnrichAsync(state);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(state.Memories, Has.Count.EqualTo(1));
            Assert.That(state.Insights, Has.Count.EqualTo(1));
            Assert.That(state.Data, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task MultipleEnrichers_SameEnricherCalledTwice_NoDuplicates()
    {
        // Arrange
        var state = CreateMinimalState();
        var memory = new ContextData { Id = 1, Name = "Memory", Type = DataType.Memory };
        SetupMockForMemory([memory]);

        // Act - Call same enricher twice
        await _memoryEnricher.EnrichAsync(state);
        await _memoryEnricher.EnrichAsync(state);

        // Assert - State's AddContextData should prevent duplicate
        Assert.That(state.Memories, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task State_PreventsDuplicates_WhenEnrichersAddSameIdByMistake()
    {
        // Arrange - Simulate bug where wrong type is returned
        var state = CreateMinimalState();

        // Memory enricher correctly adds Memory type with ID 1
        var memory = new ContextData { Id = 1, Name = "Memory", Type = DataType.Memory };
        SetupMockForMemory([memory]);

        // Another memory with same ID but different content (edge case)
        var memoryDuplicate = new ContextData { Id = 1, Name = "Memory Duplicate", Type = DataType.Memory };

        // Act
        await _memoryEnricher.EnrichAsync(state);
        state.AddContextData(memoryDuplicate); // Direct add attempt

        // Assert - Should only have one entry
        Assert.That(state.Memories, Has.Count.EqualTo(1));
        Assert.That(state.Memories.First().Name, Is.EqualTo("Memory")); // First one wins
    }

    #endregion Multiple Enricher Deduplication Tests

    #region Parallel Enrichment Deduplication Tests

    [Test]
    public async Task ParallelEnrichment_SameState_NoDuplicates()
    {
        // Arrange
        var state = CreateMinimalState();

        var memory = new ContextData { Id = 1, Name = "Memory", Type = DataType.Memory };
        var insight = new ContextData { Id = 2, Name = "Insight", Type = DataType.Insight };
        var data = new ContextData { Id = 3, Name = "Data", Type = DataType.Generic };

        SetupMockForMemory([memory]);
        SetupMockForInsight([insight]);
        SetupMockForData([data]);

        // Act - Parallel execution (like ConversationEnrichmentOrchestrator does)
        await Task.WhenAll(
            _memoryEnricher.EnrichAsync(state),
            _insightEnricher.EnrichAsync(state),
            _dataEnricher.EnrichAsync(state));

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(state.Memories, Has.Count.EqualTo(1));
            Assert.That(state.Insights, Has.Count.EqualTo(1));
            Assert.That(state.Data, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ParallelEnrichment_HighVolumeData_MaintainsUniqueness()
    {
        // Arrange
        var state = CreateMinimalState();

        // Create many items that could potentially duplicate
        // Note: Memories 1-40 will come from AlwaysOn (1-30) and Manual (21-40)
        // Memories 41-50 would require trigger evaluation, which is not part of MemoryDataEnricher's direct flow
        var memories = Enumerable.Range(1, 50)
            .Select(i => new ContextData { Id = i, Name = $"Memory {i}", Type = DataType.Memory })
            .ToList();

        // Setup to return duplicates across AlwaysOn and Manual sources
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. memories.Take(30)]); // IDs 1-30
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. memories.Skip(10).Take(40)]); // IDs 11-50, overlaps with 11-30 from AlwaysOn
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _memoryEnricher.EnrichAsync(state);

        // Assert - Should have exactly 50 unique memories
        Assert.That(state.Memories, Has.Count.EqualTo(50));
        Assert.That(state.Memories.Select(m => m.Id).Distinct().Count(), Is.EqualTo(50));
    }

    #endregion Parallel Enrichment Deduplication Tests

    #region Edge Cases

    [Test]
    public async Task Enricher_EmptyResults_NoErrors()
    {
        // Arrange
        var state = CreateMinimalState();
        SetupMockForMemory([]);

        // Act
        await _memoryEnricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Memories, Is.Empty);
    }

    [Test]
    public async Task State_PrePopulated_NewEnricherAdditionsRespectExisting()
    {
        // Arrange
        var state = CreateMinimalState();

        // Pre-populate state with a memory
        var existingMemory = new ContextData { Id = 1, Name = "Existing Memory", Type = DataType.Memory };
        state.AddContextData(existingMemory);

        // Setup enricher to return same ID
        var newMemoryWithSameId = new ContextData { Id = 1, Name = "New Memory Same ID", Type = DataType.Memory };
        SetupMockForMemory([newMemoryWithSameId]);

        // Act
        await _memoryEnricher.EnrichAsync(state);

        // Assert - Should still be only 1, and it should be the original
        Assert.That(state.Memories, Has.Count.EqualTo(1));
        Assert.That(state.Memories.First().Name, Is.EqualTo("Existing Memory"));
    }

    #endregion Edge Cases

    #region Helper Methods

    private static ConversationState CreateMinimalState()
    {
        return new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test input" },
            Session = new Session { Id = 1, Name = "Test Session" }
        };
    }

    private void SetupMockForMemory(List<ContextData> alwaysOn)
    {
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOn);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Memory, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private void SetupMockForInsight(List<ContextData> alwaysOn)
    {
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOn);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Insight, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private void SetupMockForData(List<ContextData> alwaysOn)
    {
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOn);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.Generic, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetTriggerDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.EvaluateTriggersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    private class TestDbContextFactory(DbContextOptions<GeneralDbContext> options)
        : IDbContextFactory<GeneralDbContext>
    {
        public GeneralDbContext CreateDbContext() => new(options);

        public Task<GeneralDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    #endregion Helper Methods
}