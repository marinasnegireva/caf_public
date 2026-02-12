using CAF.Interfaces;
using CAF.LLM.Logging;
using Microsoft.Extensions.Configuration;

namespace Tests.IntegrationTests;

[TestFixture]
public class GeminiEmbeddingIntegrationTests
{
    private IGeminiClient? _geminiClient;
    private GeminiOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CAF"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        _options = new GeminiOptions();
        configuration.GetSection(GeminiOptions.SectionName).Bind(_options);

        // Skip tests if no API key is configured
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            Assert.Ignore("Gemini API key not configured");
        }

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var llmLogger = loggerFactory.CreateLogger<LLMLogger>();
        var httpClient = new HttpClient();

        // Setup Gemini client with real API key from config
        var geminiOptions = Options.Create(_options);

        _geminiClient = new GeminiClient(geminiOptions, httpClient, null, llmLogger);
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedAsync_SingleText_ReturnsValidEmbedding()
    {
        // Arrange
        var text = "The quick brown fox jumps over the lazy dog.";

        // Act
        var embedding = await _geminiClient!.EmbedAsync(text);

        // Assert
        Assert.That(embedding, Is.Not.Null, "Embedding should not be null");
        Assert.That(embedding.Length, Is.EqualTo(_options.EmbeddingDimension),
            $"Embedding should have {_options.EmbeddingDimension} dimensions");

        // Check that embedding is not all zeros
        var hasNonZero = embedding.Any(v => Math.Abs(v) > 0.0001f);
        Assert.That(hasNonZero, Is.True, "Embedding should contain non-zero values");

        // Check that values are normalized (for cosine similarity)
        var magnitude = Math.Sqrt(embedding.Sum(v => v * v));
        Assert.That(magnitude, Is.GreaterThan(0.5).And.LessThan(1.5),
            "Embedding should have reasonable magnitude");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedBatchAsync_MultipleTexts_ReturnsCorrectCount()
    {
        // Arrange
        var texts = new[]
        {
            "Artificial intelligence is transforming technology.",
            "Machine learning algorithms process vast amounts of data.",
            "Neural networks mimic the human brain structure."
        };

        // Act
        var embeddings = await _geminiClient!.EmbedBatchAsync(texts);

        // Assert
        Assert.That(embeddings, Is.Not.Null, "Embeddings should not be null");
        Assert.That(embeddings, Has.Count.EqualTo(texts.Length),
            "Should return one embedding per input text");

        foreach (var embedding in embeddings)
        {
            Assert.That(embedding.Length, Is.EqualTo(_options.EmbeddingDimension),
                $"Each embedding should have {_options.EmbeddingDimension} dimensions");

            var hasNonZero = embedding.Any(v => Math.Abs(v) > 0.0001f);
            Assert.That(hasNonZero, Is.True, "Embedding should contain non-zero values");
        }
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedBatchAsync_SimilarTexts_ProducesSimilarEmbeddings()
    {
        // Arrange
        var similarTexts = new[]
        {
            "The cat sat on the mat.",
            "A feline rested on the rug."
        };

        var differentText = "Quantum physics explores subatomic particles.";

        // Act
        var allTexts = similarTexts.Append(differentText).ToArray();
        var embeddings = await _geminiClient!.EmbedBatchAsync(allTexts);

        // Calculate cosine similarity
        var similarity1 = CosineSimilarity(embeddings[0], embeddings[1]);
        var similarity2 = CosineSimilarity(embeddings[0], embeddings[2]);

        // Assert
        Assert.That(similarity1, Is.GreaterThan(similarity2),
            "Similar texts should have higher cosine similarity than different texts");

        Assert.That(similarity1, Is.GreaterThan(0.5),
            "Similar texts should have positive correlation");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedAsync_RetrievalDocumentType_SucceedsWithCorrectDimensions()
    {
        // Arrange
        var documentText = "This is a document that will be indexed for search.";

        // Act
        var embedding = await _geminiClient!.EmbedAsync(
            documentText);

        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.EqualTo(_options.EmbeddingDimension));
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedAsync_RetrievalQueryType_SucceedsWithCorrectDimensions()
    {
        // Arrange
        var queryText = "What is the capital of France?";

        // Act
        var embedding = await _geminiClient!.EmbedAsync(
            queryText);

        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.EqualTo(_options.EmbeddingDimension));
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedAsync_LongText_SucceedsWithValidEmbedding()
    {
        // Arrange
        var longText = string.Join(" ", Enumerable.Repeat(
            "This is a long text that contains many words and sentences.", 20));

        // Act
        var embedding = await _geminiClient!.EmbedAsync(longText);

        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.EqualTo(_options.EmbeddingDimension));

        var hasNonZero = embedding.Any(v => Math.Abs(v) > 0.0001f);
        Assert.That(hasNonZero, Is.True);
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedBatchAsync_SpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var texts = new[]
        {
            "Hello, World! 🌍",
            "Test with émojis and àccénts",
            "Unicode: 你好世界",
            "Math symbols: ∑ ∫ ∂"
        };

        // Act
        var embeddings = await _geminiClient!.EmbedBatchAsync(texts);

        // Assert
        Assert.That(embeddings, Has.Count.EqualTo(texts.Length));
        foreach (var embedding in embeddings)
        {
            Assert.That(embedding.Length, Is.EqualTo(_options.EmbeddingDimension));
        }
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedAsync_ConsecutiveCalls_ProducesSimilarEmbedding()
    {
        // Arrange
        var text = "Deterministic test for embedding consistency.";

        // Act
        var embedding1 = await _geminiClient!.EmbedAsync(text);
        var embedding2 = await _geminiClient!.EmbedAsync(text);

        // Calculate similarity
        var similarity = CosineSimilarity(embedding1, embedding2);

        // Assert
        Assert.That(similarity, Is.GreaterThan(0.95),
            "Same text should produce nearly identical embeddings");
    }

    [Test]
    [Category("Integration")]
    public void EmbedAsync_EmptyString_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _geminiClient!.EmbedAsync("");
        });
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task EmbedBatchAsync_EmptyList_ThrowsException()
    {
        // Arrange
        var emptyList = new List<string>();

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _geminiClient!.EmbedBatchAsync(emptyList);
        });

        Assert.That(ex!.Message, Does.Contain("At least one text is required"));
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException("Vectors must have the same length");

        float dotProduct = 0;
        float magnitudeA = 0;
        float magnitudeB = 0;

        for (var i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        return magnitudeA == 0 || magnitudeB == 0 ? 0 : dotProduct / (magnitudeA * magnitudeB);
    }
}