namespace CAF.Tests.UnitTests;

[TestFixture]
public class ClaudeMessageBuilderTests
{
    [Test]
    public void Build_WithMinimalConfiguration_CreatesValidRequest()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .Build("claude-sonnet-4-5");

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(request.Model, Is.EqualTo("claude-sonnet-4-5"));
            Assert.That(request.Messages, Has.Count.EqualTo(1));
        });
        Assert.Multiple(() =>
        {
            Assert.That(request.Messages[0].Role, Is.EqualTo("user"));
            Assert.That(request.Messages[0].Content, Is.EqualTo("Hello"));
        });
    }

    [Test]
    public void Build_WithSystemMessage_IncludesSystem()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .WithSystem("You are a helpful assistant")
            .AddUserMessage("Hello")
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.System, Is.EqualTo("You are a helpful assistant"));
    }

    [Test]
    public void Build_WithMultipleMessages_MaintainsOrder()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("First")
            .AddAssistantMessage("Second")
            .AddUserMessage("Third")
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.Messages, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(request.Messages[0].Content, Is.EqualTo("First"));
            Assert.That(request.Messages[1].Content, Is.EqualTo("Second"));
            Assert.That(request.Messages[2].Content, Is.EqualTo("Third"));
        });
    }

    [Test]
    public void AddTurn_AddsUserAndAssistantMessages()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddTurn("User input", "Assistant response")
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.Messages, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(request.Messages[0].Role, Is.EqualTo("user"));
            Assert.That(request.Messages[0].Content, Is.EqualTo("User input"));
            Assert.That(request.Messages[1].Role, Is.EqualTo("assistant"));
            Assert.That(request.Messages[1].Content, Is.EqualTo("Assistant response"));
        });
    }

    [Test]
    public void AddHistory_AddsMultipleTurns()
    {
        // Arrange
        var history = new[]
        {
            ("First user", "First assistant"),
            ("Second user", "Second assistant")
        };

        // Act
        var request = ClaudeMessageBuilder.Create()
            .AddHistory(history)
            .AddUserMessage("Current message")
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.Messages, Has.Count.EqualTo(5)); // 2 turns (4 messages) + 1 current
        Assert.Multiple(() =>
        {
            Assert.That(request.Messages[0].Content, Is.EqualTo("First user"));
            Assert.That(request.Messages[1].Content, Is.EqualTo("First assistant"));
            Assert.That(request.Messages[2].Content, Is.EqualTo("Second user"));
            Assert.That(request.Messages[3].Content, Is.EqualTo("Second assistant"));
            Assert.That(request.Messages[4].Content, Is.EqualTo("Current message"));
        });
    }

    [Test]
    public void WithCacheBreakpointOnLastMessage_AppliesCachingToLastMessage()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("First message")
            .AddAssistantMessage("First response")
            .AddUserMessage("Last message")
            .WithCacheBreakpointOnLastMessage()
            .Build("claude-sonnet-4-5");

        // Assert - Last message should have cache control
        var lastMessage = request.Messages[2];
        Assert.That(lastMessage.Content, Is.TypeOf<List<ClaudeContentBlock>>());

        var contentBlocks = (List<ClaudeContentBlock>)lastMessage.Content;
        Assert.That(contentBlocks, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(contentBlocks[0].Text, Is.EqualTo("Last message"));
            Assert.That(contentBlocks[0].CacheControl, Is.Not.Null);
        });
        Assert.That(contentBlocks[0].CacheControl.Type, Is.EqualTo("ephemeral"));
    }

    [Test]
    public void WithCacheBreakpointOnLastMessage_WithNoMessages_ThrowsException()
    {
        // Arrange
        var builder = ClaudeMessageBuilder.Create();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.WithCacheBreakpointOnLastMessage());

        Assert.That(ex.Message, Does.Contain("no messages have been added"));
    }

    [Test]
    public void WithCacheBreakpointOnLastMessage_WithAlreadyCachedMessage_SkipsCaching()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Message")
            .WithCacheBreakpointOnLastMessage()
            .WithCacheBreakpointOnLastMessage() // Call twice
            .Build("claude-sonnet-4-5");

        // Assert - Should still have one cache control
        var lastMessage = request.Messages[0];
        Assert.That(lastMessage.Content, Is.TypeOf<List<ClaudeContentBlock>>());

        var contentBlocks = (List<ClaudeContentBlock>)lastMessage.Content;
        Assert.That(contentBlocks, Has.Count.EqualTo(1)); // Only one block
    }

    [Test]
    public void Build_WithThinking_IncludesThinkingConfig()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithThinking()
            .Build("claude-opus-4-6");

        // Assert
        Assert.That(request.Thinking, Is.Not.Null);
        Assert.That(request.Thinking.Type, Is.EqualTo("adaptive"));
    }

    [Test]
    public void Build_WithoutThinking_DisablesThinking()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithoutThinking()
            .Build("claude-opus-4-6");

        // Assert
        Assert.That(request.Thinking, Is.Not.Null);
        Assert.That(request.Thinking.Type, Is.EqualTo("disabled"));
    }

    [Test]
    public void Build_WithNoMessages_ThrowsException()
    {
        // Arrange
        var builder = ClaudeMessageBuilder.Create();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Build("claude-sonnet-4-5"));

        Assert.That(ex.Message, Does.Contain("At least one message must be added"));
    }

    [Test]
    public void Build_WithAssistantFirstMessage_ThrowsException()
    {
        // Arrange
        var builder = ClaudeMessageBuilder.Create()
            .AddAssistantMessage("Hello");

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Build("claude-sonnet-4-5"));

        Assert.That(ex.Message, Does.Contain("First message must be from user"));
    }

    [Test]
    public void WithMaxTokens_SetsMaxTokens()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithMaxTokens(4096)
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.MaxTokens, Is.EqualTo(4096));
    }

    [Test]
    public void WithTemperature_SetsTemperature()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithTemperature(0.7)
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.Temperature, Is.EqualTo(0.7));
    }

    [Test]
    public void WithTopP_SetsTopP()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithTopP(0.9)
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.TopP, Is.EqualTo(0.9));
    }

    [Test]
    public void WithTopK_SetsTopK()
    {
        // Arrange & Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithTopK(40)
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.TopK, Is.EqualTo(40));
    }

    [Test]
    public void WithStopSequences_SetsStopSequences()
    {
        // Arrange
        var stopSequences = new List<string> { "STOP", "END" };

        // Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithStopSequences(stopSequences)
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.StopSequences, Is.EqualTo(stopSequences));
    }

    [Test]
    public void WithMetadata_SetsMetadata()
    {
        // Arrange
        var metadata = new ClaudeMetadata { UserId = "user123" };

        // Act
        var request = ClaudeMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithMetadata(metadata)
            .Build("claude-sonnet-4-5");

        // Assert
        Assert.That(request.Metadata, Is.EqualTo(metadata));
        Assert.That(request.Metadata.UserId, Is.EqualTo("user123"));
    }
}