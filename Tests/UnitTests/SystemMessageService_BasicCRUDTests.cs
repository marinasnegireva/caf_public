namespace Tests.UnitTests;

[TestFixture]
public class SystemMessageService_BasicCRUDTests : SystemMessageServiceTestBase
{
    #region GetAllAsync Tests

    [Test]
    public async Task GetAllAsync_NoFilter_ReturnsAllMessages()
    {
        await SeedStandardTestData();

        var result = await Service.GetAllAsync();

        Assert.That(result, Has.Count.EqualTo(3)); // Excludes archived by default
    }

    [Test]
    public async Task GetAllAsync_FilterByType_ReturnsOnlyMatchingType()
    {
        await SeedStandardTestData();

        var result = await Service.GetAllAsync(SystemMessage.SystemMessageType.Persona);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(SystemMessage.SystemMessageType.Persona));
    }

    [Test]
    public async Task GetAllAsync_ExcludeArchived_ReturnsOnlyNonArchived()
    {
        await SeedStandardTestData();

        var result = await Service.GetAllAsync(includeArchived: false);

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result, Has.All.Matches<SystemMessage>(m => !m.IsArchived));
    }

    [Test]
    public async Task GetAllAsync_IncludeArchived_ReturnsAllMessages()
    {
        await SeedStandardTestData();

        var result = await Service.GetAllAsync(includeArchived: true);

        Assert.That(result, Has.Count.EqualTo(4));
    }

    [Test]
    public async Task GetAllAsync_OrdersByCreatedAtDescending()
    {
        await DbContext.SystemMessages.AddRangeAsync(
            new SystemMessage { Name = "First", Content = "Content 1", CreatedAt = DateTime.UtcNow.AddDays(-2), ProfileId = 1 },
            new SystemMessage { Name = "Second", Content = "Content 2", CreatedAt = DateTime.UtcNow.AddDays(-1), ProfileId = 1 },
            new SystemMessage { Name = "Third", Content = "Content 3", CreatedAt = DateTime.UtcNow, ProfileId = 1 }
        );
        await DbContext.SaveChangesAsync();

        var result = await Service.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Name, Is.EqualTo("Third"));
            Assert.That(result[1].Name, Is.EqualTo("Second"));
            Assert.That(result[2].Name, Is.EqualTo("First"));
        });
    }

    #endregion GetAllAsync Tests

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingId_ReturnsMessage()
    {
        var message = await CreateTestMessage(name: "Test", content: "Content");

        var result = await Service.GetByIdAsync(message.Id);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Test"));
    }

    [Test]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        var result = await Service.GetByIdAsync(999);

        Assert.That(result, Is.Null);
    }

    #endregion GetByIdAsync Tests

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_ValidMessage_CreatesAndReturnsMessage()
    {
        var message = new SystemMessage
        {
            Name = "New Persona",
            Content = "You are a helpful assistant",
            Type = SystemMessage.SystemMessageType.Persona
        };

        var result = await Service.CreateAsync(message);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(result.Version, Is.EqualTo(1));
            Assert.That(result.CreatedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
        });

        var dbMessage = await DbContext.SystemMessages.FindAsync(result.Id);
        Assert.That(dbMessage, Is.Not.Null);
    }

    [Test]
    public async Task CreateAsync_SetsVersionToOne()
    {
        var message = new SystemMessage { Name = "Test", Content = "Content" };

        var result = await Service.CreateAsync(message);

        Assert.That(result.Version, Is.EqualTo(1));
    }

    #endregion CreateAsync Tests

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ExistingMessage_DeletesAndReturnsTrue()
    {
        var message = await CreateTestMessage();

        var result = await Service.DeleteAsync(message.Id);

        Assert.That(result, Is.True);
        var dbMessage = await ReloadEntityAsync(message.Id);
        Assert.That(dbMessage, Is.Null);
    }

    [Test]
    public async Task DeleteAsync_DeletesAllVersions()
    {
        var parent = await CreateTestMessage(name: "Parent");
        var child = new SystemMessage
        {
            Name = "Child",
            Content = "Content",
            Version = 2,
            ParentId = parent.Id
        };
        await DbContext.SystemMessages.AddAsync(child);
        await DbContext.SaveChangesAsync();

        var result = await Service.DeleteAsync(parent.Id);

        Assert.That(result, Is.True);
        var remaining = await DbContext.SystemMessages.ToListAsync();
        Assert.That(remaining, Is.Empty);
    }

    [Test]
    public async Task DeleteAsync_NonExistingMessage_ReturnsFalse()
    {
        var result = await Service.DeleteAsync(999);

        Assert.That(result, Is.False);
    }

    #endregion DeleteAsync Tests

    #region Archive/Restore Tests

    [Test]
    public async Task ArchiveAsync_SetsIsArchivedToTrue()
    {
        var message = await CreateTestMessage(isArchived: false);

        var result = await Service.ArchiveAsync(message.Id);

        Assert.That(result, Is.True);
        var dbMessage = await ReloadEntityAsync(message.Id);
        Assert.Multiple(() =>
        {
            Assert.That(dbMessage!.IsArchived, Is.True);
            Assert.That(dbMessage.ModifiedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task RestoreAsync_SetsIsArchivedToFalse()
    {
        var message = await CreateTestMessage(isArchived: true);

        var result = await Service.RestoreAsync(message.Id);

        Assert.That(result, Is.True);
        var dbMessage = await ReloadEntityAsync(message.Id);
        Assert.That(dbMessage!.IsArchived, Is.False);
    }

    #endregion Archive/Restore Tests
}