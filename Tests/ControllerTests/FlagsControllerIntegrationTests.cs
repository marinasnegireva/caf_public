using System.Net;
using System.Net.Http.Json;
using Tests.Infrastructure;

namespace Tests.ControllerTests;

[TestFixture]
public class FlagsControllerIntegrationTests : IntegrationTestBase
{
    #region GET Tests

    [Test]
    public async Task GetAll_ReturnsEmptyList_WhenNoFlags()
    {
        // Act
        var response = await _client.GetAsync("/api/flags");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var flags = await response.Content.ReadFromJsonAsync<List<Flag>>(_jsonOptions);
        Assert.That(flags, Is.Not.Null);
        Assert.That(flags, Is.Empty);
    }

    [Test]
    public async Task GetAll_ReturnsFlags_WhenFlagsExist()
    {
        // Arrange
        await CreateTestFlag("test-flag-1");
        await CreateTestFlag("test-flag-2");

        // Act
        var response = await _client.GetAsync("/api/flags");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var flags = await response.Content.ReadFromJsonAsync<List<Flag>>(_jsonOptions);
        Assert.That(flags, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetAll_FiltersActiveFlags()
    {
        // Arrange - Create two flags (both active by default)
        var activeFlag = await CreateTestFlag("active-flag");
        var inactiveFlag = await CreateTestFlag("inactive-flag");
        // Toggle the inactive flag OFF
        await ToggleActive(inactiveFlag.Id);

        // Act - Filter for active flags only
        var response = await _client.GetAsync("/api/flags?active=true");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var flags = await response.Content.ReadFromJsonAsync<List<Flag>>(_jsonOptions);
        Assert.That(flags, Has.Count.EqualTo(1));
        Assert.That(flags![0].Value, Is.EqualTo("active-flag"));
    }

    [Test]
    public async Task GetById_ReturnsFlag_WhenExists()
    {
        // Arrange
        var created = await CreateTestFlag("test-flag");

        // Act
        var response = await _client.GetAsync($"/api/flags/{created.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var flag = await response.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(flag, Is.Not.Null);
        Assert.That(flag!.Value, Is.EqualTo("test-flag"));
    }

    [Test]
    public async Task GetById_Returns404_WhenNotExists()
    {
        // Act
        var response = await _client.GetAsync("/api/flags/999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion GET Tests

    #region POST Tests

    [Test]
    public async Task Create_CreatesFlag_WithValidData()
    {
        // Arrange
        var request = new { value = "new-flag" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await response.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Id, Is.GreaterThan(0));
            Assert.That(created.Value, Is.EqualTo("new-flag"));
            Assert.That(created.Active, Is.True);
            Assert.That(created.Constant, Is.False);
        });
    }

    [Test]
    public async Task Create_ReturnsBadRequest_WhenValueIsEmpty()
    {
        // Arrange
        var request = new { value = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/flags", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion POST Tests

    #region PUT Tests

    [Test]
    public async Task Update_UpdatesFlag_WithValidData()
    {
        // Arrange
        var created = await CreateTestFlag("original-flag");
        var request = new { value = "updated-flag" };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/flags/{created.Id}", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await response.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(updated!.Value, Is.EqualTo("updated-flag"));
    }

    [Test]
    public async Task Update_Returns404_WhenNotExists()
    {
        // Arrange
        var request = new { value = "test" };

        // Act
        var response = await _client.PutAsJsonAsync("/api/flags/999", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion PUT Tests

    #region Toggle Tests

    [Test]
    public async Task ToggleActive_TogglesActiveState()
    {
        // Arrange - flags are active by default
        var flag = await CreateTestFlag("test-flag");
        Assert.That(flag.Active, Is.True);

        // Act - Toggle to false
        var response1 = await _client.PostAsync($"/api/flags/{flag.Id}/toggle-active", null);

        // Assert
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var toggled1 = await response1.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(toggled1!.Active, Is.False);

        // Act - Toggle back to true
        var response2 = await _client.PostAsync($"/api/flags/{flag.Id}/toggle-active", null);

        // Assert
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var toggled2 = await response2.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(toggled2!.Active, Is.True);
    }

    [Test]
    public async Task ToggleConstant_TogglesConstantState()
    {
        // Arrange
        var flag = await CreateTestFlag("test-flag");
        Assert.That(flag.Constant, Is.False);

        // Act - Toggle to true
        var response1 = await _client.PostAsync($"/api/flags/{flag.Id}/toggle-constant", null);

        // Assert
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var toggled1 = await response1.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(toggled1!.Constant, Is.True);

        // Act - Toggle back to false
        var response2 = await _client.PostAsync($"/api/flags/{flag.Id}/toggle-constant", null);

        // Assert
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var toggled2 = await response2.Content.ReadFromJsonAsync<Flag>(_jsonOptions);
        Assert.That(toggled2!.Constant, Is.False);
    }

    [Test]
    public async Task ToggleActive_Returns404_WhenNotExists()
    {
        // Act
        var response = await _client.PostAsync("/api/flags/999/toggle-active", null);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ToggleConstant_Returns404_WhenNotExists()
    {
        // Act
        var response = await _client.PostAsync("/api/flags/999/toggle-constant", null);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion Toggle Tests

    #region DELETE Tests

    [Test]
    public async Task Delete_DeletesFlag_WhenExists()
    {
        // Arrange
        var created = await CreateTestFlag("test-flag");

        // Act
        var response = await _client.DeleteAsync($"/api/flags/{created.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var getResponse = await _client.GetAsync($"/api/flags/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Returns404_WhenNotExists()
    {
        // Act
        var response = await _client.DeleteAsync("/api/flags/999");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion DELETE Tests

    #region Helper Methods

    private async Task<Flag> CreateTestFlag(string value)
    {
        var request = new { value };
        var response = await _client.PostAsJsonAsync("/api/flags", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Flag>(_jsonOptions))!;
    }

    private async Task<Flag> ToggleActive(int flagId)
    {
        var response = await _client.PostAsync($"/api/flags/{flagId}/toggle-active", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Flag>(_jsonOptions))!;
    }

    #endregion Helper Methods
}