using System.Net;
using System.Net.Http.Json;
using Tests.Infrastructure;

namespace Tests.ControllerTests;

[TestFixture]
public class DashboardControllerIntegrationTests : IntegrationTestBase
{
    #region Helper Methods

    private async Task<Flag> CreateFlag(string value)
    {
        var request = new { value };
        var response = await Client.PostAsJsonAsync("/api/flags", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Flag>(JsonOptions))!;
    }

    private async Task ToggleFlagActive(int flagId)
    {
        var response = await Client.PostAsync($"/api/flags/{flagId}/toggle-active", null);
        response.EnsureSuccessStatusCode();
    }

    private async Task ToggleFlagConstant(int flagId)
    {
        var response = await Client.PostAsync($"/api/flags/{flagId}/toggle-constant", null);
        response.EnsureSuccessStatusCode();
    }

    private async Task<Session> CreateSession(string name)
    {
        var request = new { name };
        var response = await Client.PostAsJsonAsync("/api/sessions", request);
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<Session>(JsonOptions))!;

        // Activate the session
        var activateResponse = await Client.PostAsync($"/api/sessions/{session.Id}/activate", null);
        activateResponse.EnsureSuccessStatusCode();

        // Fetch the updated session
        var getResponse = await Client.GetAsync($"/api/sessions/{session.Id}");
        return (await getResponse.Content.ReadFromJsonAsync<Session>(JsonOptions))!;
    }

    private async Task<Setting> CreateOrUpdateSetting(SettingsKeys key, string value)
    {
        var request = new { name = key.ToString(), value };
        var response = await Client.PostAsJsonAsync("/api/settings", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Setting>(JsonOptions))!;
    }

    private static void AssertDashboardSuccess(HttpResponseMessage response, DashboardResponse? dashboard)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(dashboard, Is.Not.Null);
        });
    }

    #endregion

    #region Empty Dashboard Tests
    #endregion

    #region Empty Dashboard Tests

    [Test]
    public async Task GetDashboard_ReturnsEmptyDashboard_WhenNoData()
    {
        // Act
        var response = await Client.GetAsync("/api/dashboard");

        // Assert
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
        AssertDashboardSuccess(response, dashboard);
        Assert.Multiple(() =>
        {
            Assert.That(dashboard!.ActiveFlags, Is.Empty, "Should have no active flags");
            Assert.That(dashboard.Settings, Is.Not.Empty, "Should have default settings");
            Assert.That(dashboard.ActiveSession, Is.Null, "Should have no active session");
            Assert.That(dashboard.Statistics.TotalFlags, Is.EqualTo(0), "Should have zero total flags");
            Assert.That(dashboard.Statistics.TotalSettings, Is.EqualTo(dashboard.Settings.Count));
        });
    }

    #endregion

    #region Flag Tests

    #endregion

    #region Flag Tests

    [Test]
    public async Task GetDashboard_ReturnsActiveFlags()
    {
        // Arrange - flags are created with Active=true by default
        var activeFlag = await CreateFlag("active-flag");
        var constantFlag = await CreateFlag("constant-flag");
        await ToggleFlagConstant(constantFlag.Id);  // Mark as constant (still active)

        var inactiveFlag = await CreateFlag("inactive-flag");
        await ToggleFlagActive(inactiveFlag.Id);  // Toggle OFF to make it inactive

        // Act
        var response = await Client.GetAsync("/api/dashboard");

        // Assert
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
        AssertDashboardSuccess(response, dashboard);

        Assert.Multiple(() =>
        {
            // Dashboard returns active OR constant flags, so 2 flags (active-flag and constant-flag)
            Assert.That(dashboard!.ActiveFlags, Has.Count.EqualTo(2), "Should return 2 flags (active + constant)");
            Assert.That(dashboard.Statistics.TotalFlags, Is.EqualTo(2), "Should count 2 total flags");
            Assert.That(dashboard.Statistics.ActiveFlags, Is.EqualTo(1), "Should count 1 active flag");
            Assert.That(dashboard.Statistics.ConstantFlags, Is.EqualTo(1), "Should count 1 constant flag");
        });
    }

    #endregion

    #region Session Tests

    #endregion

    #region Session Tests

    [Test]
    public async Task GetDashboard_ReturnsActiveSession()
    {
        // Arrange
        var session = await CreateSession("Test Session");

        // Act
        var response = await Client.GetAsync("/api/dashboard");

        // Assert
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
        AssertDashboardSuccess(response, dashboard);
        Assert.Multiple(() =>
        {
            Assert.That(dashboard!.ActiveSession, Is.Not.Null, "Should have an active session");
            Assert.That(dashboard.ActiveSession!.Name, Is.EqualTo("Test Session"));
        });
    }

    #endregion

    #region Settings Tests

    #endregion

    #region Settings Tests

    [Test]
    public async Task GetDashboard_ReturnsSettings()
    {
        // Arrange - Create additional settings beyond defaults
        await CreateOrUpdateSetting(SettingsKeys.GeminiModel, "gemini-pro");
        await CreateOrUpdateSetting(SettingsKeys.ClaudeModel, "claude-3-opus");

        // Act
        var response = await Client.GetAsync("/api/dashboard");

        // Assert
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
        AssertDashboardSuccess(response, dashboard);
        Assert.Multiple(() =>
        {
            Assert.That(dashboard!.Settings, Has.Count.GreaterThanOrEqualTo(2), "Should have at least 2 settings");
            Assert.That(dashboard.Statistics.TotalSettings, Is.EqualTo(dashboard.Settings.Count));
        });
    }

    #endregion

    #region Complete Dashboard Tests

    #endregion

    #region Complete Dashboard Tests

    [Test]
    public async Task GetDashboard_ReturnsCompleteData_WithAllComponents()
    {
        // Arrange - Create all types of data
        var activeFlag = await CreateFlag("active-flag");
        var constantFlag = await CreateFlag("constant-flag");
        await ToggleFlagConstant(constantFlag.Id);

        var session = await CreateSession("Current Session");

        await CreateOrUpdateSetting(SettingsKeys.MaxDialogueLogTurns, "10");
        await CreateOrUpdateSetting(SettingsKeys.PreviousTurnsCount, "5");

        // Act
        var response = await Client.GetAsync("/api/dashboard");

        // Assert
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardResponse>(JsonOptions);
        AssertDashboardSuccess(response, dashboard);

        Assert.Multiple(() =>
        {
            // Verify all components
            Assert.That(dashboard!.ActiveFlags, Has.Count.EqualTo(2), "Should have 2 active flags");
            Assert.That(dashboard.ActiveSession, Is.Not.Null, "Should have an active session");
            Assert.That(dashboard.Settings, Has.Count.GreaterThanOrEqualTo(2), "Should have at least 2 settings");

            // Verify statistics
            Assert.That(dashboard.Statistics.TotalFlags, Is.EqualTo(2));
            Assert.That(dashboard.Statistics.ActiveFlags, Is.EqualTo(1));
            Assert.That(dashboard.Statistics.ConstantFlags, Is.EqualTo(1));
            Assert.That(dashboard.Statistics.TotalSettings, Is.EqualTo(dashboard.Settings.Count));
        });
    }

    #endregion
}

public class DashboardResponse
{
    public List<Flag> ActiveFlags { get; set; } = [];
    public Session? ActiveSession { get; set; }
    public List<Setting> Settings { get; set; } = [];
    public DashboardStatistics Statistics { get; set; } = new();
}

public class DashboardStatistics
{
    public int TotalFlags { get; set; }
    public int ActiveFlags { get; set; }
    public int ConstantFlags { get; set; }
    public int TotalSettings { get; set; }
}