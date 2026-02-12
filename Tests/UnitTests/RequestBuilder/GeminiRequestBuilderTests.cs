namespace Tests.UnitTests.RequestBuilder;

[TestFixture]
public class GeminiRequestBuilderTests : ConversationRequestBuilderTestBase
{
    [Test]
    public async Task BuildGeminiRequestAsync_WithNullContext_ThrowsArgumentNullException()
    {
        var ex = Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Builder.BuildGeminiRequestAsync(null!));
        Assert.That(ex!.ParamName, Is.EqualTo("context"));
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithMinimalContext_BuildsBasicRequest()
    {
        var context = CreateMinimalContext();

        var result = await Builder.BuildGeminiRequestAsync(context);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Contents, Is.Not.Empty);
            Assert.That(context.GeminiRequest, Is.EqualTo(result));
        });
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithSystemInstruction_IncludesPersonaContent()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage
        {
            Id = 1,
            Name = "Test Persona",
            Content = "You are a helpful assistant."
        };

        var result = await Builder.BuildGeminiRequestAsync(context);

        Assert.That(result.SystemInstruction, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.SystemInstruction!.Parts, Has.Count.EqualTo(1));
            Assert.That(result.SystemInstruction.Parts[0].Text, Is.EqualTo("You are a helpful assistant."));
        });
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithMemories_AddsMemoryContent()
    {
        var context = CreateMinimalContext();
        context.Memories.Add(CreateContextData(DataType.Memory, "Memory content", "Memory1"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        AssertGeminiUserMessageContains(result, "Memory content");
        AssertGeminiUserMessageContains(result, "`[meta] memories`");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithQuotes_AddsContextDataSection()
    {
        var context = CreateMinimalContext();
        context.Quotes.Add(CreateContextData(DataType.Quote, "Quote content 1"));
        context.Quotes.Add(CreateContextData(DataType.Quote, "Quote content 2"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        AssertGeminiUserMessageContains(result, "`[meta] quotes`");
        AssertGeminiUserMessageContains(result, "Quote content 1");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithVoiceSamples_AddsContextDataSection()
    {
        var context = CreateMinimalContext();
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice sample 1"));
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice sample 2"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        AssertGeminiUserMessageContains(result, "`[meta] voice sample`");
        AssertGeminiUserMessageContains(result, "Voice sample 1");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithDialogueLog_AddsHistoryNote()
    {
        var context = CreateMinimalContext();
        context.DialogueLog = "Past conversation history...";

        var result = await Builder.BuildGeminiRequestAsync(context);

        AssertGeminiUserMessageContains(result, "Past conversation history");
        AssertGeminiModelMessageContains(result, "History noted.");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithRecentTurns_AddsConversationHistory()
    {
        var context = CreateMinimalContext();
        context.RecentTurns =
        [
            new Turn { Input = "User message 1", Response = "Bot response 1", JsonInput = "User message 1" },
            new Turn { Input = "User message 2", Response = "Bot response 2", JsonInput = "User message 2" }
        ];

        var result = await Builder.BuildGeminiRequestAsync(context);

        AssertGeminiUserMessageContains(result, "User message 1");
        AssertGeminiUserMessageContains(result, "User message 2");
        AssertGeminiModelMessageContains(result, "Bot response 1");
        AssertGeminiModelMessageContains(result, "Bot response 2");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithSafetySettings_AppliesSafetyConfiguration()
    {
        GeminiOptions.SafetySettings =
        [
            new SafetySettingOptions { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_NONE" }
        ];
        var context = CreateMinimalContext();

        var result = await Builder.BuildGeminiRequestAsync(context);

        Assert.That(result.SafetySettings, Is.Not.Null);
        Assert.That(result.SafetySettings, Has.Count.EqualTo(1));
        Assert.That(result.SafetySettings![0].Category, Is.EqualTo("HARM_CATEGORY_HARASSMENT"));
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithUserProfile_AddsUserProfileFirst()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };
        context.UserProfile = CreateContextData(
            DataType.CharacterProfile,
            "User character details",
            "User Profile",
            isUser: true);

        var result = await Builder.BuildGeminiRequestAsync(context);

        AssertGeminiUserMessageContains(result, "User character details");
        AssertGeminiUserMessageContains(result, "`[meta] user profile`");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_WithFullContext_ConstructsCorrectMessageOrder()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Arden", Content = "You are Arden." };
        context.UserName = "Sandro";
        context.CurrentTurn = new Turn { Input = "Hello!" };

        context.UserProfile = CreateContextData(DataType.CharacterProfile, "User is Sandro", "Sandro Profile", isUser: true);
        context.Data.Add(CreateContextData(DataType.Generic, "Generic reference", "Generic"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "Alice profile", "Alice"));
        context.Memories.Add(CreateContextData(DataType.Memory, "Memory content", "Memory"));
        context.Insights.Add(CreateContextData(DataType.Insight, "Insight content", "Insight"));
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice sample"));
        context.Quotes.Add(CreateContextData(DataType.Quote, "Quote content"));
        context.DialogueLog = "Older conversation history...";
        context.RecentTurns.Add(new Turn { Input = "Previous user input", Response = "Previous response", JsonInput = "Previous user input" });

        await AddFlagAsync("[test] Test flag");

        var result = await Builder.BuildGeminiRequestAsync(context);

        var allUserText = GetAllGeminiUserText(result);

        Assert.That(allUserText, Does.Contain("User is Sandro"), "User profile should be present");
        Assert.That(allUserText, Does.Contain("Generic reference"), "Generic data should be present");
        Assert.That(allUserText, Does.Contain("Alice profile"), "Character profile should be present");
        Assert.That(allUserText, Does.Contain("Memory content"), "Memory should be present");
        Assert.That(allUserText, Does.Contain("Insight content"), "Insight should be present");
        Assert.That(allUserText, Does.Contain("Voice sample"), "Voice sample should be present");
        Assert.That(allUserText, Does.Contain("Quote content"), "Quote should be present");
        Assert.That(allUserText, Does.Contain("Older conversation history"), "Dialogue log should be present");
        Assert.That(allUserText, Does.Contain("Previous user input"), "Recent turns should be present");
        Assert.That(allUserText, Does.Contain("[test] Test flag"), "Flags should be present");
        Assert.That(allUserText, Does.Contain("Hello!"), "Current turn should be present");

        var modelResponses = result.Contents.Where(c => c.Role == "model").ToList();
        Assert.That(modelResponses, Is.Not.Empty, "Should have model acknowledgments");
    }
}
