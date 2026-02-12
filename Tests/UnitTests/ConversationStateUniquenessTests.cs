using CAF.Services.Conversation;

namespace Tests.UnitTests;

/// <summary>
/// Unit tests for ConversationState data uniqueness guarantees.
/// Verifies that:
/// 1. No duplicate data entries are added to state collections
/// 2. GetAllContextData returns unique entries
/// 3. AddContextData and AddContextDataRange handle duplicates correctly
/// </summary>
[TestFixture]
public class ConversationStateUniquenessTests
{
    private ConversationState _state = null!;

    [SetUp]
    public void Setup()
    {
        _state = new ConversationState
        {
            Session = new Session { Id = 1, Name = "Test Session" },
            CurrentTurn = new Turn { Id = 1, Input = "Test input" }
        };
    }

    #region AddContextData Uniqueness Tests

    [Test]
    public void AddContextData_SameIdTwice_OnlyAddsOnce()
    {
        // Arrange
        var data = new ContextData { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Content 1" };

        // Act
        _state.AddContextData(data);
        _state.AddContextData(data); // Try to add same item again

        // Assert
        Assert.That(_state.Quotes, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddContextData_DifferentObjectsSameId_OnlyAddsFirst()
    {
        // Arrange
        var data1 = new ContextData { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Content 1" };
        var data2 = new ContextData { Id = 1, Name = "Quote 1 Updated", Type = DataType.Quote, Content = "Content 2" };

        // Act
        _state.AddContextData(data1);
        _state.AddContextData(data2); // Different object, same ID

        // Assert
        Assert.That(_state.Quotes, Has.Count.EqualTo(1));
        Assert.That(_state.Quotes.First().Name, Is.EqualTo("Quote 1")); // First one wins
    }

    [Test]
    public void AddContextData_DifferentIds_AddsBoth()
    {
        // Arrange
        var data1 = new ContextData { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Content 1" };
        var data2 = new ContextData { Id = 2, Name = "Quote 2", Type = DataType.Quote, Content = "Content 2" };

        // Act
        _state.AddContextData(data1);
        _state.AddContextData(data2);

        // Assert
        Assert.That(_state.Quotes, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddContextData_RoutesToCorrectCollection_ByType()
    {
        // Arrange
        var quote = new ContextData { Id = 1, Name = "Quote", Type = DataType.Quote, Content = "Content" };
        var memory = new ContextData { Id = 2, Name = "Memory", Type = DataType.Memory, Content = "Content" };
        var insight = new ContextData { Id = 3, Name = "Insight", Type = DataType.Insight, Content = "Content" };
        var profile = new ContextData { Id = 4, Name = "Profile", Type = DataType.CharacterProfile, Content = "Content" };
        var data = new ContextData { Id = 5, Name = "Data", Type = DataType.Generic, Content = "Content" };
        var voiceSample = new ContextData { Id = 6, Name = "Voice", Type = DataType.PersonaVoiceSample, Content = "Content" };

        // Act
        _state.AddContextData(quote);
        _state.AddContextData(memory);
        _state.AddContextData(insight);
        _state.AddContextData(profile);
        _state.AddContextData(data);
        _state.AddContextData(voiceSample);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(_state.Quotes, Has.Count.EqualTo(1));
            Assert.That(_state.Memories, Has.Count.EqualTo(1));
            Assert.That(_state.Insights, Has.Count.EqualTo(1));
            Assert.That(_state.CharacterProfiles, Has.Count.EqualTo(1));
            Assert.That(_state.Data, Has.Count.EqualTo(1));
            Assert.That(_state.PersonaVoiceSamples, Has.Count.EqualTo(1));
        });
    }

    #endregion AddContextData Uniqueness Tests

    #region AddContextDataRange Uniqueness Tests

    [Test]
    public void AddContextDataRange_WithDuplicateIds_OnlyAddsUnique()
    {
        // Arrange
        var dataList = new List<ContextData>
        {
            new() { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Content 1" },
            new() { Id = 1, Name = "Quote 1 Duplicate", Type = DataType.Quote, Content = "Content 1 Dup" },
            new() { Id = 2, Name = "Quote 2", Type = DataType.Quote, Content = "Content 2" }
        };

        // Act
        _state.AddContextDataRange(dataList);

        // Assert
        Assert.That(_state.Quotes, Has.Count.EqualTo(2));
        Assert.That(_state.Quotes.Select(q => q.Id).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public void AddContextDataRange_AfterManualAdd_PreventsDuplicates()
    {
        // Arrange
        var existingData = new ContextData { Id = 1, Name = "Existing Quote", Type = DataType.Quote, Content = "Content" };
        _state.AddContextData(existingData);

        var dataList = new List<ContextData>
        {
            new() { Id = 1, Name = "Duplicate", Type = DataType.Quote, Content = "Should not be added" },
            new() { Id = 2, Name = "New Quote", Type = DataType.Quote, Content = "Should be added" }
        };

        // Act
        _state.AddContextDataRange(dataList);

        // Assert
        Assert.That(_state.Quotes, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(_state.Quotes.Any(q => q.Name == "Existing Quote"), Is.True);
            Assert.That(_state.Quotes.Any(q => q.Name == "New Quote"), Is.True);
            Assert.That(_state.Quotes.Any(q => q.Name == "Duplicate"), Is.False);
        });
    }

    [Test]
    public void AddContextDataRange_MixedTypes_RoutesAndDeduplicatesCorrectly()
    {
        // Arrange
        var dataList = new List<ContextData>
        {
            new() { Id = 1, Name = "Quote 1", Type = DataType.Quote, Content = "Content" },
            new() { Id = 2, Name = "Memory 1", Type = DataType.Memory, Content = "Content" },
            new() { Id = 1, Name = "Quote 1 Dup", Type = DataType.Quote, Content = "Duplicate" },
            new() { Id = 3, Name = "Insight 1", Type = DataType.Insight, Content = "Content" },
            new() { Id = 2, Name = "Memory 1 Dup", Type = DataType.Memory, Content = "Duplicate" }
        };

        // Act
        _state.AddContextDataRange(dataList);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(_state.Quotes, Has.Count.EqualTo(1));
            Assert.That(_state.Memories, Has.Count.EqualTo(1));
            Assert.That(_state.Insights, Has.Count.EqualTo(1));
        });
    }

    #endregion AddContextDataRange Uniqueness Tests

    #region GetAllContextData Uniqueness Tests

    [Test]
    public void GetAllContextData_WithDuplicatesAcrossCollections_ReturnsUnique()
    {
        // Arrange - Manually add duplicate ID across collections (shouldn't happen, but test defense)
        var data = new ContextData { Id = 1, Name = "Data", Type = DataType.Quote, Content = "Content" };
        _state.Quotes.Add(data);
        _state.Quotes.Add(data); // Force duplicate in same collection

        // Act
        var result = _state.GetAllContextData().ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.Select(d => d.Id).Distinct().Count(), Is.EqualTo(result.Count));
    }

    [Test]
    public void GetAllContextData_WithUserProfile_IncludesUserProfileFirst()
    {
        // Arrange
        _state.UserProfile = new ContextData { Id = 100, Name = "User Profile", Type = DataType.CharacterProfile, Content = "User", IsUser = true };
        _state.AddContextData(new ContextData { Id = 1, Name = "Quote", Type = DataType.Quote, Content = "Content" });
        _state.AddContextData(new ContextData { Id = 2, Name = "Memory", Type = DataType.Memory, Content = "Content" });

        // Act
        var result = _state.GetAllContextData().ToList();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(result.First().Id, Is.EqualTo(100)); // User profile first
            Assert.That(result.First().IsUser, Is.True);
        });
    }

    [Test]
    public void GetAllContextData_UserProfileAlsoInCharacterProfiles_NotDuplicated()
    {
        // Arrange - Same ID in UserProfile and CharacterProfiles (edge case)
        var userProfile = new ContextData { Id = 1, Name = "User Profile", Type = DataType.CharacterProfile, Content = "User", IsUser = true };
        _state.UserProfile = userProfile;
        _state.CharacterProfiles.Add(userProfile); // Also added to character profiles

        // Act
        var result = _state.GetAllContextData().ToList();

        // Assert - Should only appear once
        Assert.That(result.Count(d => d.Id == 1), Is.EqualTo(1));
    }

    [Test]
    public void GetAllContextData_AllDataTypes_ReturnsAll()
    {
        // Arrange
        _state.UserProfile = new ContextData { Id = 1, Name = "User", Type = DataType.CharacterProfile, Content = "Content", IsUser = true };
        _state.AddContextData(new ContextData { Id = 2, Name = "Character", Type = DataType.CharacterProfile, Content = "Content" });
        _state.AddContextData(new ContextData { Id = 3, Name = "Quote", Type = DataType.Quote, Content = "Content" });
        _state.AddContextData(new ContextData { Id = 4, Name = "Voice", Type = DataType.PersonaVoiceSample, Content = "Content" });
        _state.AddContextData(new ContextData { Id = 5, Name = "Memory", Type = DataType.Memory, Content = "Content" });
        _state.AddContextData(new ContextData { Id = 6, Name = "Insight", Type = DataType.Insight, Content = "Content" });
        _state.AddContextData(new ContextData { Id = 7, Name = "Data", Type = DataType.Generic, Content = "Content" });

        // Act
        var result = _state.GetAllContextData().ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(7));
    }

    #endregion GetAllContextData Uniqueness Tests

    #region Thread Safety Tests (ConcurrentBag)

    [Test]
    public async Task AddContextData_ParallelAdds_GetAllContextDataReturnsUnique()
    {
        // Arrange
        var items = Enumerable.Range(1, 100)
            .Select(i => new ContextData { Id = i % 10, Name = $"Quote {i}", Type = DataType.Quote, Content = $"Content {i}" })
            .ToList();

        // Act
        await Task.WhenAll(items.Select(item => Task.Run(() => _state.AddContextData(item))));

        // Assert - Due to race conditions in check-then-add, duplicates MAY exist in the underlying collection
        // The GUARANTEE is that GetAllContextData() returns unique entries (via HashSet deduplication)
        var allData = _state.GetAllContextData().ToList();
        var uniqueIds = allData.Select(d => d.Id).Distinct().Count();

        Assert.That(uniqueIds, Is.EqualTo(allData.Count),
            "GetAllContextData should return only unique entries even after parallel adds");
        Assert.That(uniqueIds, Is.LessThanOrEqualTo(10),
            "Should have at most 10 unique IDs (0-9)");
    }

    [Test]
    public async Task GetAllContextData_WhileAdding_NoExceptions()
    {
        // Arrange - Pre-populate with some data
        for (var i = 1; i <= 10; i++)
        {
            _state.AddContextData(new ContextData { Id = i, Name = $"Quote {i}", Type = DataType.Quote, Content = $"Content {i}" });
        }

        // Act - Read while adding
        var addTask = Task.Run(() =>
        {
            for (var i = 11; i <= 100; i++)
            {
                _state.AddContextData(new ContextData { Id = i, Name = $"Quote {i}", Type = DataType.Quote, Content = $"Content {i}" });
            }
        });

        var readTask = Task.Run(() =>
        {
            for (var i = 0; i < 50; i++)
            {
                var result = _state.GetAllContextData().ToList();
                Assert.That(result.Select(d => d.Id).Distinct().Count(), Is.EqualTo(result.Count));
            }
        });

        // Assert - No exceptions
        Assert.DoesNotThrowAsync(async () => await Task.WhenAll(addTask, readTask));
    }

    #endregion Thread Safety Tests (ConcurrentBag)

    #region GetDataByType Tests

    [Test]
    [TestCase(DataType.Quote)]
    [TestCase(DataType.Memory)]
    [TestCase(DataType.Insight)]
    [TestCase(DataType.CharacterProfile)]
    [TestCase(DataType.Generic)]
    [TestCase(DataType.PersonaVoiceSample)]
    public void GetDataByType_ReturnsCorrectCollection(DataType type)
    {
        // Arrange
        var data = new ContextData { Id = 1, Name = "Test", Type = type, Content = "Content" };
        _state.AddContextData(data);

        // Act
        var collection = _state.GetDataByType(type);

        // Assert
        Assert.That(collection, Has.Count.EqualTo(1));
        Assert.That(collection.First().Type, Is.EqualTo(type));
    }

    #endregion GetDataByType Tests
}