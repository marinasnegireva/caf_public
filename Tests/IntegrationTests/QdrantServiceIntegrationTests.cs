using Microsoft.Extensions.Logging.Abstractions;
using Qdrant.Client;

namespace Tests.IntegrationTests;

[TestFixture]
public class QdrantServiceIntegrationTests
{
    private const string TestHost = "localhost";
    private const int TestPort = 6334;
    private const int DefaultDimensions = 3072;
    private string _testCollection = null!;
    private IQdrantServiceFactory _factory = null!;

    [SetUp]
    public void Setup()
    {
        // Use unique collection name for each test
        _testCollection = $"test_collection_{Guid.NewGuid():N}";

        // Create factory with test options
        var options = Options.Create(new QdrantOptions
        {
            Host = TestHost,
            Port = TestPort
        });
        _factory = new QdrantServiceFactory(options, NullLogger<QdrantService>.Instance);

        // Check if Qdrant is available
        try
        {
            using var client = new QdrantClient(TestHost, TestPort);
            var collections = client.ListCollectionsAsync().GetAwaiter().GetResult();
            // If we get here, Qdrant is running
        }
        catch (Exception)
        {
            Assert.Ignore("Qdrant is not running on localhost:6333");
        }
    }

    [TearDown]
    public async Task TearDown()
    {
        // Clean up test collection
        try
        {
            using var client = new QdrantClient(TestHost, TestPort);
            await client.DeleteCollectionAsync(_testCollection);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task EnsureCollectionAsync_CreatesNewCollection()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);

        // Act
        await service.EnsureCollectionAsync();

        // Assert - Verify collection was created
        using var client = new QdrantClient(TestHost, TestPort);
        var collections = await client.ListCollectionsAsync();
        var collectionNames = collections.Select(c => c).ToList();
        Assert.That(collectionNames.Contains(_testCollection), Is.True,
            "Collection should be created");
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task EnsureCollectionAsync_IdempotentCall_DoesNotThrow()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        // Act & Assert - Should not throw on second call
        Assert.DoesNotThrowAsync(async () => await service.EnsureCollectionAsync());
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task UpsertChunksBatchAsync_InsertsEmbeddings()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var embedding = CreateTestEmbedding(DefaultDimensions);
        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>
        {
            (1, embedding, "test#1#full", "Test content 1", 42, "quote_full", "Speaker1", 100, 1),
            (2, embedding, "test#2#full", "Test content 2", 43, "quote_full", "Speaker2", 101, 1)
        };

        // Act
        await service.UpsertChunksBatchAsync(chunks);

        // Assert - Verify points were inserted
        using var client = new QdrantClient(TestHost, TestPort);
        var info = await client.GetCollectionInfoAsync(_testCollection);
        Assert.That(info.PointsCount, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task UpsertChunksBatchAsync_EmptyList_DoesNothing()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var emptyChunks = new List<(int, float[], string, string, long?, string, string, int, int)>();

        // Act & Assert - Should not throw
        Assert.DoesNotThrowAsync(async () =>
            await service.UpsertChunksBatchAsync(emptyChunks));
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task SearchWithEmbeddingAsync_ReturnsRelevantResults()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var embedding1 = CreateTestEmbedding(DefaultDimensions);
        var embedding2 = CreateTestEmbedding(DefaultDimensions, offset: 0.5f);

        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>
        {
            (1, embedding1, "test#1#full", "Content about cats", 42, "quote_full", "Speaker1", 100, 1),
            (2, embedding2, "test#2#full", "Content about dogs", 43, "quote_full", "Speaker2", 101, 1)
        };

        await service.UpsertChunksBatchAsync(chunks);

        // Wait a bit for indexing
        await Task.Delay(500);

        // Act - Search with embedding similar to embedding1
        var results = await service.SearchWithEmbeddingAsync(embedding1, k: 2);

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.Multiple(() =>
        {
            Assert.That(results[0].PayloadId, Does.Contain("test#"));
            Assert.That(results[0].Score, Is.GreaterThan(0));
        });
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task SearchWithEmbeddingAsync_LimitResults_ReturnsCorrectCount()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var embedding = CreateTestEmbedding(DefaultDimensions);
        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>();

        for (var i = 1; i <= 10; i++)
        {
            var variedEmbedding = CreateTestEmbedding(DefaultDimensions, offset: i * 0.01f);
            chunks.Add((i, variedEmbedding, $"test#{i}#full", $"Content {i}", 42, "quote_full", $"Speaker{i}", 100 + i, 1));
        }

        await service.UpsertChunksBatchAsync(chunks);
        await Task.Delay(500);

        // Act
        var results = await service.SearchWithEmbeddingAsync(embedding, k: 3);

        // Assert
        Assert.That(results, Has.Count.LessThanOrEqualTo(3));
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task SearchWithEmbeddingAsync_IncludesAllPayloadFields()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var embedding = CreateTestEmbedding(DefaultDimensions);
        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>
        {
            (1, embedding, "test#1#semantic", "Test semantic content", 42, "quote_semantic", "TestSpeaker", 123, 1)
        };

        await service.UpsertChunksBatchAsync(chunks);
        await Task.Delay(500);

        // Act
        var results = await service.SearchWithEmbeddingAsync(embedding, k: 1);

        // Assert
        Assert.That(results, Has.Count.EqualTo(1));
        var result = results[0];
        Assert.Multiple(() =>
        {
            Assert.That(result.PayloadId, Is.EqualTo("test#1#semantic"));
            Assert.That(result.Json, Is.EqualTo("Test semantic content"));
            Assert.That(result.Session, Is.EqualTo(42));
            Assert.That(result.EntryType, Is.EqualTo("quote_semantic"));
            Assert.That(result.DbPK, Is.EqualTo(123));
            Assert.That(result.Score, Is.GreaterThan(0));
        });
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task SearchWithEmbeddingAsync_NoResults_ReturnsEmptyList()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var embedding = CreateTestEmbedding(DefaultDimensions);

        // Act - Search in empty collection
        var results = await service.SearchWithEmbeddingAsync(embedding, k: 5);

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results, Is.Empty);
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task UpsertChunksBatchAsync_UpdatesExistingPoint()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var embedding = CreateTestEmbedding(DefaultDimensions);
        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>
        {
            (1, embedding, "test#1#full", "Original content", 42, "quote_full", "Speaker1", 100, 1)
        };

        await service.UpsertChunksBatchAsync(chunks);
        await Task.Delay(500);

        // Act - Upsert with same ID but different content
        var updatedChunks = new List<(int, float[], string, string, long?, string, string, int, int)>
        {
            (1, embedding, "test#1#full", "Updated content", 42, "quote_full", "Speaker1", 100, 1)
        };
        await service.UpsertChunksBatchAsync(updatedChunks);
        await Task.Delay(500);

        // Assert - Should still have only 1 point
        using var client = new QdrantClient(TestHost, TestPort);
        var info = await client.GetCollectionInfoAsync(_testCollection);
        Assert.That(info.PointsCount, Is.EqualTo(1));

        // Verify content was updated
        var results = await service.SearchWithEmbeddingAsync(embedding, k: 1);
        Assert.That(results[0].Json, Is.EqualTo("Updated content"));
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task SearchWithEmbeddingAsync_CosineSimilarity_ReturnsOrderedResults()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var queryEmbedding = CreateTestEmbedding(DefaultDimensions);
        var similarEmbedding = CreateTestEmbedding(DefaultDimensions, offset: 0.01f);  // Very similar
        var differentEmbedding = CreateTestEmbedding(DefaultDimensions, offset: 0.5f); // Less similar

        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>
        {
            (1, similarEmbedding, "similar", "Similar content", 1, "type", "Speaker", 1, 1),
            (2, differentEmbedding, "different", "Different content", 2, "type", "Speaker", 2, 1)
        };

        await service.UpsertChunksBatchAsync(chunks);
        await Task.Delay(500);

        // Act
        var results = await service.SearchWithEmbeddingAsync(queryEmbedding, k: 2);

        // Assert - First result should be more similar (higher score)
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(results[0].Score, Is.GreaterThan(results[1].Score));
            Assert.That(results[0].PayloadId, Is.EqualTo("similar"));
        });
    }

    [Test]
    [Category("Integration")]
    [Category("Qdrant")]
    public async Task UpsertChunksBatchAsync_LargeBatch_HandlesEfficiently()
    {
        // Arrange
        var service = _factory.CreateService(_testCollection);
        await service.EnsureCollectionAsync();

        var chunks = new List<(int, float[], string, string, long?, string, string, int, int)>();
        for (var i = 0; i < 100; i++)
        {
            var embedding = CreateTestEmbedding(DefaultDimensions, offset: i * 0.001f);
            chunks.Add((i, embedding, $"test#{i}#full", $"Content {i}", i, "quote_full", $"Speaker{i}", 1000 + i, 1));
        }

        // Act
        var startTime = DateTime.UtcNow;
        await service.UpsertChunksBatchAsync(chunks);
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.That(duration.TotalSeconds, Is.LessThan(10), "Batch upsert should be reasonably fast");

        using var client = new QdrantClient(TestHost, TestPort);
        var info = await client.GetCollectionInfoAsync(_testCollection);
        Assert.That(info.PointsCount, Is.EqualTo(100));
    }

    /// <summary>
    /// Helper method to create a test embedding vector
    /// </summary>
    private static float[] CreateTestEmbedding(int dimensions, float offset = 0f)
    {
        var embedding = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)Math.Sin(i * 0.1) + offset;
        }

        // Normalize the vector
        var magnitude = Math.Sqrt(embedding.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (var i = 0; i < dimensions; i++)
            {
                embedding[i] /= (float)magnitude;
            }
        }

        return embedding;
    }
}