using Microsoft.Extensions.Logging.Abstractions;

namespace Tests.UnitTests;

[TestFixture]
public class QdrantServiceTests
{
    private IQdrantServiceFactory _factory = null!;

    [SetUp]
    public void Setup()
    {
        var options = Options.Create(new QdrantOptions
        {
            Host = "localhost",
            Port = 6333
        });
        _factory = new QdrantServiceFactory(options, NullLogger<QdrantService>.Instance);
    }

    [Test]
    public void Factory_CreatesInstance()
    {
        // Arrange & Act
        var service = _factory.CreateService("test_collection");

        // Assert
        Assert.That(service, Is.Not.Null);
    }

    [Test]
    public void Factory_WithCustomOptions_CreatesInstance()
    {
        // Arrange
        var customOptions = Options.Create(new QdrantOptions
        {
            Host = "custom-host",
            Port = 6334
        });
        var customFactory = new QdrantServiceFactory(customOptions, NullLogger<QdrantService>.Instance);

        // Act
        var service = customFactory.CreateService("test_collection");

        // Assert
        Assert.That(service, Is.Not.Null);
    }

    [Test]
    public void VectorEntry_CanBeCreated()
    {
        // Arrange & Act
        var entry = new VectorEntry(
            PayloadId: "test#123#full",
            Score: 0.95f,
            Session: 42,
            EntryType: "quote_full",
            Json: "Test content",
            DbPK: 123,
            ProfileId: 1
        );

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(entry.PayloadId, Is.EqualTo("test#123#full"));
            Assert.That(entry.Score, Is.EqualTo(0.95f));
            Assert.That(entry.Session, Is.EqualTo(42));
            Assert.That(entry.EntryType, Is.EqualTo("quote_full"));
            Assert.That(entry.Json, Is.EqualTo("Test content"));
            Assert.That(entry.DbPK, Is.EqualTo(123));
            Assert.That(entry.ProfileId, Is.EqualTo(1));
        });
    }

    [Test]
    public void VectorEntry_RecordEquality()
    {
        // Arrange
        var entry1 = new VectorEntry("test#1", 0.9f, 1, "type1", "content1", 1, 1);
        var entry2 = new VectorEntry("test#1", 0.9f, 1, "type1", "content1", 1, 1);
        var entry3 = new VectorEntry("test#2", 0.9f, 1, "type1", "content1", 1, 1);

        // Assert
        Assert.That(entry1, Is.EqualTo(entry2));
        Assert.That(entry1, Is.Not.EqualTo(entry3));
    }
}