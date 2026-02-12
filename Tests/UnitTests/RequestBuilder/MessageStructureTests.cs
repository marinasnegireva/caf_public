namespace Tests.UnitTests.RequestBuilder;

[TestFixture]
public class MessageStructureTests : ConversationRequestBuilderTestBase
{
    #region Header Format Tests - Grouped Types

    [TestCase(DataType.Memory, "`[meta] memories`", "Memory 1", "Memory 2")]
    [TestCase(DataType.Insight, "`[meta] insights`", "Insight 1", "Insight 2")]
    [TestCase(DataType.Quote, "`[meta] quotes`", "Quote 1", "Quote 2")]
    public async Task BuildGeminiRequestAsync_VerifiesGroupedHeaderFormat(
        DataType dataType, string expectedHeader, string content1, string content2)
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };

        // Add items directly to the appropriate collection
        switch (dataType)
        {
            case DataType.Memory:
                context.Memories.Add(CreateContextData(dataType, content1));
                context.Memories.Add(CreateContextData(dataType, content2));
                break;
            case DataType.Insight:
                context.Insights.Add(CreateContextData(dataType, content1));
                context.Insights.Add(CreateContextData(dataType, content2));
                break;
            case DataType.Quote:
                context.Quotes.Add(CreateContextData(dataType, content1));
                context.Quotes.Add(CreateContextData(dataType, content2));
                break;
            case DataType.PersonaVoiceSample:
                context.PersonaVoiceSamples.Add(CreateContextData(dataType, content1));
                context.PersonaVoiceSamples.Add(CreateContextData(dataType, content2));
                break;
        }

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var matchingMessages = userMessages.Where(m => m.Parts.Any(p =>
            p.Text!.Contains(content1) || p.Text!.Contains(content2))).ToList();

        Assert.That(matchingMessages, Has.Count.EqualTo(1), $"All {dataType}s should be in ONE message");
        Assert.That(matchingMessages[0].Parts[0].Text, Does.StartWith(expectedHeader), $"Header should be '{expectedHeader}'");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_VoiceSamples_UseSingularHeader()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice 1"));
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice 2"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var voiceMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("Voice 1") || p.Text!.Contains("Voice 2"))).ToList();

        Assert.That(voiceMessages, Has.Count.EqualTo(1), "All voice samples should be in ONE message");
        Assert.That(voiceMessages[0].Parts[0].Text, Does.StartWith("`[meta] voice sample`"), "Header should be '[meta] voice sample' (singular)");
    }

    #endregion

    #region Header Format Tests - Individual Types

    [Test]
    public async Task BuildGeminiRequestAsync_GenericData_EachInSeparateMessage()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };
        context.Data.Add(CreateContextData(DataType.Generic, "Chronicle content", "chronicle"));
        context.Data.Add(CreateContextData(DataType.Generic, "Lexicon content", "lexicon"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var chronicleMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("Chronicle content"))).ToList();
        var lexiconMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("Lexicon content"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(chronicleMessages, Has.Count.EqualTo(1), "Chronicle should be in its own message");
            Assert.That(lexiconMessages, Has.Count.EqualTo(1), "Lexicon should be in its own message");
        });

        Assert.Multiple(() =>
        {
            Assert.That(chronicleMessages[0].Parts[0].Text, Does.StartWith("`[meta] chronicle`"), "Header should use item name");
            Assert.That(lexiconMessages[0].Parts[0].Text, Does.StartWith("`[meta] lexicon`"), "Header should use item name");
        });

        var combinedMessages = userMessages.Where(m =>
            m.Parts.Any(p => p.Text!.Contains("Chronicle content") && p.Text!.Contains("Lexicon content"))).ToList();
        Assert.That(combinedMessages, Is.Empty, "Generic items should NOT be combined in one message");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_CharacterProfiles_EachInSeparateMessage()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "Sandro profile", "sandro"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "Arden profile", "arden"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var sandroMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("Sandro profile"))).ToList();
        var ardenMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("Arden profile"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(sandroMessages, Has.Count.EqualTo(1), "Sandro should be in his own message");
            Assert.That(ardenMessages, Has.Count.EqualTo(1), "Arden should be in her own message");
        });

        Assert.Multiple(() =>
        {
            Assert.That(sandroMessages[0].Parts[0].Text, Does.StartWith("`[meta] sandro`"), "Header should use character name");
            Assert.That(ardenMessages[0].Parts[0].Text, Does.StartWith("`[meta] arden`"), "Header should use character name");
        });

        var combinedMessages = userMessages.Where(m =>
            m.Parts.Any(p => p.Text!.Contains("Sandro profile") && p.Text!.Contains("Arden profile"))).ToList();
        Assert.That(combinedMessages, Is.Empty, "Character profiles should NOT be combined in one message");
    }

    [Test]
    public async Task BuildGeminiRequestAsync_UserProfile_HasCorrectHeader()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };
        context.UserProfile = CreateContextData(DataType.CharacterProfile, "User details", "user_profile", isUser: true);

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var profileMessage = userMessages.First(m => m.Parts.Any(p => p.Text!.Contains("User details")));

        Assert.That(profileMessage.Parts[0].Text, Does.StartWith("`[meta] user_profile`"), "Header should use user profile name");
    }

    #endregion

    #region Complete Message Structure Test

    [Test]
    public async Task BuildGeminiRequestAsync_VerifiesCompleteMessageStructure()
    {
        var context = CreateMinimalContext();
        context.Persona = new SystemMessage { Id = 1, Name = "Test", Content = "Test" };

        context.UserProfile = CreateContextData(DataType.CharacterProfile, "User content", "user", isUser: true);
        context.Data.Add(CreateContextData(DataType.Generic, "Chronicle", "chronicle"));
        context.Data.Add(CreateContextData(DataType.Generic, "Lexicon", "lexicon"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "Sandro", "sandro"));
        context.CharacterProfiles.Add(CreateContextData(DataType.CharacterProfile, "Arden", "arden"));
        context.Memories.Add(CreateContextData(DataType.Memory, "Memory1"));
        context.Memories.Add(CreateContextData(DataType.Memory, "Memory2"));
        context.Insights.Add(CreateContextData(DataType.Insight, "Insight1"));
        context.Insights.Add(CreateContextData(DataType.Insight, "Insight2"));
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice1"));
        context.PersonaVoiceSamples.Add(CreateContextData(DataType.PersonaVoiceSample, "Voice2"));
        context.Quotes.Add(CreateContextData(DataType.Quote, "Quote1"));
        context.Quotes.Add(CreateContextData(DataType.Quote, "Quote2"));

        var result = await Builder.BuildGeminiRequestAsync(context);

        var userMessages = result.Contents.Where(c => c.Role == "user").ToList();
        var allText = string.Join("\n", userMessages.SelectMany(m => m.Parts.Select(p => p.Text)));

        Assert.That(allText, Does.Contain("`[meta] user`"), "Should have user profile header");
        Assert.That(allText, Does.Contain("`[meta] chronicle`"), "Should have chronicle header");
        Assert.That(allText, Does.Contain("`[meta] lexicon`"), "Should have lexicon header");
        Assert.That(allText, Does.Contain("`[meta] sandro`"), "Should have sandro header");
        Assert.That(allText, Does.Contain("`[meta] arden`"), "Should have arden header");
        Assert.That(allText, Does.Contain("`[meta] memories`"), "Should have memories header (plural)");
        Assert.That(allText, Does.Contain("`[meta] insights`"), "Should have insights header (plural)");
        Assert.That(allText, Does.Contain("`[meta] voice sample`"), "Should have voice sample header (singular with space)");
        Assert.That(allText, Does.Contain("`[meta] quotes`"), "Should have quotes header (plural)");

        var contextDataMessages = userMessages.Where(m => m.Parts.Any(p => p.Text!.Contains("[meta]"))).ToList();
        Assert.That(contextDataMessages, Has.Count.GreaterThanOrEqualTo(9), "Should have at least 9 context data messages");
    }

    #endregion
}
