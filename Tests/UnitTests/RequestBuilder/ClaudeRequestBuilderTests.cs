namespace Tests.UnitTests.RequestBuilder;

[TestFixture]
public class ClaudeRequestBuilderTests : ConversationRequestBuilderTestBase
{
    [Test]
    public async Task BuildClaudeRequestAsync_WithNullContext_ThrowsArgumentNullException()
    {
        var ex = Assert.ThrowsAsync<ArgumentNullException>(
            async () => await Builder.BuildClaudeRequestAsync(null!));
        Assert.That(ex!.ParamName, Is.EqualTo("context"));
    }

    [Test]
    public async Task BuildClaudeRequestAsync_WithMinimalContext_BuildsBasicRequest()
    {
        var context = CreateMinimalContext();

        var result = await Builder.BuildClaudeRequestAsync(context);

        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result.Messages, Is.Not.Empty);
            Assert.That(result.Model, Is.EqualTo(ClaudeOptions.Model));
            Assert.That(result.MaxTokens, Is.EqualTo(ClaudeOptions.MaxTokens));
            Assert.That(context.ClaudeRequest, Is.EqualTo(result));
        });
    }

    [Test]
    public async Task BuildClaudeRequestAsync_WithSystemInstruction_IncludesPersonaContent()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage
        {
            Id = 1,
            Name = "Test Persona",
            Content = "You are a helpful assistant."
        };

        var result = await Builder.BuildClaudeRequestAsync(context);

        Assert.That(result.System, Is.Not.Null);
        if (result.System is string systemString)
        {
            Assert.That(systemString, Is.EqualTo("You are a helpful assistant."));
        }
        else if (result.System is List<ClaudeSystemBlock> systemList)
        {
            Assert.That(systemList, Has.Count.EqualTo(1));
            Assert.That(systemList[0].Text, Is.EqualTo("You are a helpful assistant."));
        }
        else
        {
            Assert.Fail("System should be either string or List<ClaudeSystemBlock>");
        }
    }

    [Test]
    public async Task BuildClaudeRequestAsync_WithThinkingEnabled_ConfiguresThinking()
    {
        ClaudeOptions.EnableThinking = true;
        ClaudeOptions.ThinkingBudgetTokens = 5000;
        var context = CreateMinimalContext();

        var result = await Builder.BuildClaudeRequestAsync(context);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.MaxTokens, Is.EqualTo(ClaudeOptions.MaxTokens));
    }

    [Test]
    public async Task BuildClaudeRequestAsync_WithContextData_AddsContextDataSection()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };
        context.Memories.Add(CreateContextData(DataType.Memory, "This is a memory", "Test Memory", sortOrder: 1));

        var result = await Builder.BuildClaudeRequestAsync(context);

        AssertClaudeUserMessageContains(result, "`[meta] memories`");
    }

    [Test]
    public async Task BuildClaudeRequestAsync_WithRecentTurns_AddsConversationHistory()
    {
        var context = CreateMinimalContext();
        context.RecentTurns =
        [
            new Turn { Input = "User message", Response = "Assistant response", JsonInput = "User message" }
        ];

        var result = await Builder.BuildClaudeRequestAsync(context);

        AssertClaudeUserMessageContains(result, "User message");
        AssertClaudeAssistantMessageContains(result, "Assistant response");
    }

    [Test]
    public async Task BuildClaudeRequestAsync_WithFullContext_ConstructsCorrectMessageOrder()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Arden", Content = "You are Arden." };
        context.UserName = "Sandro";
        context.CurrentTurn = new Turn { Input = "Hello!" };

        context.UserProfile = CreateContextData(DataType.CharacterProfile, "User is Sandro", "Sandro Profile", isUser: true);
        context.Memories.Add(CreateContextData(DataType.Memory, "Memory content", "Memory"));
        context.Insights.Add(CreateContextData(DataType.Insight, "Insight content", "Insight"));
        context.DialogueLog = "Older conversation history...";
        context.RecentTurns.Add(new Turn { Input = "Previous input", Response = "Previous response", JsonInput = "Previous input" });

        await AddFlagAsync("[test] Test flag");

        var result = await Builder.BuildClaudeRequestAsync(context);

        var combinedUserText = GetAllClaudeUserText(result);

        Assert.That(combinedUserText, Does.Contain("User is Sandro"), "User profile should be present");
        Assert.That(combinedUserText, Does.Contain("Memory content"), "Memory should be present");
        Assert.That(combinedUserText, Does.Contain("Insight content"), "Insight should be present");
        Assert.That(combinedUserText, Does.Contain("Older conversation history"), "Dialogue log should be present");
        Assert.That(combinedUserText, Does.Contain("Previous input"), "Recent turns should be present");
        Assert.That(combinedUserText, Does.Contain("[test] Test flag"), "Flags should be present");
        Assert.That(combinedUserText, Does.Contain("Hello!"), "Current turn should be present");

        var assistantMessages = result.Messages.Where(m => m.Role == "assistant").ToList();
        Assert.That(assistantMessages, Is.Not.Empty, "Should have assistant acknowledgments");
    }
}
