using System.Net;
using System.Net.Http.Json;
using Tests.Infrastructure;

namespace Tests.ControllerTests;

[TestFixture]
[Category("Integration")]
public class SessionsControllerIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task GetAll_NoSessions_Returns200EmptyList()
    {
        var resp = await _client.GetAsync("/api/sessions");
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var sessions = await resp.Content.ReadFromJsonAsync<List<Session>>(_jsonOptions);
        Assert.That(sessions, Is.Empty);
    }

    [Test]
    public async Task Create_ThenGetById_ReturnsCreatedSession()
    {
        var createResp = await _client.PostAsJsonAsync("/api/sessions", new { Name = "S1" });
        Assert.That(createResp.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await createResp.Content.ReadFromJsonAsync<Session>(_jsonOptions);
        Assert.That(created, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(created!.Id, Is.GreaterThan(0));
            Assert.That(created.Name, Is.EqualTo("S1"));
            Assert.That(created.Number, Is.EqualTo(1));
            Assert.That(created.IsActive, Is.False);
        });

        var getResp = await _client.GetAsync($"/api/sessions/{created.Id}");
        Assert.That(getResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var loaded = await getResp.Content.ReadFromJsonAsync<Session>(_jsonOptions);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Id, Is.EqualTo(created.Id));
            Assert.That(loaded.Turns, Is.Not.Null);
        });
    }

    [Test]
    public async Task Create_Multiple_AssignsIncrementingNumbers()
    {
        var s1 = await (await _client.PostAsJsonAsync("/api/sessions", new { Name = "A" }))
            .Content.ReadFromJsonAsync<Session>(_jsonOptions);
        var s2 = await (await _client.PostAsJsonAsync("/api/sessions", new { Name = "B" }))
            .Content.ReadFromJsonAsync<Session>(_jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(s1!.Number, Is.EqualTo(1));
            Assert.That(s2!.Number, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task GetActive_NoActive_Returns404WithMessage()
    {
        var resp = await _client.GetAsync("/api/sessions/active");
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var body = await resp.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("No active session"));
    }

    [Test]
    public async Task Activate_MakesOnlyThatSessionActive_ActiveEndpointReturnsIt()
    {
        var s1 = await (await _client.PostAsJsonAsync("/api/sessions", new { Name = "A" }))
            .Content.ReadFromJsonAsync<Session>(_jsonOptions);
        var s2 = await (await _client.PostAsJsonAsync("/api/sessions", new { Name = "B" }))
            .Content.ReadFromJsonAsync<Session>(_jsonOptions);

        var activateResp = await _client.PostAsync($"/api/sessions/{s2!.Id}/activate", content: null);
        Assert.That(activateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activeResp = await _client.GetAsync("/api/sessions/active");
        Assert.That(activeResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var active = await activeResp.Content.ReadFromJsonAsync<Session>(_jsonOptions);
        Assert.That(active!.Id, Is.EqualTo(s2.Id));

        var get1 = await _client.GetAsync($"/api/sessions/{s1!.Id}");
        var re1 = await get1.Content.ReadFromJsonAsync<Session>(_jsonOptions);
        var get2 = await _client.GetAsync($"/api/sessions/{s2.Id}");
        var re2 = await get2.Content.ReadFromJsonAsync<Session>(_jsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(re1!.IsActive, Is.False);
            Assert.That(re2!.IsActive, Is.True);
        });
    }

    [Test]
    public async Task Update_NonExisting_Returns404()
    {
        var resp = await _client.PutAsJsonAsync("/api/sessions/99999", new { Name = "X", IsActive = false });
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_Existing_Returns204_ThenGetByIdReturns404()
    {
        var created = await (await _client.PostAsJsonAsync("/api/sessions", new { Name = "ToDelete" }))
            .Content.ReadFromJsonAsync<Session>(_jsonOptions);

        var del = await _client.DeleteAsync($"/api/sessions/{created!.Id}");
        Assert.That(del.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var get = await _client.GetAsync($"/api/sessions/{created.Id}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CompleteWorkflow_CreateUpdateActivateDelete_Works()
    {
        var createResp = await _client.PostAsJsonAsync("/api/sessions", new { Name = "Initial" });
        var created = await createResp.Content.ReadFromJsonAsync<Session>(_jsonOptions);

        var updateResp = await _client.PutAsJsonAsync($"/api/sessions/{created!.Id}", new { Name = "Updated", IsActive = false });
        Assert.That(updateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activateResp = await _client.PostAsync($"/api/sessions/{created.Id}/activate", null);
        Assert.That(activateResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var active = await (await _client.GetAsync("/api/sessions/active")).Content.ReadFromJsonAsync<Session>(_jsonOptions);
        Assert.That(active!.Id, Is.EqualTo(created.Id));

        var deleteResp = await _client.DeleteAsync($"/api/sessions/{created.Id}");
        Assert.That(deleteResp.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var verify = await _client.GetAsync($"/api/sessions/{created.Id}");
        Assert.That(verify.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}