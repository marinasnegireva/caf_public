using CAF.Services.Conversation;
using CAF.Services.Conversation.Enrichment;
using CAF.Services.Conversation.Providers;

namespace CAF;

/// <summary>
/// Extension methods for service registration to keep Program.cs clean and organized
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Register database services (DbContext factory and scoped context)
    /// </summary>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use factory for background/hosted/transient consumers
        services.AddDbContextFactory<GeneralDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Register scoped DbContext resolved from factory
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<GeneralDbContext>>().CreateDbContext());

        return services;
    }

    /// <summary>
    /// Register all LLM client implementations (Gemini, Claude)
    /// </summary>
    public static IServiceCollection AddLLMClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Gemini Client
        services.Configure<GeminiOptions>(
            configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient(nameof(IGeminiClient));
        services.AddScoped<IGeminiClient, GeminiClient>();

        // Claude Client
        services.Configure<ClaudeOptions>(
            configuration.GetSection(ClaudeOptions.SectionName));
        services.AddHttpClient(nameof(IClaudeClient));
        services.AddScoped<IClaudeClient, ClaudeClient>();

        return services;
    }

    /// <summary>
    /// Register core conversation pipeline services
    /// </summary>
    public static IServiceCollection AddConversationServices(
        this IServiceCollection services)
    {
        // Context and state management
        services.AddScoped<ISystemMessageService, SystemMessageService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<ITurnService, TurnService>();
        services.AddScoped<IConversationContextBuilder, ConversationStateBuilder>();

        // Enrichment orchestration
        services.AddScoped<IConversationEnrichmentOrchestrator, ConversationEnrichmentOrchestrator>();

        // LLM provider strategies and factory
        services.AddScoped<ILLMProviderStrategy, GeminiProviderStrategy>();
        services.AddScoped<ILLMProviderStrategy, ClaudeProviderStrategy>();
        services.AddScoped<ILLMProviderFactory, LLMProviderFactory>();

        // Pipeline and request building
        services.AddScoped<IConversationPipeline, ConversationPipeline>();
        services.AddScoped<IConversationRequestBuilder, ConversationRequestBuilder>();

        // Turn processing
        services.AddTransient<ITurnStripperService, GeminiTurnStripperService>();

        return services;
    }

    /// <summary>
    /// Register all conversation enrichers
    /// </summary>
    public static IServiceCollection AddEnrichers(
        this IServiceCollection services)
    {
        // Data type-specific enrichers (new unified approach)
        services.AddScoped<IEnricher, QuoteEnricher>();
        services.AddScoped<IEnricher, PersonaVoiceSampleEnricher>();
        services.AddScoped<IEnricher, MemoryDataEnricher>();
        services.AddScoped<IEnricher, InsightEnricher>();
        services.AddScoped<IEnricher, CharacterProfileEnricher>();
        services.AddScoped<IEnricher, GenericDataEnricher>();

        // Semantic enricher (handles semantic search for all applicable types)
        services.AddScoped<IEnricher, SemanticDataEnricher>();

        // Trigger enricher (handles trigger-based context data)
        services.AddScoped<IEnricher, TriggerEnricher>();

        // Other enrichers
        services.AddScoped<IEnricher, PerceptionEnricher>();
        services.AddScoped<IEnricher, DialogueLogEnricher>();
        services.AddScoped<IEnricher, TurnHistoryEnricher>();
        services.AddScoped<IEnricher, FlagEnricher>();

        return services;
    }

    /// <summary>
    /// Register vector database and semantic services
    /// </summary>
    public static IServiceCollection AddSemanticServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Qdrant configuration
        services.Configure<QdrantOptions>(
            configuration.GetSection(QdrantOptions.SectionName));

        services.AddSingleton<IQdrantServiceFactory, QdrantServiceFactory>();
        services.AddScoped<IVectorCollectionManager, VectorCollectionManager>();

        // Unified semantic service for ContextData (handles all embedding operations)
        services.AddScoped<ISemanticService, SemanticService>();

        return services;
    }

    /// <summary>
    /// Register core services (flags, settings, profiles, context data)
    /// </summary>
    public static IServiceCollection AddCoreServices(
        this IServiceCollection services)
    {
        services.AddScoped<IFlagService, FlagService>();
        services.AddScoped<ISettingService, SettingService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IContextDataService, ContextDataService>();

        return services;
    }

    /// <summary>
    /// Register Telegram bot service and configuration
    /// </summary>
    public static IServiceCollection AddTelegramBot(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TelegramBotOptions>(
            configuration.GetSection(TelegramBotOptions.SectionName));

        services.AddSingleton<ITelegramBotService, TelegramBotService>();
        services.AddHostedService<TelegramBotHostedService>();

        return services;
    }

    /// <summary>
    /// Register background services for asynchronous processing
    /// </summary>
    public static IServiceCollection AddBackgroundServices(
        this IServiceCollection services)
    {
        services.AddHostedService<TurnStripperBackgroundService>();

        return services;
    }
}