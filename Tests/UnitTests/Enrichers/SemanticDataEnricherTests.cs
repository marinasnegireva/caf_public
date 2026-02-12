using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for SemanticDataEnricher - handles Semantic availability data via vector search.
/// Adds results to type-specific collections (Quotes, Memories, Insights, PersonaVoiceSamples).
/// </summary>
[TestFixture]
public class SemanticDataEnricherTests
{
    private Mock<ISemanticService> _mockSemanticService = null!;
    private Mock<IProfileService> _mockProfileService = null!;
    private Mock<ISettingService> _mockSettingService = null!;
    private Mock<ILogger<SemanticDataEnricher>> _mockLogger = null!;
    private SemanticDataEnricher _enricher = null!;
    private const int TestProfileId = 1;

    [SetUp]
    public void Setup()
    {
        _mockSemanticService = new Mock<ISemanticService>();
        _mockProfileService = new Mock<IProfileService>();
        _mockSettingService = new Mock<ISettingService>();
        _mockLogger = new Mock<ILogger<SemanticDataEnricher>>();

        _mockProfileService.Setup(p => p.GetActiveProfileId()).Returns(TestProfileId);

        _enricher = new SemanticDataEnricher(
            _mockSemanticService.Object,
            _mockProfileService.Object,
            _mockSettingService.Object,
            _mockLogger.Object);
    }

    [Test]
    public async Task EnrichAsync_WithLLMTransformationEnabled_UsesQueryTransformation()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Tell me about the story" };

        var semanticResults = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Quote, [new() { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Relevant quote", TokenCount = 50 }] },
            { DataType.Memory, [new() { Id = 2, Name = "Memory 1", Type = DataType.Memory, Content = "Related memory", TokenCount = 50 }] }
        };

        SetupDefaultQuotas(1000);
        _mockSettingService.Setup(s => s.GetBoolAsync(CAF.Services.SettingsKeys.SemanticUseLLMQueryTransformation, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        await _enricher.EnrichAsync(state);

        Assert.Multiple(() =>
        {
            // Assert - Results are added to type-specific collections
            Assert.That(state.Quotes, Has.Count.EqualTo(1));
            Assert.That(state.Memories, Has.Count.EqualTo(1));
        });
        _mockSemanticService.Verify(s => s.SearchWithQueryTransformationAsync(
            state,
            TestProfileId,
            It.IsAny<Dictionary<DataType, int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EnrichAsync_WithLLMTransformationDisabled_UsesDirectSearch()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Tell me about the story" };

        var semanticResults = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Quote, [new() { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Relevant quote", TokenCount = 50 }] }
        };

        SetupDefaultQuotas(1000);
        _mockSettingService.Setup(s => s.GetBoolAsync(CAF.Services.SettingsKeys.SemanticUseLLMQueryTransformation, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockSemanticService.Setup(s => s.SearchMultiTypeAsync(
                "Tell me about the story",
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.Quotes, Has.Count.EqualTo(1));
        _mockSemanticService.Verify(s => s.SearchMultiTypeAsync(
            "Tell me about the story",
            TestProfileId,
            It.IsAny<Dictionary<DataType, int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task EnrichAsync_AddsToCorrectTypeSpecificCollections()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test input" };

        var semanticResults = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Quote, [new() { Id = 1, Name = "Quote", Type = DataType.Quote, Content = "Quote content", TokenCount = 50 }] },
            { DataType.Memory, [new() { Id = 2, Name = "Memory", Type = DataType.Memory, Content = "Memory content", TokenCount = 50 }] },
            { DataType.Insight, [new() { Id = 3, Name = "Insight", Type = DataType.Insight, Content = "Insight content", TokenCount = 50 }] },
            { DataType.PersonaVoiceSample, [new() { Id = 4, Name = "Voice", Type = DataType.PersonaVoiceSample, Content = "Voice content", TokenCount = 50 }] }
        };

        SetupDefaultQuotas(5000);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Each type goes to its own collection
        Assert.That(state.Quotes, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(state.Quotes.First().Name, Is.EqualTo("Quote"));
            Assert.That(state.Memories, Has.Count.EqualTo(1));
        });
        Assert.Multiple(() =>
        {
            Assert.That(state.Memories.First().Name, Is.EqualTo("Memory"));
            Assert.That(state.Insights, Has.Count.EqualTo(1));
        });
        Assert.Multiple(() =>
        {
            Assert.That(state.Insights.First().Name, Is.EqualTo("Insight"));
            Assert.That(state.PersonaVoiceSamples, Has.Count.EqualTo(1));
        });
        Assert.That(state.PersonaVoiceSamples.First().Name, Is.EqualTo("Voice"));
    }

    [Test]
    public async Task EnrichAsync_WithEmptyInput_DoesNotSearch()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "" };

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        _mockSemanticService.Verify(s => s.SearchWithQueryTransformationAsync(
            It.IsAny<ConversationState>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<DataType, int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mockSemanticService.Verify(s => s.SearchMultiTypeAsync(
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<DataType, int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(state.GetAllContextData().Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task EnrichAsync_WithAllQuotasZero_DoesNotSearch()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test input" };

        SetupDefaultQuotas(0);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        _mockSemanticService.Verify(s => s.SearchWithQueryTransformationAsync(
            It.IsAny<ConversationState>(),
            It.IsAny<int>(),
            It.IsAny<Dictionary<DataType, int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task EnrichAsync_AppliesCharacterQuotas()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test" };

        var longContent = new string('x', 500);
        var results = new Dictionary<DataType, List<ContextData>>
        {
            {
                DataType.Quote,
                [
                    new() { Id = 1, Name = "Q1", Type = DataType.Quote, Content = longContent, TokenCount = 500 },
                    new() { Id = 2, Name = "Q2", Type = DataType.Quote, Content = longContent, TokenCount = 500 },
                    new() { Id = 3, Name = "Q3", Type = DataType.Quote, Content = longContent, TokenCount = 500 }
                ]
            }
        };

        _mockSettingService.Setup(s => s.GetIntAsync(CAF.Services.SettingsKeys.SemanticTokenQuota_Quote, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(700);
        _mockSettingService.Setup(s => s.GetIntAsync(It.Is<CAF.Services.SettingsKeys>(k => k != CAF.Services.SettingsKeys.SemanticTokenQuota_Quote), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - should be limited by character quota
        Assert.That(state.Quotes.Sum(d => d.Content.Length), Is.LessThanOrEqualTo(700));
    }

    [Test]
    public async Task EnrichAsync_ExcludesAlreadyLoadedData()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test" };
        state.AddContextData(new ContextData { Id = 1, Name = "Already loaded", Type = DataType.Memory });

        var results = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Memory, [
                new() { Id = 1, Name = "Duplicate", Type = DataType.Memory, Content = "Already loaded", TokenCount = 50 },
                new() { Id = 2, Name = "New", Type = DataType.Memory, Content = "New content", TokenCount = 50 }
            ]}
        };

        SetupDefaultQuotas(5000);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - should exclude id=1 which was already loaded, only add id=2
        Assert.That(state.Memories, Has.Count.EqualTo(2));
        Assert.That(state.Memories.Any(m => m.Id == 2), Is.True);
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
        _mockSettingService.Setup(s => s.GetIntAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        Assert.Multiple(() =>
        {
            // Assert - Should not throw, collections remain empty
            Assert.That(state.Quotes, Is.Empty);
            Assert.That(state.Memories, Is.Empty);
        });
    }

    #region Semantic Uniqueness Tests

    [Test]
    public async Task EnrichAsync_SemanticResultsWithDuplicateIds_OnlyAddsUnique()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test" };

        // Semantic search returns duplicates (shouldn't happen, but defensive test)
        var results = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Quote, [
                new() { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Content 1", TokenCount = 50 },
                new() { Id = 1, Name = "Quote 1 Duplicate", Type = DataType.Quote, Content = "Content 1 Dup", TokenCount = 50 },
                new() { Id = 2, Name = "Quote 2", Type = DataType.Quote, Content = "Content 2", TokenCount = 50 }
            ]}
        };

        SetupDefaultQuotas(5000);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should only have 2 unique quotes
        Assert.That(state.Quotes, Has.Count.EqualTo(2));
        Assert.That(state.Quotes.Select(q => q.Id).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task EnrichAsync_MultipleTypesPreLoaded_ExcludesAllDuplicates()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test" };

        // Pre-load data of multiple types
        state.AddContextData(new ContextData { Id = 1, Name = "Quote Pre", Type = DataType.Quote });
        state.AddContextData(new ContextData { Id = 10, Name = "Memory Pre", Type = DataType.Memory });
        state.AddContextData(new ContextData { Id = 20, Name = "Insight Pre", Type = DataType.Insight });

        var results = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Quote, [
                new() { Id = 1, Name = "Quote Dup", Type = DataType.Quote, Content = "Duplicate", TokenCount = 50 },
                new() { Id = 2, Name = "Quote New", Type = DataType.Quote, Content = "New quote", TokenCount = 50 }
            ]},
            { DataType.Memory, [
                new() { Id = 10, Name = "Memory Dup", Type = DataType.Memory, Content = "Duplicate", TokenCount = 50 },
                new() { Id = 11, Name = "Memory New", Type = DataType.Memory, Content = "New memory", TokenCount = 50 }
            ]},
            { DataType.Insight, [
                new() { Id = 20, Name = "Insight Dup", Type = DataType.Insight, Content = "Duplicate", TokenCount = 50 },
                new() { Id = 21, Name = "Insight New", Type = DataType.Insight, Content = "New insight", TokenCount = 50 }
            ]}
        };

        SetupDefaultQuotas(5000);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Each collection should have 2 (1 pre-loaded + 1 new)
        Assert.That(state.Quotes, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(state.Quotes.Any(q => q.Name == "Quote Pre"), Is.True);
            Assert.That(state.Quotes.Any(q => q.Name == "Quote New"), Is.True);
            Assert.That(state.Quotes.Any(q => q.Name == "Quote Dup"), Is.False);

            Assert.That(state.Memories, Has.Count.EqualTo(2));
        });
        Assert.Multiple(() =>
        {
            Assert.That(state.Memories.Any(m => m.Name == "Memory Pre"), Is.True);
            Assert.That(state.Memories.Any(m => m.Name == "Memory New"), Is.True);

            Assert.That(state.Insights, Has.Count.EqualTo(2));
        });
        Assert.Multiple(() =>
        {
            Assert.That(state.Insights.Any(i => i.Name == "Insight Pre"), Is.True);
            Assert.That(state.Insights.Any(i => i.Name == "Insight New"), Is.True);
        });
    }

    [Test]
    public async Task EnrichAsync_SemanticRunsAfterOtherEnrichers_NoOverlap()
    {
        // Arrange - Simulate semantic enricher running after other enrichers have already populated state
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test" };

        // Data from AlwaysOn enrichers (Memory, Insight)
        state.AddContextData(new ContextData { Id = 100, Name = "AlwaysOn Memory", Type = DataType.Memory });
        state.AddContextData(new ContextData { Id = 101, Name = "AlwaysOn Insight", Type = DataType.Insight });

        // Data from Trigger enrichers
        state.AddContextData(new ContextData { Id = 200, Name = "Triggered Memory", Type = DataType.Memory });

        // Data from Manual enrichers
        state.AddContextData(new ContextData { Id = 300, Name = "Manual Quote", Type = DataType.Quote });

        // Semantic results include some IDs that are already loaded
        var results = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Memory, [
                new() { Id = 100, Name = "Semantic finds AlwaysOn Memory", Type = DataType.Memory, Content = "Overlap", TokenCount = 50 },
                new() { Id = 200, Name = "Semantic finds Triggered Memory", Type = DataType.Memory, Content = "Overlap", TokenCount = 50 },
                new() { Id = 500, Name = "Truly New Memory", Type = DataType.Memory, Content = "New", TokenCount = 50 }
            ]},
            { DataType.Quote, [
                new() { Id = 300, Name = "Semantic finds Manual Quote", Type = DataType.Quote, Content = "Overlap", TokenCount = 50 },
                new() { Id = 600, Name = "Truly New Quote", Type = DataType.Quote, Content = "New", TokenCount = 50 }
            ]},
            { DataType.Insight, [
                new() { Id = 101, Name = "Semantic finds AlwaysOn Insight", Type = DataType.Insight, Content = "Overlap", TokenCount = 50 },
                new() { Id = 700, Name = "Truly New Insight", Type = DataType.Insight, Content = "New", TokenCount = 50 }
            ]}
        };

        SetupDefaultQuotas(10000);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        // Memories: 100 (AlwaysOn), 200 (Triggered), 500 (New from Semantic) = 3
        Assert.That(state.Memories, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(state.Memories.Select(m => m.Id).Distinct().Count(), Is.EqualTo(3));

            // Quotes: 300 (Manual), 600 (New from Semantic) = 2
            Assert.That(state.Quotes, Has.Count.EqualTo(2));

            // Insights: 101 (AlwaysOn), 700 (New from Semantic) = 2
            Assert.That(state.Insights, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task EnrichAsync_GetAllContextData_ReturnsUniqueResults()
    {
        // Arrange
        var state = CreateMinimalState();
        state.CurrentTurn = new Turn { Id = 1, Input = "Test" };

        // Pre-populate with various data
        state.UserProfile = new ContextData { Id = 1, Name = "User", Type = DataType.CharacterProfile, IsUser = true };
        state.AddContextData(new ContextData { Id = 10, Name = "Memory 1", Type = DataType.Memory });
        state.AddContextData(new ContextData { Id = 20, Name = "Quote 1", Type = DataType.Quote });

        var results = new Dictionary<DataType, List<ContextData>>
        {
            { DataType.Memory, [new() { Id = 11, Name = "Memory 2", Type = DataType.Memory, Content = "Content", TokenCount = 50 }] },
            { DataType.Quote, [new() { Id = 21, Name = "Quote 2", Type = DataType.Quote, Content = "Content", TokenCount = 50 }] }
        };

        SetupDefaultQuotas(5000);
        _mockSettingService.Setup(s => s.GetBoolAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockSemanticService.Setup(s => s.SearchWithQueryTransformationAsync(
                It.IsAny<ConversationState>(),
                TestProfileId,
                It.IsAny<Dictionary<DataType, int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        var allData = state.GetAllContextData().ToList();
        Assert.That(allData.Select(d => d.Id).Distinct().Count(), Is.EqualTo(allData.Count),
            "GetAllContextData should return only unique entries");
    }

    #endregion Semantic Uniqueness Tests

    private void SetupDefaultQuotas(int quota)
    {
        _mockSettingService.Setup(s => s.GetIntAsync(It.IsAny<CAF.Services.SettingsKeys>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(quota);
    }

    private static ConversationState CreateMinimalState()
    {
        return new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test input" },
            Session = new Session { Id = 1, Name = "Test Session" },
            PersonaName = "Test Persona",
            UserName = "Test User"
        };
    }
}