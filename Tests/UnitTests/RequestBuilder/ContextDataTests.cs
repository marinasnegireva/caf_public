using NUnit.Framework;

namespace Tests.UnitTests.RequestBuilder;

[TestFixture]
public class ContextDataTests : ConversationRequestBuilderTestBase
{
    #region Display Type Tests

    [TestCase(RetrievalType.Content, "Full content", true)]
    [TestCase(RetrievalType.Summary, "Summary content", true)]
    [TestCase(RetrievalType.CoreFacts, "Core facts content", true)]
    public async Task BuildContextData_WithDisplayType_UsesCorrectContent(
        RetrievalType displayType, string expectedContent, bool shouldContain)
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };

        context.Memories.Add(new ContextData
        {
            Id = 1,
            Type = DataType.Memory,
            Name = "Test Memory",
            Content = "Full content",
            Summary = "Summary content",
            CoreFacts = "Core facts content",
            Display = displayType,
            IsEnabled = true,
            SortOrder = 1
        });

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        Assert.That(userMessages.Any(m => m.Parts.Any(p => p.Text!.Contains(expectedContent))), Is.EqualTo(shouldContain));
    }

    #endregion

    #region Context Data Ordering Tests

    [Test]
    public async Task BuildGeminiRequestAsync_WithUserProfile_LoadsFirstBeforeAllOtherContextData()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };

        context.UserProfile = CreateContextData(DataType.CharacterProfile, "USER PROFILE CONTENT", "user_character", isUser: true);
        context.Data.Add(CreateContextData(DataType.Generic, "Generic content", "generic_data"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "Other character", "other_char"));
        context.Memories.Add(CreateContextData(DataType.Memory, "Memory content"));
        context.Insights.Add(CreateContextData(DataType.Insight, "Insight content"));
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice"));
        context.Quotes.Add(CreateContextData(DataType.Quote, "Quote"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var contextDataMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("[meta]"))).ToList();

        Assert.That(contextDataMessages, Is.Not.Empty, "Should have context data messages");

        var firstContextMessage = contextDataMessages.First();
        Assert.That(firstContextMessage.Parts[0].Text, Does.StartWith("`[meta] user_character`"), "User profile should be FIRST");
        Assert.That(firstContextMessage.Parts[0].Text, Does.Contain("USER PROFILE CONTENT"), "First message should contain user profile content");

        var userProfileIndex = contextDataMessages.FindIndex(m => m.Parts.Any(p => p.Text!.Contains("USER PROFILE CONTENT")));
        var genericIndex = contextDataMessages.FindIndex(m => m.Parts.Any(p => p.Text!.Contains("Generic content")));
        var otherCharIndex = contextDataMessages.FindIndex(m => m.Parts.Any(p => p.Text!.Contains("Other character")));
        var memoryIndex = contextDataMessages.FindIndex(m => m.Parts.Any(p => p.Text!.Contains("Memory content")));

        Assert.Multiple(() =>
        {
            Assert.That(userProfileIndex, Is.EqualTo(0), "User profile should be at index 0 (first)");
            Assert.That(genericIndex, Is.GreaterThan(userProfileIndex), "Generic data should come after user profile");
            Assert.That(otherCharIndex, Is.GreaterThan(userProfileIndex), "Other characters should come after user profile");
            Assert.That(memoryIndex, Is.GreaterThan(userProfileIndex), "Memories should come after user profile");
        });
    }

    [Test]
    public async Task BuildGeminiRequestAsync_ContextDataOrdering_UserProfileThenGenericThenCharacterProfiles()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };

        context.UserProfile = CreateContextData(DataType.CharacterProfile, "USER_PROFILE_MARKER", "user_profile", isUser: true);
        context.Data.Add(CreateContextData(DataType.Generic, "GENERIC1_MARKER", "generic1"));
        context.Data.Add(CreateContextData(DataType.Generic, "GENERIC2_MARKER", "generic2"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "CHAR1_MARKER", "char1"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "CHAR2_MARKER", "char2"));
        context.Memories.Add(CreateContextData(DataType.Memory, "MEMORY_MARKER"));
        context.Insights.Add(CreateContextData(DataType.Insight, "INSIGHT_MARKER"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();

        int FindMessageIndex(string marker)
        {
            for (var i = 0; i < userMessages.Count; i++)
            {
                if (userMessages[i].Parts.Any(p => p.Text!.Contains(marker)))
                    return i;
            }
            return -1;
        }

        var userProfileIdx = FindMessageIndex("USER_PROFILE_MARKER");
        var generic1Idx = FindMessageIndex("GENERIC1_MARKER");
        var generic2Idx = FindMessageIndex("GENERIC2_MARKER");
        var char1Idx = FindMessageIndex("CHAR1_MARKER");
        var char2Idx = FindMessageIndex("CHAR2_MARKER");
        var memoryIdx = FindMessageIndex("MEMORY_MARKER");
        var insightIdx = FindMessageIndex("INSIGHT_MARKER");

        Assert.Multiple(() =>
        {
            Assert.That(userProfileIdx, Is.GreaterThanOrEqualTo(0), "User profile should be found");
            Assert.That(generic1Idx, Is.GreaterThanOrEqualTo(0), "Generic1 should be found");
            Assert.That(generic2Idx, Is.GreaterThanOrEqualTo(0), "Generic2 should be found");
            Assert.That(char1Idx, Is.GreaterThanOrEqualTo(0), "Char1 should be found");
            Assert.That(char2Idx, Is.GreaterThanOrEqualTo(0), "Char2 should be found");
            Assert.That(memoryIdx, Is.GreaterThanOrEqualTo(0), "Memory should be found");
            Assert.That(insightIdx, Is.GreaterThanOrEqualTo(0), "Insight should be found");
        });

        Assert.That(userProfileIdx, Is.LessThan(generic1Idx), "User profile must come before Generic1");
        Assert.Multiple(() =>
        {
            Assert.That(userProfileIdx, Is.LessThan(generic2Idx), "User profile must come before Generic2");

            Assert.That(generic1Idx, Is.LessThan(char1Idx), "Generic1 must come before Char1");
        });
        Assert.Multiple(() =>
        {
            Assert.That(generic1Idx, Is.LessThan(char2Idx), "Generic1 must come before Char2");
            Assert.That(generic2Idx, Is.LessThan(char1Idx), "Generic2 must come before Char1");
        });
        Assert.Multiple(() =>
        {
            Assert.That(generic2Idx, Is.LessThan(char2Idx), "Generic2 must come before Char2");

            Assert.That(char1Idx, Is.LessThan(memoryIdx), "Char1 must come before Memory");
        });
        Assert.Multiple(() =>
        {
            Assert.That(char1Idx, Is.LessThan(insightIdx), "Char1 must come before Insight");
            Assert.That(char2Idx, Is.LessThan(memoryIdx), "Char2 must come before Memory");
        });
        Assert.That(char2Idx, Is.LessThan(insightIdx), "Char2 must come before Insight");
    }

    #endregion
}
