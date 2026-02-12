using CAF.Interfaces;
using CAF.Services.Conversation;
using CAF.Services.Conversation.Enrichment;

namespace Tests.UnitTests;

[TestFixture]
public class ConversationEnrichmentOrchestratorTests
{
    private Mock<ILogger<ConversationEnrichmentOrchestrator>> _loggerMock = null!;
    private ConversationState _state = null!;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<ConversationEnrichmentOrchestrator>>();
        _state = new ConversationState
        {
            CurrentTurn = new Turn
            {
                Id = 1,
                Input = "Test input",
                SessionId = 1
            },
            Session = new Session
            {
                Id = 1,
                Name = "Test Session"
            }
        };
    }

    [Test]
    public async Task EnrichAsync_WithNoEnrichers_CompletesSuccessfully()
    {
        // Arrange
        var enrichers = new List<IEnricher>();
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enrichment complete")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithSingleEnricher_CallsEnricher()
    {
        // Arrange
        var enricherMock = new Mock<IEnricher>();
        enricherMock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher> { enricherMock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        enricherMock.Verify(
            e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithMultipleEnrichers_CallsAllEnrichers()
    {
        // Arrange
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();
        var enricher3Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        enricher2Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        enricher3Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object,
            enricher3Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        enricher1Mock.Verify(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()), Times.Once);
        enricher2Mock.Verify(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()), Times.Once);
        enricher3Mock.Verify(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithCancellationToken_PassesTokenToEnrichers()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var enricherMock = new Mock<IEnricher>();
        enricherMock
            .Setup(e => e.EnrichAsync(_state, cts.Token))
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher> { enricherMock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state, cts.Token);

        // Assert
        enricherMock.Verify(e => e.EnrichAsync(_state, cts.Token), Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithNullState_LogsWarningAndReturns()
    {
        // Arrange
        var enricherMock = new Mock<IEnricher>();
        var enrichers = new List<IEnricher> { enricherMock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(null!);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("null state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        enricherMock.Verify(
            e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task EnrichAsync_WithNullCurrentTurn_LogsWarningAndReturns()
    {
        // Arrange
        var enricherMock = new Mock<IEnricher>();
        var enrichers = new List<IEnricher> { enricherMock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        var stateWithNullTurn = new ConversationState
        {
            CurrentTurn = null!
        };

        // Act
        await orchestrator.EnrichAsync(stateWithNullTurn);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("current turn")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        enricherMock.Verify(
            e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task EnrichAsync_EnrichersRunInParallel_VerifyTiming()
    {
        // Arrange
        var delayMs = 100;
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();
        var enricher3Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(async () => await Task.Delay(delayMs));
        enricher2Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(async () => await Task.Delay(delayMs));
        enricher3Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns(async () => await Task.Delay(delayMs));

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object,
            enricher3Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        var startTime = DateTime.UtcNow;
        await orchestrator.EnrichAsync(_state);
        var duration = DateTime.UtcNow - startTime;

        // Assert - if running in parallel, should take ~100ms, not ~300ms
        Assert.That(duration.TotalMilliseconds, Is.LessThan(delayMs * 2),
            "Enrichers should run in parallel, not sequentially");
    }

    [Test]
    public void EnrichAsync_WhenEnricherThrows_PropagatesException()
    {
        // Arrange
        var enricherMock = new Mock<IEnricher>();
        var expectedException = new InvalidOperationException("Enricher failed");

        enricherMock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var enrichers = new List<IEnricher> { enricherMock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.EnrichAsync(_state));
    }

    [Test]
    public void EnrichAsync_WhenMultipleEnrichersThrow_ThrowsFirstException()
    {
        // Arrange
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Enricher 1 failed"));
        enricher2Mock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Enricher 2 failed"));

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act & Assert
        // Note: When awaiting Task.WhenAll(), only the first exception is unwrapped and thrown
        // This is standard async/await behavior in .NET
        var ex = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await orchestrator.EnrichAsync(_state));
        Assert.That(ex!.Message, Does.Contain("failed"));
    }

    [Test]
    public async Task EnrichAsync_LogsDebugMessageAtStart()
    {
        // Arrange
        var enrichers = new List<IEnricher>();
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting enrichment")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_LogsInformationMessageAtEnd()
    {
        // Arrange
        var enrichers = new List<IEnricher>();
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Enrichment complete")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var enricherMock = new Mock<IEnricher>();
        enricherMock
            .Setup(e => e.EnrichAsync(_state, It.IsAny<CancellationToken>()))
            .Returns((ConversationState s, CancellationToken ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });

        var enrichers = new List<IEnricher> { enricherMock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await orchestrator.EnrichAsync(_state, cts.Token));
    }

    [Test]
    public async Task EnrichAsync_AllEnrichersReceiveSameState()
    {
        // Arrange
        ConversationState? capturedState1 = null;
        ConversationState? capturedState2 = null;

        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) => capturedState1 = s)
            .Returns(Task.CompletedTask);

        enricher2Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) => capturedState2 = s)
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(capturedState1, Is.SameAs(_state));
            Assert.That(capturedState2, Is.SameAs(_state));
        });
        Assert.That(capturedState1, Is.SameAs(capturedState2));
    }

    [Test]
    public async Task EnrichAsync_EnrichersCanModifySharedState()
    {
        // Arrange
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.Flags.Add(new Flag { Value = "Flag1" });
            })
            .Returns(Task.CompletedTask);

        enricher2Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.Flags.Add(new Flag { Value = "Flag2" });
            })
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        Assert.That(_state.Flags, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(_state.Flags.Any(f => f.Value == "Flag1"), Is.True);
            Assert.That(_state.Flags.Any(f => f.Value == "Flag2"), Is.True);
        });
    }

    [Test]
    public async Task EnrichAsync_WithEmptyEnrichersList_CompletesSuccessfully()
    {
        // Arrange
        var enrichers = Enumerable.Empty<IEnricher>();
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await orchestrator.EnrichAsync(_state));
    }

    #region Parallel Enrichment Uniqueness Tests

    [Test]
    public async Task EnrichAsync_ParallelEnrichers_AddingToDifferentCollections_AllDataAdded()
    {
        // Arrange
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();
        var enricher3Mock = new Mock<IEnricher>();

        // Each enricher adds to different collections
        enricher1Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.AddContextData(new ContextData { Id = 1, Name = "Memory 1", Type = DataType.Memory });
                s.AddContextData(new ContextData { Id = 2, Name = "Memory 2", Type = DataType.Memory });
            })
            .Returns(Task.CompletedTask);

        enricher2Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.AddContextData(new ContextData { Id = 10, Name = "Quote 1", Type = DataType.Quote });
                s.AddContextData(new ContextData { Id = 11, Name = "Quote 2", Type = DataType.Quote });
            })
            .Returns(Task.CompletedTask);

        enricher3Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.AddContextData(new ContextData { Id = 20, Name = "Insight 1", Type = DataType.Insight });
            })
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object,
            enricher3Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(_state.Memories, Has.Count.EqualTo(2));
            Assert.That(_state.Quotes, Has.Count.EqualTo(2));
            Assert.That(_state.Insights, Has.Count.EqualTo(1));
            Assert.That(_state.GetAllContextData().Count(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task EnrichAsync_ParallelEnrichers_AddingSameIds_DeduplicatesCorrectly()
    {
        // Arrange - Multiple enrichers try to add same ID
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.AddContextData(new ContextData { Id = 1, Name = "Memory from Enricher1", Type = DataType.Memory });
            })
            .Returns(Task.CompletedTask);

        enricher2Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                // Try to add same ID
                s.AddContextData(new ContextData { Id = 1, Name = "Memory from Enricher2 (Duplicate)", Type = DataType.Memory });
            })
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher>
        {
            enricher1Mock.Object,
            enricher2Mock.Object
        };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert - Should only have one memory with ID=1
        Assert.That(_state.Memories, Has.Count.EqualTo(1));
        Assert.That(_state.Memories.Count(m => m.Id == 1), Is.EqualTo(1));
    }

    [Test]
    public async Task EnrichAsync_ParallelEnrichers_HighVolume_MaintainsUniqueness()
    {
        // Arrange - Many enrichers adding many items with some overlap
        var enricherMocks = new List<Mock<IEnricher>>();
        var numEnrichers = 10;
        var itemsPerEnricher = 100;
        var overlapFactor = 5; // Each ID appears in ~5 enrichers on average

        for (var e = 0; e < numEnrichers; e++)
        {
            var enricherMock = new Mock<IEnricher>();
            var enricherIndex = e;
            enricherMock
                .Setup(x => x.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
                .Callback<ConversationState, CancellationToken>((s, ct) =>
                {
                    for (var i = 0; i < itemsPerEnricher; i++)
                    {
                        // Create overlapping IDs: enricher 0 uses 0-99, enricher 1 uses 20-119, etc.
                        var id = (enricherIndex * (itemsPerEnricher / overlapFactor)) + i;
                        s.AddContextData(new ContextData
                        {
                            Id = id,
                            Name = $"Memory {id} from Enricher {enricherIndex}",
                            Type = DataType.Memory
                        });
                    }
                })
                .Returns(Task.CompletedTask);
            enricherMocks.Add(enricherMock);
        }

        var enrichers = enricherMocks.Select(m => m.Object).Cast<IEnricher>().ToList();
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert - All IDs should be unique
        var allIds = _state.Memories.Select(m => m.Id).ToList();
        var uniqueIds = allIds.Distinct().Count();
        Assert.That(uniqueIds, Is.EqualTo(_state.Memories.Count),
            "All memory IDs should be unique after parallel enrichment");
    }

    [Test]
    public async Task EnrichAsync_ParallelEnrichers_ConcurrentWritesToSameCollection_ThreadSafe()
    {
        // Arrange - All enrichers write to the same collection type simultaneously
        var enricherMocks = new List<Mock<IEnricher>>();
        var numEnrichers = 5;

        for (var e = 0; e < numEnrichers; e++)
        {
            var enricherMock = new Mock<IEnricher>();
            var enricherIndex = e;
            enricherMock
                .Setup(x => x.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
                .Returns<ConversationState, CancellationToken>(async (s, ct) =>
                {
                    // Small delay to increase chances of race conditions
                    await Task.Delay(Random.Shared.Next(0, 10));
                    for (var i = 0; i < 50; i++)
                    {
                        // Each enricher uses unique IDs
                        s.AddContextData(new ContextData
                        {
                            Id = (enricherIndex * 1000) + i,
                            Name = $"Data from Enricher {enricherIndex}",
                            Type = DataType.Generic
                        });
                    }
                });
            enricherMocks.Add(enricherMock);
        }

        var enrichers = enricherMocks.Select(m => m.Object).Cast<IEnricher>().ToList();
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        Assert.DoesNotThrowAsync(async () => await orchestrator.EnrichAsync(_state),
            "Concurrent writes should not cause exceptions");

        // Assert
        Assert.That(_state.Data, Has.Count.EqualTo(numEnrichers * 50),
            "All items should be added without exceptions");
    }

    [Test]
    public async Task EnrichAsync_GetAllContextData_ReturnsOnlyUniqueAfterParallelEnrichment()
    {
        // Arrange
        var enricher1Mock = new Mock<IEnricher>();
        var enricher2Mock = new Mock<IEnricher>();

        enricher1Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                s.AddContextData(new ContextData { Id = 1, Name = "Memory", Type = DataType.Memory });
                s.AddContextData(new ContextData { Id = 2, Name = "Quote", Type = DataType.Quote });
            })
            .Returns(Task.CompletedTask);

        enricher2Mock
            .Setup(e => e.EnrichAsync(It.IsAny<ConversationState>(), It.IsAny<CancellationToken>()))
            .Callback<ConversationState, CancellationToken>((s, ct) =>
            {
                // Add different ID to same collection and same ID to different collection
                s.AddContextData(new ContextData { Id = 3, Name = "Memory 2", Type = DataType.Memory });
                s.AddContextData(new ContextData { Id = 2, Name = "Quote Dup Attempt", Type = DataType.Quote });
            })
            .Returns(Task.CompletedTask);

        var enrichers = new List<IEnricher> { enricher1Mock.Object, enricher2Mock.Object };
        var orchestrator = new ConversationEnrichmentOrchestrator(enrichers, _loggerMock.Object);

        // Act
        await orchestrator.EnrichAsync(_state);

        // Assert
        var allData = _state.GetAllContextData().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(allData.Select(d => d.Id).Distinct().Count(), Is.EqualTo(allData.Count),
                    "GetAllContextData should return only unique entries by ID");
            Assert.That(allData, Has.Count.EqualTo(3)); // 1 Memory, 1 Quote, 1 Memory 2
        });
    }

    #endregion Parallel Enrichment Uniqueness Tests
}