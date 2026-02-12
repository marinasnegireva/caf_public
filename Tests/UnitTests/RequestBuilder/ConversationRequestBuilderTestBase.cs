using CAF.Interfaces;

namespace Tests.UnitTests.RequestBuilder;

/// <summary>
/// Base class for ConversationRequestBuilder tests providing shared setup and helper methods
/// </summary>
public abstract class ConversationRequestBuilderTestBase
{
    protected GeneralDbContext Context = null!;
    protected ConversationRequestBuilder Builder = null!;
    protected GeminiOptions GeminiOptions = null!;
    protected ClaudeOptions ClaudeOptions = null!;

    [SetUp]
    public void BaseSetup()
    {
        var options = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        Context = new GeneralDbContext(options);

        GeminiOptions = new GeminiOptions
        {
            ApiKey = "test-key",
            Model = "gemini-2.0-flash-exp",
            SafetySettings = []
        };

        ClaudeOptions = new ClaudeOptions
        {
            ApiKey = "test-key",
            Model = "claude-3-5-sonnet-20241022",
            MaxTokens = 8192,
            Temperature = 1.0,
            EnableThinking = false,
            EnablePromptCaching = false
        };

        var mockSettingService = new Mock<ISettingService>();
        mockSettingService
            .Setup(s => s.GetByNameAsync(It.IsAny<SettingsKeys>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Setting?)null);

        Builder = new ConversationRequestBuilder(
            Context,
            Options.Create(GeminiOptions),
            Options.Create(ClaudeOptions),
            mockSettingService.Object,
            new Mock<ILogger<ConversationRequestBuilder>>().Object);
    }

    [TearDown]
    public void BaseTearDown()
    {
        Context?.Dispose();
    }

    #region Helper Methods

    private static int _nextContextDataId = 1;

    protected static ConversationState CreateMinimalContext()
    {
        return new ConversationState
        {
            CurrentTurn = new Turn
            {
                Input = "Test input"
            },
            Perceptions = []
        };
    }

    protected static ContextData CreateContextData(
        DataType type,
        string content,
        string? name = null,
        bool isUser = false,
        int sortOrder = 0)
    {
        return new ContextData
        {
            Id = Interlocked.Increment(ref _nextContextDataId),
            Type = type,
            Content = content,
            Name = name ?? $"{type} Item",
            Display = RetrievalType.Content,
            IsEnabled = true,
            IsUser = isUser,
            SortOrder = sortOrder,
            Speaker = type is DataType.Quote or DataType.PersonaVoiceSample ? "TestSpeaker" : null
        };
    }

    protected async Task AddFlagAsync(string value, bool active = true, bool constant = false)
    {
        await Context.Flags.AddAsync(new Flag
        {
            Value = value,
            Active = active,
            Constant = constant
        });
        await Context.SaveChangesAsync();
    }

    #endregion

    #region Gemini Assertion Helpers

    protected static void AssertGeminiUserMessageContains(GeminiRequest result, string text)
    {
        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        Assert.That(userMessages.Any(m => m.Parts.Any(p => p.Text!.Contains(text))), Is.True,
            $"Expected user message to contain '{text}'");
    }

    protected static void AssertGeminiModelMessageContains(GeminiRequest result, string text)
    {
        var modelMessages = result.Contents.Where(c => c.Role == "model").ToList();
        Assert.That(modelMessages.Any(m => m.Parts.Any(p => p.Text!.Contains(text))), Is.True,
            $"Expected model message to contain '{text}'");
    }

    protected static string GetGeminiLastUserMessageText(GeminiRequest result)
    {
        var lastUserMessage = result.Contents.Last(c => c.Role == "user");
        return lastUserMessage.Parts[0].Text!;
    }

    protected static string GetAllGeminiUserText(GeminiRequest result)
    {
        return string.Join(" ", result.Contents
            .Where(c => c.Role == "user")
            .SelectMany(c => c.Parts)
            .Select(p => p.Text));
    }

    #endregion

    #region Claude Assertion Helpers

    protected static void AssertClaudeUserMessageContains(ClaudeRequest result, string text)
    {
        var userMessages = result.Messages.Where(m => m.Role == "user").ToList();
        var textContents = ExtractClaudeTextContents(userMessages);
        Assert.That(textContents.Any(t => t.Contains(text)), Is.True,
            $"Expected user message to contain '{text}'");
    }

    protected static void AssertClaudeAssistantMessageContains(ClaudeRequest result, string text)
    {
        var assistantMessages = result.Messages.Where(m => m.Role == "assistant").ToList();
        var textContents = ExtractClaudeTextContents(assistantMessages);
        Assert.That(textContents.Any(t => t.Contains(text)), Is.True,
            $"Expected assistant message to contain '{text}'");
    }

    protected static string GetAllClaudeUserText(ClaudeRequest result)
    {
        var userMessages = result.Messages.Where(m => m.Role == "user").ToList();
        return string.Join(" ", ExtractClaudeTextContents(userMessages));
    }

    private static List<string> ExtractClaudeTextContents(List<ClaudeMessage> messages)
    {
        var textContents = new List<string>();
        foreach (var msg in messages)
        {
            if (msg.Content is string str)
                textContents.Add(str);
            else if (msg.Content is List<ClaudeTextContent> list)
                textContents.AddRange(list.Select(c => c.Text ?? string.Empty));
        }
        return textContents;
    }

    #endregion
}
