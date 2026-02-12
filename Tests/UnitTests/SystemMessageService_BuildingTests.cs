namespace Tests.UnitTests;

[TestFixture]
public class SystemMessageService_BuildingTests : SystemMessageServiceTestBase
{
    #region BuildCompleteSystemMessageAsync Tests

    [Test]
    public async Task BuildCompleteSystemMessageAsync_WithPersonaOnly_ReturnsPersonaContent()
    {
        var persona = new SystemMessage
        {
            Name = "Main Persona",
            Content = "You are a helpful assistant",
            Type = SystemMessage.SystemMessageType.Persona,
            IsActive = true,
            ProfileId = 1
        };
        await DbContext.SystemMessages.AddAsync(persona);
        await DbContext.SaveChangesAsync();

        var result = await Service.BuildCompleteSystemMessageAsync();

        Assert.That(result, Does.Contain("# PERSONA"));
        Assert.That(result, Does.Contain("You are a helpful assistant"));
    }

    [Test]
    public async Task BuildCompleteSystemMessageAsync_WithAllTypes_CombinesAll()
    {
        var persona = new SystemMessage { Name = "Persona", Content = "Persona content", Type = SystemMessage.SystemMessageType.Persona, IsActive = true, ProfileId = 1 };
        var perception = new SystemMessage { Name = "Perception", Content = "Perception content", Type = SystemMessage.SystemMessageType.Perception, IsActive = true, ProfileId = 1 };
        var technical = new SystemMessage { Name = "Technical", Content = "Technical content", Type = SystemMessage.SystemMessageType.Technical, IsActive = true, ProfileId = 1 };

        await DbContext.SystemMessages.AddRangeAsync(persona, perception, technical);
        await DbContext.SaveChangesAsync();

        var result = await Service.BuildCompleteSystemMessageAsync();

        Assert.That(result, Does.Contain("# PERSONA"));
        Assert.That(result, Does.Contain("Persona content"));
        Assert.That(result, Does.Contain("# PERCEPTION"));
        Assert.That(result, Does.Contain("Perception content"));
        Assert.That(result, Does.Contain("# TECHNICAL"));
        Assert.That(result, Does.Contain("Technical content"));
    }

    [Test]
    public async Task BuildCompleteSystemMessageAsync_OrderIsCorrect()
    {
        // Arrange: Create all message types
        var persona = new SystemMessage
        {
            Name = "Persona",
            Content = "PERSONA_CONTENT",
            Type = SystemMessage.SystemMessageType.Persona,
            IsActive = true,
            ProfileId = 1
        };

        var perception = new SystemMessage
        {
            Name = "Perception",
            Content = "PERCEPTION_CONTENT",
            Type = SystemMessage.SystemMessageType.Perception,
            IsActive = true,
            ProfileId = 1
        };

        var technical = new SystemMessage
        {
            Name = "Technical",
            Content = "TECHNICAL_CONTENT",
            Type = SystemMessage.SystemMessageType.Technical,
            IsActive = true,
            ProfileId = 1
        };

        await DbContext.SystemMessages.AddRangeAsync(persona, perception, technical);
        await DbContext.SaveChangesAsync();

        var result = await Service.BuildCompleteSystemMessageAsync();

        // Verify order: Persona -> Perception -> Technical
        var personaIndex = result.IndexOf("PERSONA_CONTENT");
        var perceptionIndex = result.IndexOf("PERCEPTION_CONTENT");
        var technicalIndex = result.IndexOf("TECHNICAL_CONTENT");

        Assert.Multiple(() =>
        {
            Assert.That(personaIndex, Is.LessThan(perceptionIndex), "Persona should come before Perception");
            Assert.That(perceptionIndex, Is.LessThan(technicalIndex), "Perception should come before Technical");
        });
    }

    [Test]
    public async Task BuildCompleteSystemMessageAsync_WithNoActiveMessages_ReturnsEmpty()
    {
        await CreateTestMessage(type: SystemMessage.SystemMessageType.Persona, isActive: false);
        await CreateTestMessage(type: SystemMessage.SystemMessageType.Technical, isActive: false);

        var result = await Service.BuildCompleteSystemMessageAsync();

        Assert.That(result, Is.Empty);
    }  

    #endregion BuildCompleteSystemMessageAsync Tests
}