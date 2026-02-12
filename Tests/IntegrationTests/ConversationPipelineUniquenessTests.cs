using Tests.Infrastructure;

namespace Tests.IntegrationTests;

/// <summary>
/// Integration tests for ConversationPipeline data uniqueness.
/// Verifies that data loaded from multiple sources contains no duplicates.
/// </summary>
[TestFixture]
public class ConversationPipelineUniquenessTests : ConversationPipelineTestBase
{
    [Test]
    [Category("Integration")]
    [Category("ConversationPipeline")]
    [Category("DataUniqueness")]
    public async Task ProcessInputAsync_DataLoadedFromMultipleSources_NoDuplicatesInState()
    {
        // Arrange - Create data that would be loaded via multiple mechanisms
        var memoryAlwaysOnAndTrigger = await TestData.CreateContextDataAsync(
            TestProfile.Id,
            "Dual Source Memory",
            "This memory can be loaded via AlwaysOn and via Trigger",
            DataType.Memory,
            AvailabilityType.AlwaysOn,
            triggerKeywords: "test,duplicate");

        var input = "Tell me about the test duplicate scenario";

        LLMMocks.ConfigureDefaultGeminiResponse("Test response");

        // Act
        var (state, turn) = await Pipeline.BuildRequestAsync(input);

        // Assert - Memory should only appear once
        var memoryCount = state.Memories.Count(m => m.Name == "Dual Source Memory");
        Assert.That(memoryCount, Is.EqualTo(1),
            "Memory should appear only once even if potentially matchable by multiple mechanisms");

        // Verify GetAllContextData uniqueness
        var allData = state.GetAllContextData().ToList();
        var uniqueIds = allData.Select(d => d.Id).Distinct().Count();
        Assert.That(uniqueIds, Is.EqualTo(allData.Count),
            "GetAllContextData should return only unique entries");
    }

    [Test]
    [Category("Integration")]
    [Category("ConversationPipeline")]
    [Category("DataUniqueness")]
    public async Task ProcessInputAsync_MultipleEnrichersLoadSameDataType_AllUnique()
    {
        // Arrange - Create multiple memories via different availability mechanisms
        var alwaysOnMemory1 = await TestData.CreateAlwaysOnMemoryAsync(
            TestProfile.Id, "AlwaysOn Memory 1", "Always available content 1");

        var alwaysOnMemory2 = await TestData.CreateAlwaysOnMemoryAsync(
            TestProfile.Id, "AlwaysOn Memory 2", "Always available content 2");

        var triggerMemory = await TestData.CreateTriggeredMemoryAsync(
            TestProfile.Id, "Triggered Memory", "This memory is trigger-based", "weather,forecast");

        var input = "What's the weather forecast for today?";

        LLMMocks.ConfigureDefaultGeminiResponse("Weather response");

        // Act
        var (state, turn) = await Pipeline.BuildRequestAsync(input);

        Assert.Multiple(() =>
        {
            // Assert - Verify our specific test memories are included
            Assert.That(state.Memories.Any(m => m.Name == "AlwaysOn Memory 1"), Is.True,
                "AlwaysOn Memory 1 should be loaded");
            Assert.That(state.Memories.Any(m => m.Name == "AlwaysOn Memory 2"), Is.True,
                "AlwaysOn Memory 2 should be loaded");
        });

        // All IDs should be unique
        var memoryIds = state.Memories.Select(m => m.Id).ToList();
        Assert.That(memoryIds.Distinct().Count(), Is.EqualTo(state.Memories.Count),
            "All memory IDs should be unique");
    }

    [Test]
    [Category("Integration")]
    [Category("ConversationPipeline")]
    [Category("DataUniqueness")]
    public async Task FullPipeline_AllEnrichersRun_FinalStateHasUniqueData()
    {
        // Arrange - Create diverse context data
        var alwaysOnQuote = await TestData.CreateContextDataAsync(
            TestProfile.Id, "AlwaysOn Quote", "Always on quote content",
            DataType.Quote, AvailabilityType.AlwaysOn);

        var triggerInsight = await TestData.CreateContextDataAsync(
            TestProfile.Id, "Triggered Insight", "This insight triggers on weather",
            DataType.Insight, AvailabilityType.Trigger, triggerKeywords: "weather,climate");

        var manualData = await TestData.CreateContextDataAsync(
            TestProfile.Id, "Manual Data", "Manually activated data",
            DataType.Generic, AvailabilityType.Manual);
        manualData.UseEveryTurn = true;
        await Db.SaveChangesAsync();

        var alwaysOnCharacterProfile = await TestData.CreateCharacterProfileAsync(
            TestProfile.Id, "NPC Profile", "NPC character profile");

        LLMMocks.ConfigureDefaultGeminiResponse("Full pipeline response");

        var input = "What's the weather like today?";

        // Act
        var (state, turn) = await Pipeline.BuildRequestAsync(input);

        Assert.Multiple(() =>
        {
            // Assert - Verify all expected data is loaded
            Assert.That(state.Quotes.Any(q => q.Name == "AlwaysOn Quote"), Is.True,
                "AlwaysOn quote should be loaded");
            Assert.That(state.Insights.Any(i => i.Name == "Triggered Insight"), Is.True,
                "Triggered insight should be loaded");
            Assert.That(state.Data.Any(d => d.Name == "Manual Data"), Is.True,
                "Manual data should be loaded");
            Assert.That(state.CharacterProfiles.Any(p => p.Name == "NPC Profile"), Is.True,
                "Character profile should be loaded");
        });

        // Verify final uniqueness
        var allData = state.GetAllContextData().ToList();
        var allIds = allData.Select(d => d.Id).ToList();
        var uniqueIds = allIds.Distinct().Count();

        Assert.Multiple(() =>
        {
            Assert.That(uniqueIds, Is.EqualTo(allData.Count),
                    $"All context data IDs should be unique. Found {allData.Count} items but only {uniqueIds} unique IDs");

            // Verify by type
            Assert.That(state.Quotes.Select(q => q.Id).Distinct().Count(), Is.EqualTo(state.Quotes.Count),
                "All quote IDs should be unique");
            Assert.That(state.Memories.Select(m => m.Id).Distinct().Count(), Is.EqualTo(state.Memories.Count),
                "All memory IDs should be unique");
            Assert.That(state.Insights.Select(i => i.Id).Distinct().Count(), Is.EqualTo(state.Insights.Count),
                "All insight IDs should be unique");
            Assert.That(state.Data.Select(d => d.Id).Distinct().Count(), Is.EqualTo(state.Data.Count),
                "All data IDs should be unique");
            Assert.That(state.CharacterProfiles.Select(p => p.Id).Distinct().Count(), Is.EqualTo(state.CharacterProfiles.Count),
                "All character profile IDs should be unique");
        });
    }
}