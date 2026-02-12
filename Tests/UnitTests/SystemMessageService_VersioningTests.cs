namespace Tests.UnitTests;

[TestFixture]
public class SystemMessageService_VersioningTests : SystemMessageServiceTestBase
{
    #region UpdateAsync Tests

    [Test]
    public async Task UpdateAsync_ExistingMessage_CreatesNewVersionAndSetsActive()
    {
        var message = new SystemMessage
        {
            Name = "Original",
            Content = "Original Content",
            Version = 1,
            IsActive = true
        };
        await DbContext.SystemMessages.AddAsync(message);
        await DbContext.SaveChangesAsync();
        var originalId = message.Id;

        var updatedMessage = new SystemMessage
        {
            Name = "Updated",
            Content = "Updated Content",
            Description = "New Description",
            Type = message.Type,
            ModifiedBy = "testuser"
        };

        var result = await Service.UpdateAsync(originalId, updatedMessage);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Id, Is.Not.EqualTo(originalId));
            Assert.That(result.Name, Is.EqualTo("Updated"));
            Assert.That(result.Content, Is.EqualTo("Updated Content"));
            Assert.That(result.Description, Is.EqualTo("New Description"));
            Assert.That(result.Version, Is.EqualTo(2));
            Assert.That(result.IsActive, Is.True);
            Assert.That(result.ParentId, Is.EqualTo(originalId));
            Assert.That(result.CreatedBy, Is.EqualTo("testuser"));
        });

        var originalVersion = await ReloadEntityAsync(originalId);
        Assert.That(originalVersion!.IsActive, Is.False);
    }

    [Test]
    public async Task UpdateAsync_ExistingVersion_CreatesNewVersionWithCorrectParentId()
    {
        var parent = new SystemMessage
        {
            Name = "Parent",
            Content = "Parent Content",
            Version = 1,
            IsActive = false
        };
        await DbContext.SystemMessages.AddAsync(parent);
        await DbContext.SaveChangesAsync();
        var parentId = parent.Id;

        var childVersion = new SystemMessage
        {
            Name = "Child",
            Content = "Child Content",
            Version = 2,
            ParentId = parentId,
            IsActive = true
        };
        await DbContext.SystemMessages.AddAsync(childVersion);
        await DbContext.SaveChangesAsync();
        var childId = childVersion.Id;

        var updatedMessage = new SystemMessage
        {
            Name = "Updated Child",
            Content = "Updated Content",
            Type = childVersion.Type,
            ModifiedBy = "testuser"
        };

        var result = await Service.UpdateAsync(childId, updatedMessage);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Version, Is.EqualTo(3));
            Assert.That(result.ParentId, Is.EqualTo(parentId));
            Assert.That(result.IsActive, Is.True);
        });

        // Detach all tracked entities to force a fresh query
        foreach (var entry in DbContext.ChangeTracker.Entries<SystemMessage>())
        {
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }

        var allVersions = await DbContext.SystemMessages
            .Where(m => m.Id == parentId || m.ParentId == parentId)
            .ToListAsync();

        foreach (var version in allVersions.Where(v => v.Id != result.Id))
        {
            Assert.That(version.IsActive, Is.False);
        }
    }

    [Test]
    public async Task UpdateAsync_NonExistingMessage_ReturnsNull()
    {
        var message = new SystemMessage { Name = "Test", Content = "Content" };

        var result = await Service.UpdateAsync(999, message);

        Assert.That(result, Is.Null);
    }

    #endregion UpdateAsync Tests

    #region CreateNewVersionAsync Tests

    [Test]
    public async Task CreateNewVersionAsync_CreatesNewVersionWithIncrementedNumber()
    {
        var original = await CreateTestMessage(name: "Original", content: "Content");

        var result = await Service.CreateNewVersionAsync(original.Id, "testuser");

        Assert.Multiple(() =>
        {
            Assert.That(result!.Version, Is.EqualTo(2));
            Assert.That(result.ParentId, Is.EqualTo(original.Id));
            Assert.That(result.IsActive, Is.False);
            Assert.That(result.CreatedBy, Is.EqualTo("testuser"));
            Assert.That(result.Name, Is.EqualTo(original.Name));
            Assert.That(result.Content, Is.EqualTo(original.Content));
        });
    }

    [Test]
    public async Task CreateNewVersionAsync_FromChildVersion_UsesRootParentId()
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

        var result = await Service.CreateNewVersionAsync(child.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result!.ParentId, Is.EqualTo(parent.Id));
            Assert.That(result.Version, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task CreateNewVersionAsync_NonExistingId_ReturnsNull()
    {
        var result = await Service.CreateNewVersionAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task CreateNewVersionAsync_InvalidId_ReturnsNull()
    {
        var result = await Service.CreateNewVersionAsync(0);

        Assert.That(result, Is.Null);
    }

    #endregion CreateNewVersionAsync Tests

    #region GetVersionHistoryAsync Tests

    [Test]
    public async Task GetVersionHistoryAsync_ReturnsAllVersionsOrderedByVersion()
    {
        var parent = await CreateTestMessage(name: "Parent");

        var v2 = new SystemMessage { Name = "V2", Content = "Content", Version = 2, ParentId = parent.Id };
        var v3 = new SystemMessage { Name = "V3", Content = "Content", Version = 3, ParentId = parent.Id };
        await DbContext.SystemMessages.AddRangeAsync(v2, v3);
        await DbContext.SaveChangesAsync();

        var result = await Service.GetVersionHistoryAsync(parent.Id);

        Assert.That(result, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].Version, Is.EqualTo(1));
            Assert.That(result[1].Version, Is.EqualTo(2));
            Assert.That(result[2].Version, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task GetVersionHistoryAsync_NonExistingId_ReturnsEmptyList()
    {
        var result = await Service.GetVersionHistoryAsync(999);

        Assert.That(result, Is.Empty);
    }

    #endregion GetVersionHistoryAsync Tests

    #region SetActiveVersionAsync Tests

    [Test]
    public async Task SetActiveVersionAsync_DeactivatesOthersAndActivatesSpecified()
    {
        var v1 = new SystemMessage { Name = "V1", Content = "Content", Version = 1, IsActive = true };
        await DbContext.SystemMessages.AddAsync(v1);
        await DbContext.SaveChangesAsync();

        var v2 = new SystemMessage { Name = "V2", Content = "Content", Version = 2, ParentId = v1.Id, IsActive = false };
        await DbContext.SystemMessages.AddAsync(v2);
        await DbContext.SaveChangesAsync();

        var result = await Service.SetActiveVersionAsync(v2.Id);

        Assert.That(result, Is.True);

        var dbV1 = await ReloadEntityAsync(v1.Id);
        var dbV2 = await ReloadEntityAsync(v2.Id);

        Assert.Multiple(() =>
        {
            Assert.That(dbV1!.IsActive, Is.False);
            Assert.That(dbV2!.IsActive, Is.True);
        });
    }

    [Test]
    public async Task SetActiveVersionAsync_NonExistingId_ReturnsFalse()
    {
        var result = await Service.SetActiveVersionAsync(999);

        Assert.That(result, Is.False);
    }

    #endregion SetActiveVersionAsync Tests
}