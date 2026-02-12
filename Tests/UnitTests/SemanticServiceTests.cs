using CAF.Interfaces;

namespace Tests.UnitTests;

/// <summary>
/// Comprehensive unit tests for SemanticService covering embedding and search operations
/// </summary>
[TestFixture]
public class SemanticServiceTests
{
    private Mock<IGeminiClient> _mockGeminiClient = null!;
    private Mock<IQdrantServiceFactory> _mockQdrantFactory = null!;
    private Mock<IQdrantService> _mockQdrantService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<SemanticService>> _mockLogger = null!;
    private SemanticService _service = null!;
    private const int TestProfileId = 1;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = new TestDbContextFactory(options);
        _mockGeminiClient = new Mock<IGeminiClient>();
        _mockQdrantFactory = new Mock<IQdrantServiceFactory>();
        _mockQdrantService = new Mock<IQdrantService>();
        _mockLogger = new Mock<ILogger<SemanticService>>();

        // Setup Qdrant factory to return mock service
        _mockQdrantFactory.Setup(f => f.CreateService(It.IsAny<string>()))
            .Returns(_mockQdrantService.Object);

        _mockQdrantService.Setup(s => s.EnsureCollectionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _service = new SemanticService(
            _mockGeminiClient.Object,
            _mockQdrantFactory.Object,
            _dbContextFactory,
            _mockLogger.Object);

        // Seed test profile
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.Profiles.Add(new Profile { Id = TestProfileId, Name = "Test Profile" });
        await db.SaveChangesAsync();
    }

    #region Collection Name Tests

    [Test]
    [TestCase(DataType.Quote, "context_quotes")]
    [TestCase(DataType.Memory, "context_memories")]
    [TestCase(DataType.Insight, "context_insights")]
    [TestCase(DataType.PersonaVoiceSample, "context_voice_samples")]
    public void GetCollectionName_ReturnsCorrectName(DataType type, string expectedName)
    {
        // Act
        var result = _service.GetCollectionName(type);

        // Assert
        Assert.That(result, Is.EqualTo(expectedName));
    }

    #endregion Collection Name Tests

    #region Semantic Eligibility Tests

    [Test]
    [TestCase(DataType.Quote, true)]
    [TestCase(DataType.Memory, true)]
    [TestCase(DataType.Insight, true)]
    [TestCase(DataType.PersonaVoiceSample, true)]
    [TestCase(DataType.CharacterProfile, false)]
    [TestCase(DataType.Generic, false)]
    public async Task EmbedAsync_ValidatesSemanticEligibility(DataType type, bool shouldEmbed)
    {
        // Arrange
        var availability = type is DataType.CharacterProfile or DataType.Generic
            ? AvailabilityType.AlwaysOn
            : AvailabilityType.Semantic;
        var data = await CreateTestContextDataAsync(type, availability);

        if (shouldEmbed)
        {
            _mockGeminiClient.Setup(c => c.EmbedBatchAsync(
                    It.IsAny<List<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new float[768]]);

            _mockQdrantService.Setup(s => s.UpsertChunksBatchAsync(
                    It.IsAny<IReadOnlyList<(int Id, float[] Embedding, string PayloadId, string Text, long? SessionId, string EntryType, string Speaker, int DbPK, int ProfileId)>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // Act
        await _service.EmbedAsync(data);

        // Assert
        _mockGeminiClient.Verify(c => c.EmbedBatchAsync(
            It.IsAny<List<string>>(),
            It.IsAny<CancellationToken>()),
            shouldEmbed ? Times.Once() : Times.Never());
    }

    #endregion Semantic Eligibility Tests

    #region Stats Tests

    [Test]
    public async Task GetStatsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        await CreateTestContextDataAsync(DataType.Quote, AvailabilityType.Semantic, inVectorDb: true);
        await CreateTestContextDataAsync(DataType.Quote, AvailabilityType.Semantic, inVectorDb: false);
        await CreateTestContextDataAsync(DataType.Memory, AvailabilityType.Semantic, inVectorDb: true);

        // Act
        var stats = await _service.GetStatsAsync(TestProfileId);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(stats.TotalEmbedded, Is.EqualTo(2));
            Assert.That(stats.TotalPending, Is.EqualTo(1));
            Assert.That(stats.EmbeddedByType[DataType.Quote], Is.EqualTo(1));
            Assert.That(stats.EmbeddedByType[DataType.Memory], Is.EqualTo(1));
            Assert.That(stats.PendingByType[DataType.Quote], Is.EqualTo(1));
        });
    }

    #endregion Stats Tests

    #region EnsureCollections Tests

    [Test]
    public async Task EnsureCollectionsAsync_CreatesAllRequiredCollections()
    {
        // Act
        await _service.EnsureCollectionsAsync();

        // Assert - Should create collections for all semantic-eligible types
        _mockQdrantFactory.Verify(f => f.CreateService("context_quotes"), Times.Once);
        _mockQdrantFactory.Verify(f => f.CreateService("context_memories"), Times.Once);
        _mockQdrantFactory.Verify(f => f.CreateService("context_insights"), Times.Once);
        _mockQdrantFactory.Verify(f => f.CreateService("context_voice_samples"), Times.Once);
    }

    #endregion EnsureCollections Tests

    #region Helper Methods

    private async Task<ContextData> CreateTestContextDataAsync(
        DataType type,
        AvailabilityType availability,
        bool inVectorDb = false)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var data = new ContextData
        {
            Name = $"Test {type} {Guid.NewGuid():N}",
            Content = "Test content for embedding",
            Type = type,
            Availability = availability,
            InVectorDb = inVectorDb,
            IsEnabled = true,
            ProfileId = TestProfileId
        };
        db.ContextData.Add(data);
        await db.SaveChangesAsync();
        return data;
    }

    #endregion Helper Methods

    private class TestDbContextFactory(DbContextOptions<GeneralDbContext> options)
        : IDbContextFactory<GeneralDbContext>
    {
        public GeneralDbContext CreateDbContext()
        {
            return new GeneralDbContext(options);
        }

        public Task<GeneralDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}