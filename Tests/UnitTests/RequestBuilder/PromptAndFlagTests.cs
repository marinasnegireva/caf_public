using static CAF.Services.Conversation.Enrichment.Enrichers.PerceptionEnricher;

namespace Tests.UnitTests.RequestBuilder;

[TestFixture]
public class PromptAndFlagTests : ConversationRequestBuilderTestBase
{
    #region Prompt Formatting Tests

    [Test]
    public async Task BuildPrompt_WithOOCRequest_FormatsAsOutOfCharacter()
    {
        var context = CreateMinimalContext();
        context.IsOOCRequest = true;
        context.CurrentTurn = new Turn { Input = "What's your model?" };

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("[ooc]"));
        Assert.That(text, Does.Contain("out-of-character"));
    }

    [Test]
    public async Task BuildPrompt_WithUserName_FormatsInputWithUserName()
    {
        var context = CreateMinimalContext();
        context.UserName = "Alice";
        context.CurrentTurn = new Turn { Input = "Hello!" };

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("A:"));
        Assert.That(text, Does.Contain("Hello!"));
    }

    [Test]
    public async Task BuildPrompt_WithoutUserName_UsesPlainInput()
    {
        var context = CreateMinimalContext();
        context.UserName = null!;
        context.CurrentTurn = new Turn { Input = "Hello!" };

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("Hello!"));
    }

    [Test]
    public async Task BuildPrompt_WithFlags_IncludesFlagsInPrompt()
    {
        var context = CreateMinimalContext();
        context.CurrentTurn = new Turn { Input = "Test" };

        await AddFlagAsync("[test] Test flag");

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("Flags:"));
        Assert.That(text, Does.Contain("[test] Test flag"));
    }

    #endregion

    #region Flag Building Tests

    [Test]
    public async Task BuildFlags_WithComplaintPerception_AddsExplorationFlag()
    {
        var context = CreateMinimalContext();
        context.UserName = "Bob";
        context.Perceptions =
        [
            new PerceptionRecord { Property = "understanding.complaint:true" }
        ];

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("[direction] Exploration: You made a mistake about Bob"));
    }

    [Test]
    public async Task BuildFlags_WithExplorationDesire_AddsTopicExplorationFlag()
    {
        var context = CreateMinimalContext();
        context.Perceptions =
        [
            new PerceptionRecord { Property = "exploration.desire:true" },
            new PerceptionRecord { Property = "exploration.topic:science" }
        ];

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("[direction] Explore ideas on topics: science"));
    }

    [Test]
    public async Task BuildFlags_WithActiveDbFlags_IncludesAndDeactivatesThem()
    {
        var context = CreateMinimalContext();

        await AddFlagAsync("[active] Active flag");

        await Builder.BuildGeminiRequestAsync(context);

        var flag = await Context.Flags.FirstAsync();
        Assert.Multiple(() =>
        {
            Assert.That(flag.Active, Is.False);
            Assert.That(flag.LastUsedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task BuildFlags_WithConstantDbFlags_IncludesButKeepsInactive()
    {
        var context = CreateMinimalContext();

        await AddFlagAsync("[constant] Constant flag", active: false, constant: true);

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        Assert.That(text, Does.Contain("[constant] Constant flag"));

        var flag = await Context.Flags.FirstAsync();
        Assert.Multiple(() =>
        {
            Assert.That(flag.Active, Is.False);
            Assert.That(flag.LastUsedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task BuildFlags_WithDuplicateFlags_ReturnsDeduplicated()
    {
        var context = CreateMinimalContext();

        await Context.Flags.AddAsync(new Flag { Value = "[test] Same flag", Active = true });
        await Context.Flags.AddAsync(new Flag { Value = "[test] Same flag", Constant = true, Active = false });
        await Context.SaveChangesAsync();

        var result = await Builder.BuildGeminiRequestAsync(context);

        var text = GetGeminiLastUserMessageText(result);
        var flagCount = text.Split("[test] Same flag").Length - 1;
        Assert.That(flagCount, Is.EqualTo(1), "Should appear only once due to Distinct()");
    }

    #endregion
}
