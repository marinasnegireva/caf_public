using System.Net;
using System.Net.Http.Json;
using Tests.Infrastructure;

namespace Tests.ControllerTests;

[TestFixture]
public class SettingsControllerIntegrationTests : IntegrationTestBase
{
    #region Helper Methods

    private async Task<Setting> CreateOrUpdateSetting(SettingsKeys key, string value)
    {
        var request = new { name = key.ToString(), value };
        var response = await Client.PostAsJsonAsync("/api/settings", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Setting>(JsonOptions))!;
    }

    private static void AssertSuccessResponse(HttpResponseMessage response, string context = "")
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), 
            $"Request should succeed{(string.IsNullOrEmpty(context) ? "" : $": {context}")}");
    }

    private static void AssertSetting(Setting? setting, string expectedName, string expectedValue)
    {
        Assert.That(setting, Is.Not.Null, "Setting should not be null");
        Assert.Multiple(() =>
        {
            Assert.That(setting!.Name, Is.EqualTo(expectedName), "Setting name should match");
            Assert.That(setting.Value, Is.EqualTo(expectedValue), "Setting value should match");
        });
    }

    #endregion

    #region GET Tests

    [Test]
    public async Task GetAll_ReturnsNonEmptyList_WhenOnlyDefaultSettingsExist()
    {
        // Act
        var response = await Client.GetAsync("/api/settings");

        // Assert
        AssertSuccessResponse(response);

        var settings = await response.Content.ReadFromJsonAsync<List<Setting>>(JsonOptions);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings, Is.Not.Empty, "Should return default settings");
    }

    [Test]
    public async Task GetAll_ReturnsSettings_WhenSettingsExist()
    {
        // Arrange
        await CreateOrUpdateSetting(SettingsKeys.QuotesMaxLength, "3000");
        await CreateOrUpdateSetting(SettingsKeys.QuoteCanonMaxLength, "600");

        // Act
        var response = await Client.GetAsync("/api/settings");

        // Assert
        AssertSuccessResponse(response);

        var settings = await response.Content.ReadFromJsonAsync<List<Setting>>(JsonOptions);
        Assert.That(settings, Is.Not.Null);
        Assert.That(settings!, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(settings.Select(s => s.Name), Does.Contain("QuotesMaxLength"));
        Assert.That(settings.Select(s => s.Name), Does.Contain("QuoteCanonMaxLength"));
    }

    [Test]
    public async Task GetById_ReturnsSetting_WhenExists()
    {
        // Arrange
        var created = await CreateOrUpdateSetting(SettingsKeys.PreviousTurnsCount, "10");

        // Act
        var response = await Client.GetAsync($"/api/settings/{created.Id}");

        // Assert
        AssertSuccessResponse(response);

        var setting = await response.Content.ReadFromJsonAsync<Setting>(JsonOptions);
        AssertSetting(setting, "PreviousTurnsCount", "10");
    }

    [Test]
    public async Task GetById_Returns404_WhenNotExists()
    {
        // Act
        var response = await Client.GetAsync("/api/settings/999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetByName_ReturnsSetting_WhenExists()
    {
        // Arrange
        await CreateOrUpdateSetting(SettingsKeys.MaxDialogueLogTurns, "100");

        // Act
        var response = await Client.GetAsync("/api/settings/by-name/MaxDialogueLogTurns");

        // Assert
        AssertSuccessResponse(response);

        var setting = await response.Content.ReadFromJsonAsync<Setting>(JsonOptions);
        AssertSetting(setting, "MaxDialogueLogTurns", "100");
    }

    [Test]
    public async Task GetByName_ReturnsBadRequest_WhenNameIsInvalid()
    {
        // Act
        var response = await Client.GetAsync("/api/settings/by-name/nonexistent");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetValue_ReturnsValue_WhenExists()
    {
        // Arrange
        await CreateOrUpdateSetting(SettingsKeys.PerceptionEnabled, "true");

        // Act
        var response = await Client.GetAsync("/api/settings/value/PerceptionEnabled");

        // Assert
        AssertSuccessResponse(response);

        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!["value"], Is.EqualTo("true"));
    }

    [Test]
    public async Task GetValue_ReturnsBadRequest_WhenNameIsInvalid()
    {
        // Act
        var response = await Client.GetAsync("/api/settings/value/nonexistent");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region POST Tests

    [Test]
    public async Task CreateOrUpdate_CreatesSetting_WhenNotExists()
    {
        // Arrange
        var request = new { name = SettingsKeys.TriggerScanTextAdditionalWords.ToString(), value = "test,words" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/settings", request);

        // Assert
        AssertSuccessResponse(response);

        var created = await response.Content.ReadFromJsonAsync<Setting>(JsonOptions);
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Id, Is.GreaterThan(0));
        AssertSetting(created, "TriggerScanTextAdditionalWords", "test,words");
    }

    [Test]
    public async Task CreateOrUpdate_UpdatesSetting_WhenExists()
    {
        // Arrange
        var existing = await CreateOrUpdateSetting(SettingsKeys.PerceptionEnabled, "false");
        var request = new { name = SettingsKeys.PerceptionEnabled.ToString(), value = "true" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/settings", request);

        // Assert
        AssertSuccessResponse(response, "update existing setting");

        var updated = await response.Content.ReadFromJsonAsync<Setting>(JsonOptions);
        Assert.That(updated, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Id, Is.EqualTo(existing.Id), "Should update same setting");
            Assert.That(updated.Value, Is.EqualTo("true"), "Should have new value");
        });
    }

    [Test]
    public async Task CreateOrUpdate_ReturnsBadRequest_WhenNameIsEmpty()
    {
        // Arrange
        var request = new { name = "", value = "value" };

        // Act
        var response = await Client.PostAsJsonAsync("/api/settings", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region DELETE Tests

    [Test]
    public async Task Delete_DeletesSetting_WhenExists()
    {
        // Arrange
        var created = await CreateOrUpdateSetting(SettingsKeys.SemanticTokenQuota_Quote, "5000");

        // Act
        var response = await Client.DeleteAsync($"/api/settings/{created.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Verify deletion
        var getResponse = await Client.GetAsync($"/api/settings/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "Deleted setting should not be found");
    }

    [Test]
    public async Task Delete_Returns404_WhenNotExists()
    {
        // Act
        var response = await Client.DeleteAsync("/api/settings/999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion
}