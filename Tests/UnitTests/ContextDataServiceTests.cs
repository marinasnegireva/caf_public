using CAF.Interfaces;

namespace Tests.UnitTests;

/// <summary>
/// Comprehensive unit tests for ContextDataService covering all availability types and data types
/// </summary>
[TestFixture]
public class ContextDataServiceTests
{
    private IDbContextFactory<GeneralDbContext> _dbContextFactory = null!;
    private Mock<IProfileService> _mockProfileService = null!;
    private Mock<ISemanticService> _mockSemanticService = null!;
    private Mock<ILogger<ContextDataService>> _mockLogger = null!;
    private ContextDataService _service = null!;
    private const int TestProfileId = 1;

    [SetUp]
    public async Task Setup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContextFactory = new TestDbContextFactory(options);
        _mockProfileService = new Mock<IProfileService>();
        _mockSemanticService = new Mock<ISemanticService>();
        _mockLogger = new Mock<ILogger<ContextDataService>>();

        _mockProfileService.Setup(x => x.GetActiveProfileId()).Returns(TestProfileId);

        _service = new ContextDataService(
            _dbContextFactory,
            _mockProfileService.Object,
            _mockSemanticService.Object,
            _mockLogger.Object);

        // Seed test profile
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.Profiles.Add(new Profile { Id = TestProfileId, Name = "Test Profile" });
        await db.SaveChangesAsync();
    }

    #region Basic CRUD Tests

    [Test]
    public async Task CreateAsync_ValidData_CreatesAndReturnsData()
    {
        // Arrange
        var data = new ContextData
        {
            Name = "Test Entry",
            Content = "Test content",
            Type = DataType.Quote,
            Availability = AvailabilityType.Semantic
        };

        // Act
        var result = await _service.CreateAsync(data);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(result.Id, Is.GreaterThan(0));
            Assert.That(result.Name, Is.EqualTo("Test Entry"));
            Assert.That(result.ProfileId, Is.EqualTo(TestProfileId));
        });
    }

    [Test]
    public async Task CreateAsync_InvalidCombination_ThrowsException()
    {
        // Arrange - PersonaVoiceSample cannot have Manual availability
        var data = new ContextData
        {
            Name = "Invalid Entry",
            Content = "Test content",
            Type = DataType.PersonaVoiceSample,
            Availability = AvailabilityType.Manual
        };

        // Act & Assert
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.CreateAsync(data));
    }

    [Test]
    public async Task GetByIdAsync_ExistingId_ReturnsData()
    {
        // Arrange
        var data = await CreateTestDataAsync();

        // Act
        var result = await _service.GetByIdAsync(data.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo(data.Name));
    }

    [Test]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Act
        var result = await _service.GetByIdAsync(99999);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task UpdateAsync_ValidData_UpdatesAndReturnsData()
    {
        // Arrange
        var data = await CreateTestDataAsync();
        data.Name = "Updated Name";
        data.Content = "Updated content";

        // Act
        var result = await _service.UpdateAsync(data.Id, data);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.Name, Is.EqualTo("Updated Name"));
            Assert.That(result.Content, Is.EqualTo("Updated content"));
        });
    }

    [Test]
    public async Task DeleteAsync_ExistingId_DeletesData()
    {
        // Arrange
        var data = await CreateTestDataAsync();

        // Act
        var result = await _service.DeleteAsync(data.Id);

        // Assert
        Assert.That(result, Is.True);
        var deleted = await _service.GetByIdAsync(data.Id);
        Assert.That(deleted, Is.Null);
    }

    [Test]
    public async Task ArchiveAsync_ExistingId_ArchivesData()
    {
        // Arrange
        var data = await CreateTestDataAsync();

        // Act
        var result = await _service.ArchiveAsync(data.Id);

        // Assert
        Assert.That(result, Is.True);
        var archived = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(archived!.IsArchived, Is.True);
            Assert.That(archived.Availability, Is.EqualTo(AvailabilityType.Archive));
        });
    }

    [Test]
    public async Task RestoreAsync_ArchivedId_RestoresData()
    {
        // Arrange
        var data = await CreateTestDataAsync();
        await _service.ArchiveAsync(data.Id);

        // Act
        var result = await _service.RestoreAsync(data.Id);

        // Assert
        Assert.That(result, Is.True);
        var restored = await _service.GetByIdAsync(data.Id);
        Assert.That(restored!.IsArchived, Is.False);
    }

    #endregion Basic CRUD Tests

    #region Availability-Based Retrieval Tests

    [Test]
    public async Task GetAlwaysOnDataAsync_ReturnsOnlyAlwaysOnData()
    {
        // Arrange
        await CreateTestDataAsync(availability: AvailabilityType.AlwaysOn);
        await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await CreateTestDataAsync(availability: AvailabilityType.Manual);

        // Act
        var result = await _service.GetAlwaysOnDataAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.All(d => d.Availability == AvailabilityType.AlwaysOn), Is.True);
    }

    [Test]
    public async Task GetAlwaysOnDataAsync_WithTypeFilter_FiltersCorrectly()
    {
        // Arrange
        await CreateTestDataAsync(type: DataType.Quote, availability: AvailabilityType.AlwaysOn);
        await CreateTestDataAsync(type: DataType.Memory, availability: AvailabilityType.AlwaysOn);

        // Act
        var result = await _service.GetAlwaysOnDataAsync(typeFilter: DataType.Quote);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(DataType.Quote));
    }

    [Test]
    public async Task GetActiveManualDataAsync_ReturnsOnlyActiveManual()
    {
        // Arrange
        var useEveryTurn = await CreateTestDataAsync(availability: AvailabilityType.Manual);
        useEveryTurn.UseEveryTurn = true;
        await _service.UpdateAsync(useEveryTurn.Id, useEveryTurn);

        var useNextTurn = await CreateTestDataAsync(availability: AvailabilityType.Manual);
        useNextTurn.UseNextTurnOnly = true;
        await _service.UpdateAsync(useNextTurn.Id, useNextTurn);

        var inactive = await CreateTestDataAsync(availability: AvailabilityType.Manual);
        // Not setting any toggle - should not be returned

        // Act
        var result = await _service.GetActiveManualDataAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetUserProfileAsync_ReturnsUserProfile()
    {
        // Arrange
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var userProfile = new ContextData
        {
            Name = "User Profile",
            Content = "User profile content",
            Type = DataType.CharacterProfile,
            Availability = AvailabilityType.AlwaysOn,
            IsUser = true,
            IsEnabled = true,
            ProfileId = TestProfileId
        };
        db.ContextData.Add(userProfile);
        await db.SaveChangesAsync();

        // Act
        var result = await _service.GetUserProfileAsync();

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result!.IsUser, Is.True);
            Assert.That(result.Type, Is.EqualTo(DataType.CharacterProfile));
        });
    }

    #endregion Availability-Based Retrieval Tests

    #region Trigger-Based Retrieval Tests

    [Test]
    public async Task GetTriggerDataAsync_ReturnsOnlyTriggerData()
    {
        // Arrange
        await CreateTriggerDataAsync("test,keyword");
        await CreateTestDataAsync(availability: AvailabilityType.Semantic);

        // Act
        var result = await _service.GetTriggerDataAsync();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Availability, Is.EqualTo(AvailabilityType.Trigger));
    }

    [Test]
    public async Task EvaluateTriggersAsync_MatchingKeywords_ReturnsMatchedData()
    {
        // Arrange
        await CreateTriggerDataAsync("weather,rain,sunny", minMatchCount: 1);
        await CreateTriggerDataAsync("emotion,feeling,mood", minMatchCount: 2);

        // Act
        var result = await _service.EvaluateTriggersAsync("The weather is nice and sunny today");

        // Assert
        Assert.That(result, Has.Count.EqualTo(1)); // Only weather trigger should match
    }

    [Test]
    public async Task EvaluateTriggersAsync_MinMatchCount_RespectsThreshold()
    {
        // Arrange
        await CreateTriggerDataAsync("alpha,beta,gamma", minMatchCount: 2);

        // Act - Only one keyword match
        var result1 = await _service.EvaluateTriggersAsync("The alpha test");
        Assert.That(result1, Has.Count.EqualTo(0));

        // Act - Two keyword matches
        var result2 = await _service.EvaluateTriggersAsync("The alpha and beta test");
        Assert.That(result2, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RecordTriggerActivationAsync_IncrementsCount()
    {
        // Arrange
        var data = await CreateTriggerDataAsync("test");
        var initialCount = data.TriggerCount;

        // Act
        await _service.RecordTriggerActivationAsync(data.Id);

        // Assert
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.TriggerCount, Is.EqualTo(initialCount + 1));
            Assert.That(updated.LastTriggeredAt, Is.Not.Null);
        });
    }

    #endregion Trigger-Based Retrieval Tests

    #region Manual Toggle Management Tests

    [Test]
    public async Task SetUseNextTurnAsync_SetsFlag()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);

        // Act
        var result = await _service.SetUseNextTurnAsync(data.Id);

        // Assert
        Assert.That(result, Is.True);
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.UseNextTurnOnly, Is.True);
            Assert.That(updated.Availability, Is.EqualTo(AvailabilityType.Manual));
            Assert.That(updated.PreviousAvailability, Is.EqualTo(AvailabilityType.Semantic));
        });
    }

    [Test]
    public async Task SetUseEveryTurnAsync_EnablesFlag()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);

        // Act
        var result = await _service.SetUseEveryTurnAsync(data.Id, enabled: true);

        // Assert
        Assert.That(result, Is.True);
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.That(updated!.UseEveryTurn, Is.True);
    }

    [Test]
    public async Task ProcessPostTurnAsync_RevertsNextTurnEntries()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(data.Id);

        // Act
        await _service.ProcessPostTurnAsync();

        // Assert
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.UseNextTurnOnly, Is.False);
            Assert.That(updated.Availability, Is.EqualTo(AvailabilityType.Semantic));
            Assert.That(updated.PreviousAvailability, Is.Null);
        });
    }

    #endregion Manual Toggle Management Tests

    #region Data Type Validation Tests

    [Test]
    [TestCase(DataType.Quote, AvailabilityType.AlwaysOn, true)]
    [TestCase(DataType.Quote, AvailabilityType.Manual, true)]
    [TestCase(DataType.Quote, AvailabilityType.Semantic, true)]
    [TestCase(DataType.Quote, AvailabilityType.Trigger, false)]
    [TestCase(DataType.Quote, AvailabilityType.Archive, true)]
    [TestCase(DataType.PersonaVoiceSample, AvailabilityType.AlwaysOn, true)]
    [TestCase(DataType.PersonaVoiceSample, AvailabilityType.Manual, false)]
    [TestCase(DataType.PersonaVoiceSample, AvailabilityType.Semantic, true)]
    [TestCase(DataType.PersonaVoiceSample, AvailabilityType.Trigger, false)]
    [TestCase(DataType.Memory, AvailabilityType.AlwaysOn, true)]
    [TestCase(DataType.Memory, AvailabilityType.Manual, true)]
    [TestCase(DataType.Memory, AvailabilityType.Semantic, true)]
    [TestCase(DataType.Memory, AvailabilityType.Trigger, true)]
    [TestCase(DataType.Insight, AvailabilityType.AlwaysOn, true)]
    [TestCase(DataType.Insight, AvailabilityType.Manual, true)]
    [TestCase(DataType.Insight, AvailabilityType.Semantic, true)]
    [TestCase(DataType.Insight, AvailabilityType.Trigger, true)]
    [TestCase(DataType.CharacterProfile, AvailabilityType.AlwaysOn, true)]
    [TestCase(DataType.CharacterProfile, AvailabilityType.Manual, true)]
    [TestCase(DataType.CharacterProfile, AvailabilityType.Semantic, false)]
    [TestCase(DataType.CharacterProfile, AvailabilityType.Trigger, true)]
    [TestCase(DataType.Generic, AvailabilityType.AlwaysOn, true)]
    [TestCase(DataType.Generic, AvailabilityType.Manual, true)]
    [TestCase(DataType.Generic, AvailabilityType.Semantic, false)]
    [TestCase(DataType.Generic, AvailabilityType.Trigger, true)]
    public async Task CreateAsync_ValidatesDataTypeAvailabilityCombination(
        DataType type, AvailabilityType availability, bool shouldSucceed)
    {
        // Arrange
        var data = new ContextData
        {
            Name = $"Test {type} {availability}",
            Content = "Test content",
            Type = type,
            Availability = availability
        };

        // Act & Assert
        if (shouldSucceed)
        {
            var result = await _service.CreateAsync(data);
            Assert.That(result.Id, Is.GreaterThan(0));
        }
        else
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _service.CreateAsync(data));
        }
    }

    #endregion Data Type Validation Tests

    #region GetAllAsync Filter Tests

    [Test]
    public async Task GetAllAsync_TypeFilter_ReturnsCorrectType()
    {
        // Arrange
        await CreateTestDataAsync(type: DataType.Quote);
        await CreateTestDataAsync(type: DataType.Memory);
        await CreateTestDataAsync(type: DataType.Insight);

        // Act
        var result = await _service.GetAllAsync(type: DataType.Quote);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Type, Is.EqualTo(DataType.Quote));
    }

    [Test]
    public async Task GetAllAsync_AvailabilityFilter_ReturnsCorrectAvailability()
    {
        // Arrange
        await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await CreateTestDataAsync(availability: AvailabilityType.AlwaysOn);

        // Act
        var result = await _service.GetAllAsync(availability: AvailabilityType.Semantic);

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Availability, Is.EqualTo(AvailabilityType.Semantic));
    }

    [Test]
    public async Task GetAllAsync_IncludeArchived_ReturnsArchivedData()
    {
        // Arrange
        var archived = await CreateTestDataAsync();
        await _service.ArchiveAsync(archived.Id);
        await CreateTestDataAsync(); // Active data

        // Act
        var withArchived = await _service.GetAllAsync(includeArchived: true);
        var withoutArchived = await _service.GetAllAsync(includeArchived: false);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(withArchived, Has.Count.EqualTo(2));
            Assert.That(withoutArchived, Has.Count.EqualTo(1));
        });
    }

    #endregion GetAllAsync Filter Tests

    #region Availability Mechanism Integrity Tests

    [Test]
    public async Task SetUseNextTurnAsync_FromTrigger_PreservesPreviousAvailability()
    {
        // Arrange - Create trigger-based data
        var data = await CreateTriggerDataAsync("keyword1,keyword2");
        Assert.That(data.Availability, Is.EqualTo(AvailabilityType.Trigger));

        // Act - Set use next turn
        await _service.SetUseNextTurnAsync(data.Id);

        // Assert
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Availability, Is.EqualTo(AvailabilityType.Manual), "Availability should be Manual");
            Assert.That(updated.PreviousAvailability, Is.EqualTo(AvailabilityType.Trigger), "Previous availability should be preserved");
            Assert.That(updated.UseNextTurnOnly, Is.True, "UseNextTurnOnly flag should be set");
        });
    }

    [Test]
    public async Task SetUseNextTurnAsync_FromAlwaysOn_PreservesPreviousAvailability()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.AlwaysOn);

        // Act
        await _service.SetUseNextTurnAsync(data.Id);

        // Assert
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Availability, Is.EqualTo(AvailabilityType.Manual));
            Assert.That(updated.PreviousAvailability, Is.EqualTo(AvailabilityType.AlwaysOn));
        });
    }

    [Test]
    public async Task SetUseNextTurnAsync_AlreadyManual_DoesNotOverwritePrevious()
    {
        // Arrange - Create semantic data and convert to manual
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(data.Id); // Now Manual with PreviousAvailability=Semantic

        // Act - Call SetUseNextTurn again
        await _service.SetUseNextTurnAsync(data.Id);

        // Assert - PreviousAvailability should still be Semantic, not Manual
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.That(updated!.PreviousAvailability, Is.EqualTo(AvailabilityType.Semantic),
            "Original previous availability should be preserved");
    }

    [Test]
    public async Task ProcessPostTurnAsync_MultipleTriggerOrigins_AllRevert()
    {
        // Arrange - Create multiple entries with different original availabilities
        var triggerData = await CreateTriggerDataAsync("keyword1");
        await _service.SetUseNextTurnAsync(triggerData.Id);

        var semanticData = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(semanticData.Id);

        var alwaysOnData = await CreateTestDataAsync(availability: AvailabilityType.AlwaysOn);
        await _service.SetUseNextTurnAsync(alwaysOnData.Id);

        // Act
        await _service.ProcessPostTurnAsync();

        // Assert - All should revert to their original availability
        var updatedTrigger = await _service.GetByIdAsync(triggerData.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updatedTrigger!.Availability, Is.EqualTo(AvailabilityType.Trigger));
            Assert.That(updatedTrigger.PreviousAvailability, Is.Null);
            Assert.That(updatedTrigger.UseNextTurnOnly, Is.False);
        });

        var updatedSemantic = await _service.GetByIdAsync(semanticData.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updatedSemantic!.Availability, Is.EqualTo(AvailabilityType.Semantic));
            Assert.That(updatedSemantic.PreviousAvailability, Is.Null);
        });

        var updatedAlwaysOn = await _service.GetByIdAsync(alwaysOnData.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updatedAlwaysOn!.Availability, Is.EqualTo(AvailabilityType.AlwaysOn));
            Assert.That(updatedAlwaysOn.PreviousAvailability, Is.Null);
        });
    }

    [Test]
    public async Task SetUseEveryTurnAsync_DisableRestoresPreviousAvailability()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseEveryTurnAsync(data.Id, enabled: true);

        // Verify it's now Manual
        var manual = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(manual!.Availability, Is.EqualTo(AvailabilityType.Manual));
            Assert.That(manual.PreviousAvailability, Is.EqualTo(AvailabilityType.Semantic));
        });

        // Act - Disable use every turn
        await _service.SetUseEveryTurnAsync(data.Id, enabled: false);

        // Assert - Should revert to Semantic
        var reverted = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reverted!.Availability, Is.EqualTo(AvailabilityType.Semantic));
            Assert.That(reverted.PreviousAvailability, Is.Null);
            Assert.That(reverted.UseEveryTurn, Is.False);
        });
    }

    [Test]
    public async Task SetUseNextTurn_ThenUseEveryTurn_KeepsOriginalPreviousAvailability()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(data.Id);

        // Act - Switch from UseNextTurn to UseEveryTurn
        await _service.SetUseEveryTurnAsync(data.Id, enabled: true);

        // Assert - PreviousAvailability should still be Semantic
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Availability, Is.EqualTo(AvailabilityType.Manual));
            Assert.That(updated.PreviousAvailability, Is.EqualTo(AvailabilityType.Semantic));
            Assert.That(updated.UseEveryTurn, Is.True);
            Assert.That(updated.UseNextTurnOnly, Is.False);
        });
    }

    [Test]
    public async Task ChangeAvailabilityAsync_ClearsManualFlags()
    {
        // Arrange
        var data = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(data.Id);

        // Act - Change availability directly
        await _service.ChangeAvailabilityAsync(data.Id, AvailabilityType.AlwaysOn);

        // Assert - Manual flags should be cleared
        var updated = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(updated!.Availability, Is.EqualTo(AvailabilityType.AlwaysOn));
            Assert.That(updated.UseNextTurnOnly, Is.False);
            Assert.That(updated.UseEveryTurn, Is.False);
            Assert.That(updated.PreviousAvailability, Is.Null);
        });
    }

    [Test]
    public async Task ChangeAvailabilityAsync_InvalidCombination_ReturnsFalse()
    {
        // Arrange - PersonaVoiceSample cannot have Manual availability
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var voiceSample = new ContextData
        {
            Name = "Voice Sample",
            Content = "Sample content",
            Type = DataType.PersonaVoiceSample,
            Availability = AvailabilityType.Semantic,
            IsEnabled = true,
            ProfileId = TestProfileId
        };
        db.ContextData.Add(voiceSample);
        await db.SaveChangesAsync();

        // Act
        var result = await _service.ChangeAvailabilityAsync(voiceSample.Id, AvailabilityType.Manual);

        // Assert
        Assert.That(result, Is.False);
        var unchanged = await _service.GetByIdAsync(voiceSample.Id);
        Assert.That(unchanged!.Availability, Is.EqualTo(AvailabilityType.Semantic));
    }

    [Test]
    public async Task ProcessPostTurnAsync_OnlyAffectsUseNextTurnOnly_NotUseEveryTurn()
    {
        // Arrange
        var useNextTurn = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(useNextTurn.Id);

        var useEveryTurn = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseEveryTurnAsync(useEveryTurn.Id, enabled: true);

        // Act
        await _service.ProcessPostTurnAsync();

        // Assert - UseNextTurn should revert
        var revertedNextTurn = await _service.GetByIdAsync(useNextTurn.Id);
        Assert.Multiple(() =>
        {
            Assert.That(revertedNextTurn!.Availability, Is.EqualTo(AvailabilityType.Semantic));
            Assert.That(revertedNextTurn.UseNextTurnOnly, Is.False);
        });

        // Assert - UseEveryTurn should NOT revert
        var stayedEveryTurn = await _service.GetByIdAsync(useEveryTurn.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stayedEveryTurn!.Availability, Is.EqualTo(AvailabilityType.Manual));
            Assert.That(stayedEveryTurn.UseEveryTurn, Is.True);
            Assert.That(stayedEveryTurn.PreviousAvailability, Is.EqualTo(AvailabilityType.Semantic));
        });
    }

    [Test]
    public async Task Entry_CanOnlyHaveOneAvailabilityMechanism_AtATime()
    {
        // Arrange - Use Memory type which supports all availability types (Semantic, Trigger, AlwaysOn, Manual)
        var data = await CreateTestDataAsync(type: DataType.Memory, availability: AvailabilityType.Semantic);

        // Act - Verify current state
        var current = await _service.GetByIdAsync(data.Id);
        Assert.That(current!.Availability, Is.EqualTo(AvailabilityType.Semantic));

        // Change to Trigger
        await _service.ChangeAvailabilityAsync(data.Id, AvailabilityType.Trigger);
        current = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(current!.Availability, Is.EqualTo(AvailabilityType.Trigger));
            Assert.That(current.Availability, Is.Not.EqualTo(AvailabilityType.Semantic));
        });

        // Change to AlwaysOn
        await _service.ChangeAvailabilityAsync(data.Id, AvailabilityType.AlwaysOn);
        current = await _service.GetByIdAsync(data.Id);
        Assert.Multiple(() =>
        {
            Assert.That(current!.Availability, Is.EqualTo(AvailabilityType.AlwaysOn));
            Assert.That(current.Availability, Is.Not.EqualTo(AvailabilityType.Trigger));
        });
    }

    [Test]
    public async Task GetActiveManualDataAsync_OnlyReturnsActiveToggles()
    {
        // Arrange - Create various manual states
        var useNextTurn = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseNextTurnAsync(useNextTurn.Id);

        var useEveryTurn = await CreateTestDataAsync(availability: AvailabilityType.Semantic);
        await _service.SetUseEveryTurnAsync(useEveryTurn.Id, enabled: true);

        var inactiveManual = await CreateTestDataAsync(availability: AvailabilityType.Manual);
        // Neither UseNextTurnOnly nor UseEveryTurn set

        // Act
        var activeManual = await _service.GetActiveManualDataAsync();

        // Assert
        Assert.That(activeManual, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(activeManual.Any(d => d.Id == useNextTurn.Id), Is.True);
            Assert.That(activeManual.Any(d => d.Id == useEveryTurn.Id), Is.True);
            Assert.That(activeManual.Any(d => d.Id == inactiveManual.Id), Is.False);
        });
    }

    #endregion Availability Mechanism Integrity Tests

    #region Helper Methods

    private async Task<ContextData> CreateTestDataAsync(
        DataType type = DataType.Quote,
        AvailabilityType availability = AvailabilityType.Semantic,
        string? name = null)
    {
        var data = new ContextData
        {
            Name = name ?? $"Test {type} {Guid.NewGuid():N}",
            Content = "Test content",
            Type = type,
            Availability = availability
        };
        return await _service.CreateAsync(data);
    }

    private async Task<ContextData> CreateTriggerDataAsync(
        string keywords,
        int minMatchCount = 1)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var data = new ContextData
        {
            Name = $"Trigger {Guid.NewGuid():N}",
            Content = "Trigger content",
            Type = DataType.Generic,
            Availability = AvailabilityType.Trigger,
            TriggerKeywords = keywords,
            TriggerMinMatchCount = minMatchCount,
            IsEnabled = true,
            ProfileId = TestProfileId
        };
        db.ContextData.Add(data);
        await db.SaveChangesAsync();
        return data;
    }

    #endregion Helper Methods

    private class TestDbContextFactory(DbContextOptions<GeneralDbContext> options)
        : IDbContextFactory<GeneralDbContext>
    {
        public GeneralDbContext CreateDbContext()
        {
            return new GeneralDbContext(options);
        }

        public Task<GeneralDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}