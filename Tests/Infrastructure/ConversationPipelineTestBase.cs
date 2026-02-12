using CAF.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Testcontainers.Qdrant;

namespace Tests.Infrastructure;

/// <summary>
/// Base class for ConversationPipeline integration tests.
/// Provides PostgreSQL + Qdrant containers, mocked LLM clients, and common test data setup.
/// </summary>
[TestFixture]
public abstract class ConversationPipelineTestBase
{
    private PostgreSqlContainer _postgresContainer = null!;
    private QdrantContainer _qdrantContainer = null!;

    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected IServiceScope Scope { get; private set; } = null!;
    protected GeneralDbContext Db { get; private set; } = null!;
    protected IConversationPipeline Pipeline { get; private set; } = null!;
    protected Mock<IGeminiClient> MockGeminiClient { get; private set; } = null!;
    protected Mock<IClaudeClient> MockClaudeClient { get; private set; } = null!;
    protected Profile TestProfile { get; private set; } = null!;
    protected Session TestSession { get; private set; } = null!;

    protected LLMMockConfigurator LLMMocks { get; private set; } = null!;
    protected TestDataBuilder TestData { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUpBase()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:latest")
            .Build();

        await _qdrantContainer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownBase()
    {
        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync();
            await _postgresContainer.DisposeAsync();
        }

        if (_qdrantContainer != null)
        {
            await _qdrantContainer.StopAsync();
            await _qdrantContainer.DisposeAsync();
        }
    }

    [SetUp]
    public virtual async Task SetUpBase()
    {
        MockGeminiClient = new Mock<IGeminiClient>();
        MockClaudeClient = new Mock<IClaudeClient>();
        LLMMocks = new LLMMockConfigurator(MockGeminiClient, MockClaudeClient);

        // Configure default mock responses
        LLMMocks.ConfigureDefaultGeminiResponse();
        LLMMocks.ConfigureDefaultEmbeddings();

        // Step 1: Create DbContext directly and run migrations WITHOUT starting the app
        // This avoids the Program.cs startup code which tries to initialize settings
        var optionsBuilder = new DbContextOptionsBuilder<GeneralDbContext>();
        optionsBuilder.UseNpgsql(_postgresContainer.GetConnectionString());
        
        await using (var tempDb = new GeneralDbContext(optionsBuilder.Options))
        {
            await tempDb.Database.MigrateAsync();

            // Create initial test profile BEFORE app startup
            var profile = new Profile
            {
                Name = "Initial Profile",
                Description = "Initial profile for test setup",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastActivatedAt = DateTime.UtcNow
            };
            
            tempDb.Profiles.Add(profile);
            await tempDb.SaveChangesAsync();
        }

        // Step 2: Now create the factory - Program.cs startup will find the profile
        Factory = CreateWebApplicationFactory();
        Scope = Factory.Services.CreateScope();
        Db = Scope.ServiceProvider.GetRequiredService<GeneralDbContext>();

        // Initialize test data builder
        TestData = new TestDataBuilder(Db);

        // Seed base test data
        await SeedBaseTestDataAsync();

        // Initialize Qdrant collections and index semantic data
        await InitializeQdrantCollectionsAsync();

        // Recreate scope to pick up test profile in cached services
        Scope.Dispose();
        Scope = Factory.Services.CreateScope();
        Db = Scope.ServiceProvider.GetRequiredService<GeneralDbContext>();
        Pipeline = Scope.ServiceProvider.GetRequiredService<IConversationPipeline>();

        // Recreate TestData with new Db instance for derived class use
        TestData = new TestDataBuilder(Db);
    }

    [TearDown]
    public virtual async Task TearDownBase()
    {
        if (Db != null)
        {
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
        }

        Scope?.Dispose();
        await Factory.DisposeAsync();
    }

    protected virtual WebApplicationFactory<Program> CreateWebApplicationFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ConfigureDatabaseServices(services);
                    ConfigureQdrantServices(services);
                    ConfigureLLMServices(services);
                    ConfigureMockedServices(services);
                    ConfigureAdditionalServices(services);
                });
            });
    }

    protected virtual void ConfigureDatabaseServices(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<GeneralDbContext>>();
        services.RemoveAll<GeneralDbContext>();
        services.RemoveAll<IDbContextFactory<GeneralDbContext>>();

        var connectionString = _postgresContainer.GetConnectionString();
        services.AddDbContextFactory<GeneralDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<GeneralDbContext>>().CreateDbContext());
    }

    protected virtual void ConfigureQdrantServices(IServiceCollection services)
    {
        services.RemoveAll<IOptions<QdrantOptions>>();
        services.Configure<QdrantOptions>(opts =>
        {
            opts.Host = _qdrantContainer.Hostname;
            opts.Port = _qdrantContainer.GetMappedPublicPort(6334);
        });
    }

    protected virtual void ConfigureLLMServices(IServiceCollection services)
    {
        services.RemoveAll<IGeminiClient>();
        services.RemoveAll<IClaudeClient>();
        services.AddSingleton(MockGeminiClient.Object);
        services.AddSingleton(MockClaudeClient.Object);
    }

    protected virtual void ConfigureMockedServices(IServiceCollection services)
    {
        services.RemoveAll<ITelegramBotService>();
        services.AddSingleton(sp =>
            new Mock<ITelegramBotService>().Object);
    }

    /// <summary>
    /// Override to add additional service configurations specific to your test class.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Seeds the base test data. Override to customize or extend.
    /// </summary>
    protected virtual async Task SeedBaseTestDataAsync()
    {
        // Deactivate existing profiles
        var existingProfiles = await Db.Profiles.Where(p => p.IsActive).ToListAsync();
        foreach (var profile in existingProfiles)
        {
            profile.IsActive = false;
        }
        if (existingProfiles.Count > 0)
        {
            await Db.SaveChangesAsync();
        }

        // Create test profile
        TestProfile = await TestData.CreateProfileAsync("Test Profile", isActive: true);

        // Create persona system message
        await TestData.CreateSystemMessageAsync(
            TestProfile.Id,
            "Test Persona",
            "You are a helpful AI assistant named TestBot. You are knowledgeable, friendly, and concise.",
            SystemMessage.SystemMessageType.Persona);

        // Create perception system messages
        await TestData.CreateSystemMessageAsync(
            TestProfile.Id,
            "Perception Step 1",
            "Analyze the user's emotional tone. Output: [Emotional Tone: {tone}]",
            SystemMessage.SystemMessageType.Perception);

        await TestData.CreateSystemMessageAsync(
            TestProfile.Id,
            "Perception Step 2",
            "Identify key topics in the user's message. Output: [Topics: {topics}]",
            SystemMessage.SystemMessageType.Perception);

        // Create default settings
        await TestData.CreateSettingAsync(TestProfile.Id, "LLMProvider", "Gemini");
        await TestData.CreateSettingAsync(TestProfile.Id, "PerceptionEnabled", "true");
        await TestData.CreateSettingAsync(TestProfile.Id, "RecentTurnsCount", "5");
        await TestData.CreateSettingAsync(TestProfile.Id, "MaxDialogueLogTurns", "50");

        // Create test session
        TestSession = await TestData.CreateSessionAsync(TestProfile.Id, "Test Session");
    }

    /// <summary>
    /// Initializes Qdrant collections and indexes semantic test data.
    /// This ensures semantic search works for voice samples and quotes.
    /// </summary>
    protected virtual async Task InitializeQdrantCollectionsAsync()
    {
        try
        {
            var qdrantFactory = Scope.ServiceProvider.GetRequiredService<IQdrantServiceFactory>();
            var semanticService = Scope.ServiceProvider.GetRequiredService<ISemanticService>();

            // Ensure collections exist for all semantic-enabled types
            var quoteQdrant = qdrantFactory.CreateService("context_quotes");
            var memoryQdrant = qdrantFactory.CreateService("context_memories");
            var insightQdrant = qdrantFactory.CreateService("context_insights");
            var voiceQdrant = qdrantFactory.CreateService("context_voice_samples");

            await quoteQdrant.EnsureCollectionAsync();
            await memoryQdrant.EnsureCollectionAsync();
            await insightQdrant.EnsureCollectionAsync();
            await voiceQdrant.EnsureCollectionAsync();

            // Index all semantic-enabled context data
            var semanticData = await Db.ContextData
                .Where(cd => cd.Availability == AvailabilityType.Semantic && cd.IsEnabled)
                .ToListAsync();

            if (semanticData.Count > 0)
            {
                await semanticService.EmbedBatchAsync(semanticData);
                Console.WriteLine($"Successfully indexed {semanticData.Count} semantic context data entries");
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail test setup if Qdrant initialization fails
            Console.WriteLine($"Warning: Qdrant initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates a setting value, creating it if it doesn't exist.
    /// </summary>
    protected async Task UpdateSettingAsync(string name, string value)
    {
        var setting = await Db.Settings.FirstOrDefaultAsync(s => s.Name == name);
        if (setting != null)
        {
            setting.Value = value;
            Db.Settings.Update(setting);
            await Db.SaveChangesAsync();
        }
        else
        {
            await TestData.CreateSettingAsync(TestProfile.Id, name, value);
        }
    }

    /// <summary>
    /// Creates a new service scope. Remember to dispose when done.
    /// </summary>
    protected IServiceScope CreateScope() => Factory.Services.CreateScope();

    /// <summary>
    /// Gets a service from a new scope.
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}