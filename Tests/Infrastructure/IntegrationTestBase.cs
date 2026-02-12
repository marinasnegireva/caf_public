using CAF.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Tests.Infrastructure;

/// <summary>
/// Base class for integration tests that provides shared PostgreSQL container,
/// WebApplicationFactory configuration, and common test utilities.
/// </summary>
[TestFixture]
public abstract class IntegrationTestBase
{
    private PostgreSqlContainer _postgresContainer = null!;
    protected WebApplicationFactory<Program> Factory { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;

    // Backwards compatible aliases for tests using underscore naming
    protected WebApplicationFactory<Program> _factory => Factory;

    protected HttpClient _client => Client;

    /// <summary>
    /// JSON serialization options matching the application's configuration.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    // Backwards compatible alias
    protected static JsonSerializerOptions _jsonOptions => JsonOptions;

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
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDownBase()
    {
        await _postgresContainer.StopAsync();
        await _postgresContainer.DisposeAsync();
    }

    [SetUp]
    public virtual async Task SetUpBase()
    {
        // Step 1: Create DbContext directly and run migrations WITHOUT starting the app
        // This avoids the Program.cs startup code which tries to initialize settings
        var optionsBuilder = new DbContextOptionsBuilder<GeneralDbContext>();
        optionsBuilder.UseNpgsql(_postgresContainer.GetConnectionString());
        
        await using (var dbContext = new GeneralDbContext(optionsBuilder.Options))
        {
            await dbContext.Database.MigrateAsync();

            // Ensure a default active profile exists BEFORE app initialization
            var profile = new Profile
            {
                Name = "Test Profile",
                Description = "Test profile for integration tests",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastActivatedAt = DateTime.UtcNow
            };
            
            dbContext.Profiles.Add(profile);
            await dbContext.SaveChangesAsync();
        }

        // Step 2: Now create the actual test factory - startup code will find the profile
        Factory = CreateWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    [TearDown]
    public virtual async Task TearDownBase()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GeneralDbContext>();
        await dbContext.Database.EnsureDeletedAsync();

        Client?.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>
    /// Creates the WebApplicationFactory with the standard test configuration.
    /// Override to add additional service customizations.
    /// </summary>
    protected virtual WebApplicationFactory<Program> CreateWebApplicationFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ConfigureDatabaseServices(services);
                    ConfigureMockedServices(services);
                    ConfigureAdditionalServices(services);
                });
            });
    }

    /// <summary>
    /// Configures database services to use the test container.
    /// This replaces the production DbContext with one pointing to the test PostgreSQL container.
    /// </summary>
    protected virtual void ConfigureDatabaseServices(IServiceCollection services)
    {
        // Remove all existing DbContext-related registrations
        services.RemoveAll<DbContextOptions<GeneralDbContext>>();
        services.RemoveAll<GeneralDbContext>();
        services.RemoveAll<IDbContextFactory<GeneralDbContext>>();

        // Re-register with test container connection string using factory pattern
        services.AddDbContextFactory<GeneralDbContext>(options =>
            options.UseNpgsql(_postgresContainer.GetConnectionString()));

        // Register scoped DbContext resolved from factory
        services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<GeneralDbContext>>().CreateDbContext());
    }

    /// <summary>
    /// Configures mocked external services (e.g., Telegram bot).
    /// Override to customize or disable specific mocks.
    /// </summary>
    protected virtual void ConfigureMockedServices(IServiceCollection services)
    {
        // Mock Telegram bot service to avoid external API calls during tests
        services.RemoveAll<ITelegramBotService>();
        var telegramMock = new Mock<ITelegramBotService>();
        telegramMock.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        telegramMock.Setup(x => x.StopAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        services.AddSingleton(_ => telegramMock.Object);
    }

    /// <summary>
    /// Override this method to add additional service configurations specific to your test class.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
        // Override in derived classes to add test-specific services
    }

    /// <summary>
    /// Gets a scoped service from the test application's DI container.
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Creates a new scope and returns the service provider for more complex scenarios.
    /// Remember to dispose the scope when done.
    /// </summary>
    protected IServiceScope CreateScope() => Factory.Services.CreateScope();
}