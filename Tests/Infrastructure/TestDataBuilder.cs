namespace Tests.Infrastructure;

/// <summary>
/// Fluent builder for creating test data in ConversationPipeline integration tests.
/// </summary>
public class TestDataBuilder
{
    private readonly GeneralDbContext _db;

    public TestDataBuilder(GeneralDbContext db)
    {
        _db = db;
    }

    #region Profile

    public async Task<Profile> CreateProfileAsync(string name, bool isActive = true)
    {
        var profile = new Profile
        {
            Name = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        _db.Profiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile;
    }

    #endregion Profile

    #region Session

    public async Task<Session> CreateSessionAsync(int profileId, string name, bool isActive = true)
    {
        var session = new Session
        {
            Name = name,
            IsActive = isActive,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    #endregion Session

    #region SystemMessage

    public async Task<SystemMessage> CreateSystemMessageAsync(
        int profileId,
        string name,
        string content,
        SystemMessage.SystemMessageType type,
        bool isActive = true)
    {
        var message = new SystemMessage
        {
            Name = name,
            Content = content,
            Type = type,
            IsActive = isActive,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow
        };
        _db.SystemMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    #endregion SystemMessage

    #region Setting

    public async Task<Setting> CreateSettingAsync(int profileId, string name, string value)
    {
        var setting = new Setting
        {
            Name = name,
            Value = value,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Settings.Add(setting);
        await _db.SaveChangesAsync();
        return setting;
    }

    #endregion Setting

    #region Turn

    public async Task<Turn> CreateTurnAsync(
        int sessionId,
        string input,
        string response,
        bool accepted = true,
        DateTime? createdAt = null)
    {
        var turn = new Turn
        {
            SessionId = sessionId,
            Input = input,
            JsonInput = $"User: {input}",
            Response = response,
            StrippedTurn = response,
            Accepted = accepted,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        _db.Turns.Add(turn);
        await _db.SaveChangesAsync();
        return turn;
    }

    public async Task CreateTurnHistoryAsync(int sessionId, int count, TimeSpan interval)
    {
        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            var turn = new Turn
            {
                SessionId = sessionId,
                Input = $"Question {i + 1}",
                JsonInput = $"User: Question {i + 1}",
                Response = $"TestBot: Response {i + 1}",
                StrippedTurn = $"TestBot: Response {i + 1}",
                Accepted = true,
                CreatedAt = baseTime - (interval * (count - i))
            };
            _db.Turns.Add(turn);
        }
        await _db.SaveChangesAsync();
    }

    #endregion Turn

    #region ContextData

    public async Task<ContextData> CreateContextDataAsync(
        int profileId,
        string name,
        string content,
        DataType type,
        AvailabilityType availability,
        bool isEnabled = true,
        string? triggerKeywords = null,
        int? triggerMinMatchCount = null,
        int? sourceSessionId = null,
        bool isUser = false,
        int sortOrder = 0)
    {
        var data = new ContextData
        {
            Name = name,
            Content = content,
            Type = type,
            Availability = availability,
            IsEnabled = isEnabled,
            TriggerKeywords = triggerKeywords,
            TriggerMinMatchCount = triggerMinMatchCount ?? 1,
            SourceSessionId = sourceSessionId,
            IsUser = isUser,
            SortOrder = sortOrder,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow
        };
        _db.ContextData.Add(data);
        await _db.SaveChangesAsync();
        return data;
    }

    public async Task<ContextData> CreateAlwaysOnMemoryAsync(int profileId, string name, string content, int sortOrder = 0)
    {
        return await CreateContextDataAsync(profileId, name, content, DataType.Memory, AvailabilityType.AlwaysOn, sortOrder: sortOrder);
    }

    public async Task<ContextData> CreateAlwaysOnInsightAsync(int profileId, string name, string content, int sortOrder = 0)
    {
        return await CreateContextDataAsync(profileId, name, content, DataType.Insight, AvailabilityType.AlwaysOn, sortOrder: sortOrder);
    }

    public async Task<ContextData> CreateTriggeredMemoryAsync(int profileId, string name, string content, string keywords, int minMatch = 1)
    {
        return await CreateContextDataAsync(profileId, name, content, DataType.Memory, AvailabilityType.Trigger,
            triggerKeywords: keywords, triggerMinMatchCount: minMatch);
    }

    public async Task<ContextData> CreateSemanticQuoteAsync(int profileId, int sessionId, string name, string content)
    {
        return await CreateContextDataAsync(profileId, name, content, DataType.Quote, AvailabilityType.Semantic,
            sourceSessionId: sessionId);
    }

    public async Task<ContextData> CreateSemanticVoiceSampleAsync(int profileId, string name, string content)
    {
        return await CreateContextDataAsync(profileId, name, content, DataType.PersonaVoiceSample, AvailabilityType.Semantic);
    }

    public async Task<ContextData> CreateCharacterProfileAsync(int profileId, string name, string content, bool isUser = false)
    {
        return await CreateContextDataAsync(profileId, name, content, DataType.CharacterProfile, AvailabilityType.AlwaysOn, isUser: isUser);
    }

    #endregion ContextData

    #region Flag

    public async Task<Flag> CreateFlagAsync(int profileId, string value, bool active = true, bool constant = false)
    {
        var flag = new Flag
        {
            Value = value,
            Active = active,
            Constant = constant,
            ProfileId = profileId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Flags.Add(flag);
        await _db.SaveChangesAsync();
        return flag;
    }

    #endregion Flag
}