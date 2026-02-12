using Testcontainers.PostgreSql;

namespace Tests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class LLMLoggerPersistenceIntegrationTests
{
    private PostgreSqlContainer _postgresContainer = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        var opts = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        await using var ctx = new GeneralDbContext(opts);
        await ctx.Database.MigrateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _postgresContainer.StopAsync();
        await _postgresContainer.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        var opts = new DbContextOptionsBuilder<GeneralDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        await using var ctx = new GeneralDbContext(opts);
        await ctx.LLMRequestLogs.ExecuteDeleteAsync();
    }
}