using CAF.Interfaces;

namespace Tests.UnitTests;

/// <summary>
/// Base class for SystemMessageService tests providing common setup and helper methods
/// </summary>
public abstract class SystemMessageServiceTestBase
{
    protected GeneralDbContext DbContext = null!;
    protected DbContextOptions<GeneralDbContext> DbOptions = null!;
    protected Mock<IDbContextFactory<GeneralDbContext>> MockDbContextFactory = null!;
    protected Mock<ILogger<SystemMessageService>> MockLogger = null!;
    protected Mock<IProfileService> MockProfileService = null!;
    protected Mock<IGeminiClient> MockGeminiClient = null!;
    protected SystemMessageService Service = null!;

    [SetUp]
    public void BaseSetup()
    {
        DbOptions = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        DbContext = new GeneralDbContext(DbOptions);

        // Seed a test profile since services now filter by ProfileId
        DbContext.Profiles.Add(new Profile
        {
            Id = 1,
            Name = "Test Profile",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        DbContext.SaveChanges();

        // Create a mock factory that creates a new context each time (using the same options/database)
        MockDbContextFactory = new Mock<IDbContextFactory<GeneralDbContext>>();
        MockDbContextFactory.Setup(f => f.CreateDbContext())
            .Returns(() => new GeneralDbContext(DbOptions));
        MockDbContextFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new GeneralDbContext(DbOptions));

        MockLogger = new Mock<ILogger<SystemMessageService>>();
        MockProfileService = new Mock<IProfileService>();
        MockProfileService.Setup(x => x.GetActiveProfileId()).Returns(1);
        MockGeminiClient = new Mock<IGeminiClient>();

        Service = new SystemMessageService(MockDbContextFactory.Object, MockLogger.Object, MockProfileService.Object, MockGeminiClient.Object);
    }

    [TearDown]
    public void BaseTearDown()
    {
        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
    }

    #region Helper Methods

    protected async Task<SystemMessage> CreateTestMessage(
        string name = "Test",
        string content = "Content",
        SystemMessage.SystemMessageType type = SystemMessage.SystemMessageType.Persona,
        bool isActive = false,
        bool isArchived = false,
        int profileId = 1)
    {
        var message = new SystemMessage
        {
            Name = name,
            Content = content,
            Type = type,
            IsActive = isActive,
            IsArchived = isArchived,
            ProfileId = profileId
        };

        await DbContext.SystemMessages.AddAsync(message);
        await DbContext.SaveChangesAsync();
        return message;
    }

    protected async Task SeedStandardTestData()
    {
        var messages = new[]
        {
            new SystemMessage { Name = "Persona", Content = "Content", Type = SystemMessage.SystemMessageType.Persona, IsArchived = false, ProfileId = 1 },
            new SystemMessage { Name = "Perception", Content = "Content", Type = SystemMessage.SystemMessageType.Perception, IsArchived = false, ProfileId = 1 },
            new SystemMessage { Name = "Technical", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsArchived = false, ProfileId = 1 },
            new SystemMessage { Name = "Archived", Content = "Content", Type = SystemMessage.SystemMessageType.Technical, IsArchived = true, ProfileId = 1 }
        };

        await DbContext.SystemMessages.AddRangeAsync(messages);
        await DbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Reloads an entity from the database, bypassing EF's change tracker cache.
    /// Use this in tests to verify that changes were actually persisted to the database.
    /// </summary>
    protected async Task<SystemMessage?> ReloadEntityAsync(int id)
    {
        // Detach all tracked entities to force a fresh query
        foreach (var entry in DbContext.ChangeTracker.Entries<SystemMessage>())
        {
            entry.State = EntityState.Detached;
        }

        return await DbContext.SystemMessages.FindAsync(id);
    }

    #endregion Helper Methods
}
