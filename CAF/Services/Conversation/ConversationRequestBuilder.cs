namespace CAF.Services.Conversation;

public sealed class ConversationRequestBuilder(
    GeneralDbContext db,
    IOptions<GeminiOptions> geminiOptions,
    IOptions<ClaudeOptions> claudeOptions,
    ISettingService settingService,
    ILogger<ConversationRequestBuilder> logger) : IConversationRequestBuilder
{
    private readonly GeminiOptions _geminiOptions = geminiOptions.Value;
    private readonly ClaudeOptions _claudeOptions = claudeOptions.Value;

    // Interface for abstracting differences between message builders
    private interface IMessageBuilder
    {
        void AddUserMessage(string content);

        void AddAcknowledgment(string message);

        void AddCacheBreakpoint();
    }

    private sealed class GeminiMessageBuilderAdapter(GeminiMessageBuilder builder) : IMessageBuilder
    {
        public void AddUserMessage(string content) => builder.AddUserMessage(content);

        public void AddAcknowledgment(string message) => builder.AddModelResponse(message);

        public void AddCacheBreakpoint()
        {
            
        }
    }

    private sealed class ClaudeMessageBuilderAdapter(ClaudeMessageBuilder builder) : IMessageBuilder
    {
        public void AddUserMessage(string content) => builder.AddUserMessage(content);

        public void AddAcknowledgment(string message) => builder.AddAssistantMessage(message);

        public void AddCacheBreakpoint()
        {
            builder.WithCacheBreakpointOnLastMessage();
        }
    }

    private async Task<string> BuildPromptAsync(
        ConversationState context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var perceptions = context.Perceptions ?? [];
        var flags = await BuildFlagsAsync(context, perceptions, cancellationToken);

        var prompt = new StringBuilder();
        if (context.IsOOCRequest)
        {
            prompt.AppendLine($"{ConversationConstants.OocPrefix} This is an out-of-character request. Respond as yourself, the AI assistant, not the persona.");
            prompt.AppendLine(context.CurrentTurn?.Input);
        }
        else
        {
            if (flags.Count != 0)
            {
                prompt.AppendLine(ConversationConstants.FlagsHeader);
                foreach (var flag in flags)
                {
                    prompt.AppendLine($"- {flag}");
                }
                prompt.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(context.UserName))

            {
                prompt.AppendLine(context.FormatUserInput(context.CurrentTurn?.Input ?? string.Empty));
            }
            else
            {
                prompt.AppendLine(context.CurrentTurn?.Input ?? string.Empty);
            }
        }

        return prompt.ToString();
    }

    private async Task AddCommonContextAsync(
        IMessageBuilder builder,
        ConversationState context)
    {
        var allContextData = context.GetAllContextData().ToList();
        logger.LogDebug("AddCommonContextAsync: Processing {Count} context data items", allContextData.Count);

        if (allContextData.Count > 0)
        {
            // Add user profile first if exists
            AddUserProfile(builder, context);

            // Add individual messages (Generic, CharacterProfile)
            AddIndividualMessages(builder, allContextData);

            // Add grouped messages (Memory, Insight, PersonaVoiceSample, Quote)
            AddGroupedMessages(builder, allContextData);
        }

        // Add dialogue history
        AddDialogueHistory(builder, context);

        // Add recent turns
        AddRecentTurns(builder, context);
    }

    private void AddUserProfile(IMessageBuilder builder, ConversationState context)
    {
        if (context.UserProfile != null)
        {
            var header = context.UserProfile.Name?.ToLower() ?? ConversationConstants.Headers.UserProfile;
            var content = context.UserProfile.GetDisplayContent().Contains(ConversationConstants.MetaTagPrefix) 
                ? context.UserProfile.GetDisplayContent() 
                : $"`{ConversationConstants.MetaTagPrefix} {header}`\n\n{context.UserProfile.GetDisplayContent()}";
            builder.AddUserMessage(content);
            builder.AddAcknowledgment(ConversationConstants.Acknowledgments.UserProfile);
            builder.AddCacheBreakpoint();
            logger.LogInformation("Added user profile '{Name}' (Id: {Id})",
                context.UserProfile.Name, context.UserProfile.Id);
        }
        else
        {
            logger.LogDebug("No user profile to add");
        }
    }


    private static void AddIndividualMessages(IMessageBuilder builder, List<ContextData> allContextData)
    {
        var individualMessageTypes = new[] { DataType.Generic, DataType.CharacterProfile };
        var individualMessagesAdded = false;

        foreach (var dataType in individualMessageTypes)
        {
            var items = GetOrderedItems(allContextData, dataType, excludeUser: true);

            foreach (var item in items)
            {
                var header = item.Name?.ToLower() ?? dataType.ToString().ToLower();
                var content = item.GetDisplayContent().Contains(ConversationConstants.MetaTagPrefix) 
                    ? item.GetDisplayContent() 
                    : $"`{ConversationConstants.MetaTagPrefix} {header}`\n\n{item.GetDisplayContent()}";
                builder.AddUserMessage(content);
                builder.AddAcknowledgment(ConversationConstants.Acknowledgments.Default);
                individualMessagesAdded = true;
            }

        }

        if (individualMessagesAdded)
        {
            builder.AddCacheBreakpoint();
        }
    }

    private static void AddGroupedMessages(IMessageBuilder builder, List<ContextData> allContextData)
    {
        var groupedMessageTypes = new[] { DataType.Memory, DataType.Insight, DataType.PersonaVoiceSample, DataType.Quote };

        foreach (var dataType in groupedMessageTypes)
        {
            var items = GetOrderedItems(allContextData, dataType, excludeUser: false, useSortOrder: true);

            if (items.Count > 0)
            {
                var header = GetHeaderForDataType(dataType);
                var content = BuildContextDataSectionWithHeader(header, items);
                builder.AddUserMessage(content);
                builder.AddAcknowledgment(ConversationConstants.Acknowledgments.Grouped(items.Count, header));

                if (dataType is DataType.Memory or DataType.Insight)

                {
                    builder.AddCacheBreakpoint();
                }
            }
        }
    }

    private static void AddDialogueHistory(IMessageBuilder builder, ConversationState context)
    {
        if (!string.IsNullOrWhiteSpace(context.DialogueLog))
        {
            builder.AddUserMessage(context.DialogueLog);
            builder.AddAcknowledgment(ConversationConstants.Acknowledgments.History);
        }
    }


    private static void AddRecentTurns(IMessageBuilder builder, ConversationState context)
    {
        if (context.RecentTurns is { Count: > 0 })
        {
            foreach (var turn in context.RecentTurns)
            {
                if (!string.IsNullOrWhiteSpace(turn.JsonInput))
                {
                    builder.AddUserMessage(turn.JsonInput);
                }
                else if (!string.IsNullOrWhiteSpace(turn.Input))
                {
                    builder.AddUserMessage(turn.Input);
                }

                if (!string.IsNullOrWhiteSpace(turn.Response))
                {
                    builder.AddAcknowledgment(turn.Response);
                }
            }
        }
    }

    private static List<ContextData> GetOrderedItems(
        List<ContextData> allContextData,
        DataType dataType,
        bool excludeUser = false,
        bool useSortOrder = false)
    {
        var query = allContextData.Where(d => d.Type == dataType);

        if (excludeUser)
        {
            query = query.Where(d => !d.IsUser);
        }

        return useSortOrder
            ? [.. query.OrderBy(d => d.SortOrder).ThenBy(d => d.Id)]
            : [.. query.OrderByDescending(d => d.TokenCount).ThenBy(d => d.Id)];
    }

    public async Task<GeminiRequest> BuildGeminiRequestAsync(
        ConversationState context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prosePrompt = await BuildPromptAsync(context, cancellationToken);
        context.CurrentTurn.JsonInput = prosePrompt;

        var builder = GeminiMessageBuilder.Create();
        var adapter = new GeminiMessageBuilderAdapter(builder);

        // SYSTEM: instruction (persona)
        if (!string.IsNullOrWhiteSpace(context.Persona?.Content))
        {
            builder.WithSystemInstruction(context.Persona.Content);
        }

        // Add all common context
        await AddCommonContextAsync(adapter, context);

        // NOW: Current request
        builder.AddUserMessage(prosePrompt);

        // Apply safety settings from configuration if available
        if (_geminiOptions.SafetySettings?.Count > 0)
        {
            var safetySettings = _geminiOptions.SafetySettings
                .Select(s => new SafetySetting
                {
                    Category = s.Category,
                    Threshold = s.Threshold
                })
                .ToList();

            builder.WithSafetySettings(safetySettings);
        }

        context.GeminiRequest = builder.Build();
        return context.GeminiRequest;
    }

    public async Task<ClaudeRequest> BuildClaudeRequestAsync(
        ConversationState context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var prosePrompt = await BuildPromptAsync(context, cancellationToken);
        context.CurrentTurn.JsonInput = prosePrompt;

        var builder = ClaudeMessageBuilder.Create();
        var adapter = new ClaudeMessageBuilderAdapter(builder);

        // SYSTEM: instruction (persona)
        if (!string.IsNullOrWhiteSpace(context.Persona?.Content))
        {
            builder.WithSystem(context.Persona.Content);
        }

        // Add all common context
        await AddCommonContextAsync(adapter, context);

        // NOW: Current request
        builder.AddUserMessage(prosePrompt);

        // Apply configuration
        builder.WithMaxTokens(_claudeOptions.MaxTokens);
        builder.WithTemperature(_claudeOptions.Temperature);

        // Apply thinking configuration if enabled
        if (_claudeOptions.EnableThinking)
        {
            builder.WithThinking();
        }

        var model = await settingService.GetByNameAsync(SettingsKeys.ClaudeModel, cancellationToken);
        context.ClaudeRequest = builder.Build(model?.Value ?? _claudeOptions.Model);
        return context.ClaudeRequest;
    }

    private static string BuildContextDataSectionWithHeader(string header, List<ContextData> data)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"`{ConversationConstants.MetaTagPrefix} {header}`");
        sb.AppendLine();

        foreach (var item in data)

        {
            var content = item.GetDisplayContent();
            if (string.IsNullOrWhiteSpace(content))
                continue;

            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GetHeaderForDataType(DataType dataType)
    {
        return dataType switch
        {
            DataType.Memory => ConversationConstants.Headers.Memories,
            DataType.Insight => ConversationConstants.Headers.Insights,
            DataType.PersonaVoiceSample => ConversationConstants.Headers.VoiceSample,
            DataType.Quote => ConversationConstants.Headers.Quotes,
            _ => dataType.ToString().ToLower()
        };
    }


    private async Task<List<string>> BuildFlagsAsync(
        ConversationState context,
        IEnumerable<PerceptionRecord> perceptions,
        CancellationToken cancellationToken)
    {
        var flags = new List<string>();
        var perceptionList = perceptions.ToList();

        // Process perception-based flags
        if (perceptionList.Any(p => p.Property.Contains("understanding.complaint:true", StringComparison.OrdinalIgnoreCase)))
        {
            flags.Add($"[direction] Exploration: You made a mistake about {context.UserName}, understand it");
        }

        if (perceptionList.Any(p => p.Property.Contains("exploration.desire:true", StringComparison.OrdinalIgnoreCase)))
        {
            var topics = perceptionList
                .Where(p => p.Property.StartsWith("exploration.topic:", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Property["exploration.topic:".Length..])
                .FirstOrDefault();

            flags.Add("[direction] Explore ideas on topics: " + topics);
        }

        // Filter out processed perceptions (without mutating original collection)
        var remainingPerceptions = perceptionList.Where(p =>
            !p.Property.Contains("understanding.complaint", StringComparison.OrdinalIgnoreCase) &&
            !p.Property.Contains("exploration", StringComparison.OrdinalIgnoreCase));

        flags.AddRange(remainingPerceptions.Select(p => p.Property));
        // Get database flags in a single query
        var now = DateTime.UtcNow;
        var dbFlags = await db.Flags
            .Where(f => f.Active || (!f.Active && f.Constant))
            .ToListAsync(cancellationToken);

        if (dbFlags.Count > 0)
        {
            flags.AddRange(dbFlags.Select(f => f.Value));

            // Update all flags at once
            foreach (var flag in dbFlags)
            {
                if (flag.Active)
                {
                    flag.Active = false;
                }
                flag.LastUsedAt = now;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return [.. flags.Distinct()];
    }
}