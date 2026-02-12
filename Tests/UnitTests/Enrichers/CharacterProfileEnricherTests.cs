using CAF.Interfaces;
using CAF.Services.Conversation;

namespace Tests.UnitTests.Enrichers;

/// <summary>
/// Unit tests for CharacterProfileEnricher - handles CharacterProfile data type.
/// Supports: AlwaysOn, Manual
/// Does NOT support: Semantic (handled by SemanticDataEnricher), Trigger (handled by TriggerEnricher)
/// Special handling: User profile (IsUser=true) is always loaded into state.UserProfile
/// </summary>
[TestFixture]
public class CharacterProfileEnricherTests
{
    private Mock<IContextDataService> _mockContextDataService = null!;
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<ILogger<CharacterProfileEnricher>> _mockLogger = null!;
    private CharacterProfileEnricher _enricher = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);

        _mockContextDataService = new Mock<IContextDataService>();
        _mockLogger = new Mock<ILogger<CharacterProfileEnricher>>();

        _enricher = new CharacterProfileEnricher(
            _mockContextDataService.Object,
            _dbContextFactory,
            _mockLogger.Object);
    }

    [Test]
    public async Task EnrichAsync_LoadsUserProfile()
    {
        // Arrange
        var state = CreateMinimalState();
        var userProfile = new ContextData
        {
            Id = 1,
            Name = "User Profile",
            Type = DataType.CharacterProfile,
            IsUser = true
        };

        _mockContextDataService.Setup(s => s.GetUserProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(userProfile);
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.UserProfile, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(state.UserProfile!.IsUser, Is.True);
            Assert.That(state.UserName, Is.EqualTo("User Profile"));
        });
    }

    [Test]
    public async Task EnrichAsync_LoadsAlwaysOnCharacterProfiles()
    {
        // Arrange
        var state = CreateMinimalState();
        var alwaysOnData = new List<ContextData>
        {
            new() { Id = 2, Name = "NPC 1", Type = DataType.CharacterProfile },
            new() { Id = 3, Name = "NPC 2", Type = DataType.CharacterProfile }
        };

        _mockContextDataService.Setup(s => s.GetUserProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextData?)null);
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(alwaysOnData);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.CharacterProfiles, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task EnrichAsync_LoadsManualCharacterProfiles()
    {
        // Arrange
        var state = CreateMinimalState();
        var manualData = new List<ContextData>
        {
            new() { Id = 4, Name = "Manual NPC", Type = DataType.CharacterProfile, UseEveryTurn = true }
        };

        _mockContextDataService.Setup(s => s.GetUserProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextData?)null);
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manualData);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.CharacterProfiles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EnrichAsync_DeduplicatesData()
    {
        // Arrange
        var state = CreateMinimalState();

        var duplicate = new ContextData { Id = 1, Name = "Duplicate", Type = DataType.CharacterProfile };

        _mockContextDataService.Setup(s => s.GetUserProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextData?)null);
        _mockContextDataService.Setup(s => s.GetAlwaysOnDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);
        _mockContextDataService.Setup(s => s.GetActiveManualDataAsync(DataType.CharacterProfile, It.IsAny<CancellationToken>()))
            .ReturnsAsync([duplicate]);

        // Act
        await _enricher.EnrichAsync(state);

        // Assert
        Assert.That(state.CharacterProfiles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task EnrichAsync_WithNullState_DoesNotThrow()
    {
        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _enricher.EnrichAsync(null!));
    }

    [Test]
    public async Task EnrichAsync_WithException_LogsError()
    {
        // Arrange
        var state = CreateMinimalState();
        _mockContextDataService.Setup(s => s.GetUserProfileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        await _enricher.EnrichAsync(state);

        // Assert - Should not throw, just log error
        Assert.That(state.UserProfile, Is.Null);
    }

    private static ConversationState CreateMinimalState()
    {
        return new ConversationState
        {
            CurrentTurn = new Turn { Id = 1, Input = "Test input" },
            Session = new Session { Id = 1, Name = "Test Session" }
        };
    }

    private class TestDbContextFactory(DbContextOptions<GeneralDbContext> options)
        : IDbContextFactory<GeneralDbContext>
    {
        public GeneralDbContext CreateDbContext() => new(options);

        public Task<GeneralDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}