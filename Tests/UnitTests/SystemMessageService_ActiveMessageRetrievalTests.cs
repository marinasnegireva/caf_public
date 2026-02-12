namespace Tests.UnitTests;

[TestFixture]
public class SystemMessageService_ActiveMessageRetrievalTests : SystemMessageServiceTestBase
{
    #region GetActivePersonaAsync Tests

    [Test]
    public async Task GetActivePersonaAsync_ReturnsActiveNonArchivedPersona()
    {
        await DbContext.SystemMessages.AddRangeAsync(
            new SystemMessage { Name = "Active", Content = "Content", Type = SystemMessage.SystemMessageType.Persona, IsActive = true, IsArchived = false, ProfileId = 1 },
            new SystemMessage { Name = "Inactive", Content = "Content", Type = SystemMessage.SystemMessageType.Persona, IsActive = false, ProfileId = 1 },
            new SystemMessage { Name = "Archived", Content = "Content", Type = SystemMessage.SystemMessageType.Persona, IsActive = true, IsArchived = true, ProfileId = 1 }
        );
        await DbContext.SaveChangesAsync();

        var result = await Service.GetActivePersonaAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("Active"));
    }

    [Test]
    public async Task GetActivePersonaAsync_NoActivePersona_ReturnsNull()
    {
        await CreateTestMessage(type: SystemMessage.SystemMessageType.Persona, isActive: false);

        var result = await Service.GetActivePersonaAsync();

        Assert.That(result, Is.Null);
    }

    #endregion GetActivePersonaAsync Tests

    #region GetActivePerceptionsAsync Tests

    [Test]
    public async Task GetActivePerceptionsAsync_ReturnsAllActivePerceptions()
    {
        await DbContext.SystemMessages.AddRangeAsync(
            new SystemMessage { Name = "Perception1", Content = "Content", Type = SystemMessage.SystemMessageType.Perception, IsActive = true, ProfileId = 1 },
            new SystemMessage { Name = "Perception2", Content = "Content", Type = SystemMessage.SystemMessageType.Perception, IsActive = true, ProfileId = 1 },
            new SystemMessage { Name = "Inactive", Content = "Content", Type = SystemMessage.SystemMessageType.Perception, IsActive = false, ProfileId = 1 }
        );
        await DbContext.SaveChangesAsync();

        var result = await Service.GetActivePerceptionsAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.All(p => p.IsActive), Is.True);
            Assert.That(result.All(p => p.Type == SystemMessage.SystemMessageType.Perception), Is.True);
        });
    }

    [Test]
    public async Task GetActivePerceptionsAsync_ExcludesArchived()
    {
        await DbContext.SystemMessages.AddRangeAsync(
            new SystemMessage { Name = "Active", Content = "Content", Type = SystemMessage.SystemMessageType.Perception, IsArchived = false, ProfileId = 1 },
            new SystemMessage { Name = "Archived", Content = "Content", Type = SystemMessage.SystemMessageType.Perception, IsArchived = true, ProfileId = 1 }
        );
        await DbContext.SaveChangesAsync();

        var result = await Service.GetActivePerceptionsAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Active"));
    }

    #endregion GetActivePerceptionsAsync Tests

    #region GetActiveTechnicalMessagesAsync Tests

    [Test]
    public async Task GetActiveTechnicalMessagesAsync_ReturnsAllActiveTechnical()
    {
        await DbContext.SystemMessages.AddRangeAsync(
            new SystemMessage { Name = "Tech1", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsActive = true, ProfileId = 1 },
            new SystemMessage { Name = "Tech2", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsActive = true, ProfileId = 1 }
        );
        await DbContext.SaveChangesAsync();

        var result = await Service.GetActiveTechnicalMessagesAsync();

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(result.All(t => t.IsActive), Is.True);
            Assert.That(result.All(t => t.Type == SystemMessage.SystemMessageType.Technical), Is.True);
        });
    }

    [Test]
    public async Task GetActiveTechnicalMessagesAsync_ExcludesInactiveAndArchived()
    {
        await DbContext.SystemMessages.AddRangeAsync(
            new SystemMessage { Name = "Active", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsArchived = false, ProfileId = 1 },
            new SystemMessage { Name = "Inactive", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsActive = false, ProfileId = 1 },
            new SystemMessage { Name = "Archived", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsArchived = true, ProfileId = 1 }
        );
        await DbContext.SaveChangesAsync();

        var result = await Service.GetActiveTechnicalMessagesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Name, Is.EqualTo("Active"));
    }

    #endregion GetActiveTechnicalMessagesAsync Tests
}
