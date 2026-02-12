using System.Net;
using System.Net.Http.Json;
using Tests.Infrastructure;

namespace Tests.ControllerTests;

[TestFixture]
public class SystemMessagesControllerIntegrationTests : IntegrationTestBase
{
    #region GET Tests

    [Test]
    public async Task GetAll_ReturnsDefaultMessages_WhenNoCustomMessagesCreated()
    {
        // The app initializes default system messages on startup
        // Act
        var response = await Client.GetAsync("/api/systemmessages");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var messages = await response.Content.ReadFromJsonAsync<List<SystemMessage>>(JsonOptions);
        Assert.That(messages, Is.Not.Null);
        // Default system messages are created on app startup
        Assert.That(messages, Is.Not.Empty);
    }

    [Test]
    public async Task GetAll_ReturnsMessages_WhenMessagesExist()
    {
        // Arrange
        await CreateTestMessage("Test Persona", SystemMessage.SystemMessageType.Persona);

        // Act
        var response = await Client.GetAsync("/api/systemmessages");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var messages = await response.Content.ReadFromJsonAsync<List<SystemMessage>>(JsonOptions);
        Assert.That(messages, Is.Not.Null);
        Assert.That(messages!.Select(m => m.Name), Does.Contain("Test Persona"));
    }

    [Test]
    public async Task GetAll_FiltersByType()
    {
        // Arrange
        await CreateTestMessage("TestPersona", SystemMessage.SystemMessageType.Persona);

        // Act
        var response = await Client.GetAsync("/api/systemmessages?type=Persona");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var messages = await response.Content.ReadFromJsonAsync<List<SystemMessage>>(JsonOptions);
        Assert.That(messages, Is.Not.Null);
        Assert.That(messages!.All(m => m.Type == SystemMessage.SystemMessageType.Persona), Is.True);
    }

    [Test]
    public async Task GetById_ReturnsMessage_WhenExists()
    {
        // Arrange
        var created = await CreateTestMessage("Test", SystemMessage.SystemMessageType.Persona);

        // Act
        var response = await Client.GetAsync($"/api/systemmessages/{created.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var message = await response.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);
        Assert.That(message, Is.Not.Null);
        Assert.That(message!.Name, Is.EqualTo("Test"));
    }

    [Test]
    public async Task GetById_Returns404_WhenNotExists()
    {
        // Act
        var response = await Client.GetAsync("/api/systemmessages/99999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion GET Tests

    #region POST Tests

    [Test]
    public async Task Create_CreatesMessage_WithValidData()
    {
        // Arrange
        var message = new SystemMessage
        {
            Name = "New Persona",
            Content = "You are a helpful assistant",
            Type = SystemMessage.SystemMessageType.Persona,
            IsActive = true,
            Tags = ["assistant", "helpful"]
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/systemmessages", message, JsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await response.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Id, Is.GreaterThan(0));
            Assert.That(created.Name, Is.EqualTo("New Persona"));
            Assert.That(created.Version, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CreateVersion_CreatesNewVersion()
    {
        // Arrange
        var original = await CreateTestMessage("Original", SystemMessage.SystemMessageType.Persona);

        // Act
        var response = await Client.PostAsJsonAsync($"/api/systemmessages/{original.Id}/version",
            new { modifiedBy = "testuser" }, JsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var newVersion = await response.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);
        Assert.That(newVersion, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(newVersion!.Version, Is.EqualTo(2));
            Assert.That(newVersion.ParentId, Is.EqualTo(original.Id));
            Assert.That(newVersion.IsActive, Is.False);
        });
    }

    #endregion POST Tests

    #region PUT Tests

    [Test]
    public async Task Update_UpdatesMessage_WithValidData()
    {
        // Arrange
        var created = await CreateTestMessage("Original", SystemMessage.SystemMessageType.Persona);
        created.Name = "Updated";
        created.Content = "Updated content";

        // Act
        var response = await Client.PutAsJsonAsync($"/api/systemmessages/{created.Id}", created, JsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await response.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Name, Is.EqualTo("Updated"));
            Assert.That(updated.Content, Is.EqualTo("Updated content"));
        });
    }

    [Test]
    public async Task Update_Returns404_WhenNotExists()
    {
        // Arrange
        var message = new SystemMessage { Name = "Test", Content = "Content" };

        // Act
        var response = await Client.PutAsJsonAsync("/api/systemmessages/99999", message, JsonOptions);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion PUT Tests

    #region DELETE Tests

    [Test]
    public async Task Delete_DeletesMessage_WhenExists()
    {
        // Arrange
        var created = await CreateTestMessage("Test", SystemMessage.SystemMessageType.Persona);

        // Act
        var response = await Client.DeleteAsync($"/api/systemmessages/{created.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await Client.GetAsync($"/api/systemmessages/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Returns404_WhenNotExists()
    {
        // Act
        var response = await Client.DeleteAsync("/api/systemmessages/99999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion DELETE Tests

    #region Version Management Tests

    [Test]
    public async Task GetVersionHistory_ReturnsAllVersions()
    {
        // Arrange
        var original = await CreateTestMessage("Original", SystemMessage.SystemMessageType.Persona);
        await Client.PostAsJsonAsync($"/api/systemmessages/{original.Id}/version", new { }, JsonOptions);
        await Client.PostAsJsonAsync($"/api/systemmessages/{original.Id}/version", new { }, JsonOptions);

        // Act
        var response = await Client.GetAsync($"/api/systemmessages/{original.Id}/versions");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var versions = await response.Content.ReadFromJsonAsync<List<SystemMessage>>(JsonOptions);
        Assert.That(versions, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(versions![0].Version, Is.EqualTo(1));
            Assert.That(versions[1].Version, Is.EqualTo(2));
            Assert.That(versions[2].Version, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task SetActiveVersion_ActivatesSpecifiedVersion()
    {
        // Arrange
        var v1 = await CreateTestMessage("V1", SystemMessage.SystemMessageType.Persona);
        var createResponse = await Client.PostAsJsonAsync($"/api/systemmessages/{v1.Id}/version", new { }, JsonOptions);
        var v2 = await createResponse.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);

        // Act
        var response = await Client.PostAsync($"/api/systemmessages/{v2!.Id}/activate", null);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var v1Check = await Client.GetAsync($"/api/systemmessages/{v1.Id}");
        var v2Check = await Client.GetAsync($"/api/systemmessages/{v2.Id}");

        var v1Result = await v1Check.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);
        var v2Result = await v2Check.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(v1Result!.IsActive, Is.False);
            Assert.That(v2Result!.IsActive, Is.True);
        });
    }

    #endregion Version Management Tests

    #region Active Messages Tests

    [Test]
    public async Task GetActivePersona_ReturnsActivePersona()
    {
        // Arrange
        await CreateTestMessage("Active Persona", SystemMessage.SystemMessageType.Persona, isActive: true);

        // Act
        var response = await Client.GetAsync("/api/systemmessages/active/persona");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var persona = await response.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions);
        Assert.That(persona, Is.Not.Null);
        Assert.That(persona!.Name, Is.EqualTo("Active Persona"));
    }

    [Test]
    public async Task GetActivePerceptions_ReturnsAllActive()
    {
        // Arrange
        await CreateTestMessage("Perception1", SystemMessage.SystemMessageType.Perception, isActive: true);
        await CreateTestMessage("Perception2", SystemMessage.SystemMessageType.Perception, isActive: true);
        await CreateTestMessage("Inactive", SystemMessage.SystemMessageType.Perception, isActive: false);

        // Act
        var response = await Client.GetAsync("/api/systemmessages/active/perceptions");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var perceptions = await response.Content.ReadFromJsonAsync<List<SystemMessage>>(JsonOptions);
        // There may be default perception + our 2 active ones
        Assert.That(perceptions, Has.Count.GreaterThanOrEqualTo(2));
    }

    #endregion Active Messages Tests

    #region Build System Message Tests

    [Test]
    public async Task BuildCompleteSystemMessage_ReturnsMessage()
    {
        // Arrange
        await CreateTestMessage("Main Persona", SystemMessage.SystemMessageType.Persona, isActive: true,
            content: "You are a helpful assistant");

        // Act
        var response = await Client.GetAsync("/api/systemmessages/build");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.ContainsKey("systemMessage"), Is.True);
    }

    [Test]
    public async Task GetPreview_ReturnsPreviewResponse()
    {
        // Arrange
        await CreateTestMessage("Main Persona", SystemMessage.SystemMessageType.Persona, isActive: true,
            content: "You are a helpful assistant");

        // Act
        var response = await Client.GetAsync("/api/systemmessages/preview");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var preview = await response.Content.ReadFromJsonAsync<PreviewResponse>(JsonOptions);
        Assert.That(preview, Is.Not.Null);
        Assert.That(preview!.HasPersona, Is.True);
    }

    #endregion Build System Message Tests

    #region Helper Methods

    private async Task<SystemMessage> CreateTestMessage(
        string name,
        SystemMessage.SystemMessageType type,
        bool isActive = false,
        string content = "Test content")
    {
        var message = new SystemMessage
        {
            Name = name,
            Content = content,
            Type = type,
            IsActive = isActive
        };

        var response = await Client.PostAsJsonAsync("/api/systemmessages", message, JsonOptions);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<SystemMessage>(JsonOptions))!;
    }

    #endregion Helper Methods

    private class PreviewResponse
    {
        public string CompleteMessage { get; set; } = string.Empty;
        public bool HasPersona { get; set; }
        public int PerceptionCount { get; set; }
        public bool HasTechnical { get; set; }
        public int ContextCount { get; set; }
    }
}