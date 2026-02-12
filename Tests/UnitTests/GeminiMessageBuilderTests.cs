namespace Tests.UnitTests;

public class GeminiMessageBuilderTests
{
    [Test]
    public void Build_WithSingleUserMessage_CreatesValidRequest()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .AddUserMessage("Hello");

        // Act
        var request = builder.Build();

        // Assert
        Assert.That(request.Contents, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(request.Contents[0].Role, Is.EqualTo("user"));
            Assert.That(request.Contents[0].Parts[0].Text, Is.EqualTo("Hello"));
        });
    }

    [Test]
    public void Build_WithSystemInstruction_IncludesSystemInstruction()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .WithSystemInstruction("You are a helpful assistant")
            .AddUserMessage("Hello");

        // Act
        var request = builder.Build();

        // Assert
        Assert.That(request.SystemInstruction, Is.Not.Null);
        Assert.That(request.SystemInstruction.Parts[0].Text, Is.EqualTo("You are a helpful assistant"));
    }

    [Test]
    public void AddTurn_AddsBothUserAndModelMessages()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .AddTurn("Hello", "Hi there!")
            .AddUserMessage("How are you?");

        // Act
        var request = builder.Build();

        // Assert
        Assert.That(request.Contents, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(request.Contents[0].Role, Is.EqualTo("user"));
            Assert.That(request.Contents[0].Parts[0].Text, Is.EqualTo("Hello"));
            Assert.That(request.Contents[1].Role, Is.EqualTo("model"));
            Assert.That(request.Contents[1].Parts[0].Text, Is.EqualTo("Hi there!"));
            Assert.That(request.Contents[2].Role, Is.EqualTo("user"));
        });
    }

    [Test]
    public void AddHistory_AddsMultipleTurns()
    {
        // Arrange
        var history = new[]
        {
            ("Hello", "Hi!"),
            ("How are you?", "I'm doing well!"),
            ("What's your name?", "I'm Gemini")
        };

        var builder = GeminiMessageBuilder.Create()
            .AddHistory(history)
            .AddUserMessage("Nice to meet you");

        // Act
        var request = builder.Build();

        // Assert
        Assert.That(request.Contents, Has.Count.EqualTo(7)); // 3 turns (6 messages) + 1 final user message
        Assert.That(request.Contents[^1].Parts[0].Text, Is.EqualTo("Nice to meet you"));
    }

    [Test]
    public void WithGenerationConfig_SetsConfiguration()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .AddUserMessage("Hello")
            .WithGenerationConfig(
                maxOutputTokens: 1024,
                temperature: 0.5f,
                responseMimeType: "application/json");

        // Act
        var request = builder.Build();

        // Assert
        Assert.That(request.GenerationConfig, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(request.GenerationConfig.MaxOutputTokens, Is.EqualTo(1024));
            Assert.That(request.GenerationConfig.Temperature, Is.EqualTo(0.5));
            Assert.That(request.GenerationConfig.ResponseMimeType, Is.EqualTo("application/json"));
        });
    }

    [Test]
    public void Build_WithNoMessages_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void Build_EndingWithModelMessage_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .AddUserMessage("Hello")
            .AddModelResponse("Hi there!");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void Clear_RemovesAllMessagesAndConfig()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .WithSystemInstruction("Test")
            .AddUserMessage("Hello")
            .WithGenerationConfig(maxOutputTokens: 100);

        // Act
        builder.Clear();

        // Assert
        Assert.That(builder.MessageCount, Is.EqualTo(0));
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void MessageCount_ReturnsCorrectCount()
    {
        // Arrange
        var builder = GeminiMessageBuilder.Create()
            .AddUserMessage("Hello")
            .AddModelResponse("Hi")
            .AddUserMessage("How are you?");

        // Assert
        Assert.That(builder.MessageCount, Is.EqualTo(3));
    }

    [Test]
    public void FluentAPI_ChainsCorrectly()
    {
        // Act
        var request = GeminiMessageBuilder.Create()
            .WithSystemInstruction("You are helpful")
            .AddUserMessage("First")
            .AddModelResponse("Response")
            .AddUserMessage("Second")
            .WithGenerationConfig(temperature: 0.7f)
            .Build();

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(request.Contents, Has.Count.EqualTo(3));
            Assert.That(request.SystemInstruction, Is.Not.Null);
            Assert.That(request.GenerationConfig, Is.Not.Null);
        });
        Assert.That(request.GenerationConfig.Temperature, Is.EqualTo(0.7f));
    }
}