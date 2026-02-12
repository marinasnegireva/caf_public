using CAF.Interfaces;

namespace Tests.UnitTests;

[TestFixture]
public class SessionServiceTests
{
    private GeneralDbContext _dbContext = null!;
    private SessionService _service = null!;
    private Mock<IProfileService> _mockProfileService = null!;
    private const int TestProfileId = 1;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new GeneralDbContext(options);

        _mockProfileService = new Mock<IProfileService>();
        _mockProfileService.Setup(x => x.GetActiveProfileIdAsync())
            .ReturnsAsync(TestProfileId);

        _service = new SessionService(_dbContext, _mockProfileService.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task CreateSessionAsync_AssignsIncrementedNumber_StartsInactive()
    {
        await _dbContext.Sessions.AddAsync(new Session { Number = 1, Name = "S1", IsActive = false, ProfileId = TestProfileId });
        await _dbContext.Sessions.AddAsync(new Session { Number = 2, Name = "S2", IsActive = true, ProfileId = TestProfileId });
        await _dbContext.SaveChangesAsync();

        var created = await _service.CreateSessionAsync("New");

        Assert.Multiple(() =>
        {
            Assert.That(created.Id, Is.GreaterThan(0));
            Assert.That(created.Number, Is.EqualTo(3));
            Assert.That(created.Name, Is.EqualTo("New"));
            Assert.That(created.IsActive, Is.False);
            Assert.That(created.CreatedAt, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
        });
    }

    [Test]
    public async Task GetAllSessionsAsync_OrdersByCreatedAtDescending()
    {
        await _dbContext.Sessions.AddRangeAsync(
            new Session { Number = 1, Name = "Old", CreatedAt = DateTime.UtcNow.AddDays(-2), ProfileId = TestProfileId },
            new Session { Number = 2, Name = "New", CreatedAt = DateTime.UtcNow, ProfileId = TestProfileId }
        );
        await _dbContext.SaveChangesAsync();

        var sessions = await _service.GetAllSessionsAsync();

        Assert.That(sessions, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(sessions[0].Name, Is.EqualTo("New"));
            Assert.That(sessions[1].Name, Is.EqualTo("Old"));
        });
    }

    [Test]
    public async Task GetByIdAsync_IncludesTurns_OrderedByCreatedAtAscending()
    {
        var session = new Session { Number = 1, Name = "S", ProfileId = TestProfileId };
        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var t1 = new Turn { SessionId = session.Id, Input = "1", CreatedAt = DateTime.UtcNow.AddMinutes(-2) };
        var t2 = new Turn { SessionId = session.Id, Input = "2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) };
        await _dbContext.Turns.AddRangeAsync(t2, t1);
        await _dbContext.SaveChangesAsync();

        var loaded = await _service.GetByIdAsync(session.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Turns, Has.Count.EqualTo(2));
            Assert.That(loaded.Turns[0].Input, Is.EqualTo("1"));
            Assert.That(loaded.Turns[1].Input, Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task GetActiveSessionAsync_ReturnsActive_IncludesOrderedTurns()
    {
        var inactive = new Session { Number = 1, Name = "Inactive", IsActive = false, ProfileId = TestProfileId };
        var active = new Session { Number = 2, Name = "Active", IsActive = true, ProfileId = TestProfileId };
        await _dbContext.Sessions.AddRangeAsync(inactive, active);
        await _dbContext.SaveChangesAsync();

        await _dbContext.Turns.AddRangeAsync(
            new Turn { SessionId = active.Id, Input = "later", CreatedAt = DateTime.UtcNow.AddMinutes(2) },
            new Turn { SessionId = active.Id, Input = "earlier", CreatedAt = DateTime.UtcNow.AddMinutes(1) }
        );
        await _dbContext.SaveChangesAsync();

        var loaded = await _service.GetActiveSessionAsync();

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Id, Is.EqualTo(active.Id));
            Assert.That(loaded.Turns[0].Input, Is.EqualTo("earlier"));
            Assert.That(loaded.Turns[1].Input, Is.EqualTo("later"));
        });
    }

    [Test]
    public async Task UpdateSessionAsync_Existing_UpdatesFieldsAndModifiedAt()
    {
        var session = new Session { Number = 1, Name = "Old", IsActive = false, ProfileId = TestProfileId };
        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var updated = await _service.UpdateSessionAsync(session.Id, "New", true);

        Assert.Multiple(() =>
        {
            Assert.That(updated.Name, Is.EqualTo("New"));
            Assert.That(updated.IsActive, Is.True);
            Assert.That(updated.ModifiedAt, Is.Not.Null);
        });

        var fromDb = await _dbContext.Sessions.FindAsync(session.Id);
        Assert.That(fromDb!.Name, Is.EqualTo("New"));
    }

    [Test]
    public void UpdateSessionAsync_NonExisting_ThrowsKeyNotFoundException()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _service.UpdateSessionAsync(999, "X", false));
    }

    [Test]
    public async Task DeleteSessionAsync_Existing_RemovesAndReturnsTrue()
    {
        var session = new Session { Number = 1, Name = "Delete", ProfileId = TestProfileId };
        await _dbContext.Sessions.AddAsync(session);
        await _dbContext.SaveChangesAsync();

        var result = await _service.DeleteSessionAsync(session.Id);

        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.True);
            Assert.That(await _dbContext.Sessions.FindAsync(session.Id), Is.Null);
        });
    }

    [Test]
    public async Task DeleteSessionAsync_NonExisting_ReturnsFalse()
    {
        var result = await _service.DeleteSessionAsync(999);
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task SetActiveSessionAsync_DeactivatesAll_ActivatesTarget()
    {
        var s1 = new Session { Number = 1, Name = "S1", IsActive = true, ProfileId = TestProfileId };
        var s2 = new Session { Number = 2, Name = "S2", IsActive = true, ProfileId = TestProfileId };
        await _dbContext.Sessions.AddRangeAsync(s1, s2);
        await _dbContext.SaveChangesAsync();

        var ok = await _service.SetActiveSessionAsync(s1.Id);

        Assert.That(ok, Is.True);

        var reloaded = await _dbContext.Sessions.OrderBy(s => s.Id).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Single(s => s.Id == s1.Id).IsActive, Is.True);
            Assert.That(reloaded.Single(s => s.Id == s2.Id).IsActive, Is.False);
            Assert.That(reloaded.Single(s => s.Id == s1.Id).ModifiedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task SetActiveSessionAsync_NonExisting_ReturnsFalse_DoesNotDeactivateOthers()
    {
        var s1 = new Session { Number = 1, Name = "S1", IsActive = true, ProfileId = TestProfileId };
        var s2 = new Session { Number = 2, Name = "S2", IsActive = false, ProfileId = TestProfileId };
        await _dbContext.Sessions.AddRangeAsync(s1, s2);
        await _dbContext.SaveChangesAsync();

        var ok = await _service.SetActiveSessionAsync(999);

        Assert.That(ok, Is.False);

        var reloaded = await _dbContext.Sessions.OrderBy(s => s.Id).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Single(s => s.Id == s1.Id).IsActive, Is.True);
            Assert.That(reloaded.Single(s => s.Id == s2.Id).IsActive, Is.False);
        });
    }
}