using CAF;
using CAF.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog from configuration (appsettings.json + appsettings.{Environment}.json)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Prevent reference loop errors when serializing EF entities with navigation properties
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5198"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add HttpClient for API calls
builder.Services.AddHttpClient("GeminiClient", client =>
{
    client.Timeout = TimeSpan.FromMinutes(5); // Client-side limit
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // 1. Keep the connection alive while Gemini "thinks"
    KeepAlivePingDelay = TimeSpan.FromSeconds(20),
    KeepAlivePingTimeout = TimeSpan.FromSeconds(20),
    KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,

    // 2. Prevent the pool from killing the connection at 60s
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),

    // 3. Ensure headers don't time out if the model is slow to start
    ConnectTimeout = TimeSpan.FromMinutes(5)
});

// Register all application services using extension methods
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddLLMClients(builder.Configuration);
builder.Services.AddConversationServices();
builder.Services.AddEnrichers();
builder.Services.AddSemanticServices(builder.Configuration);
builder.Services.AddCoreServices();
builder.Services.AddTelegramBot(builder.Configuration);
builder.Services.AddBackgroundServices();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Register global exception handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    // Use factory to create a short-lived context for migrations and initialization
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GeneralDbContext>>();
    using var dbContext = dbFactory.CreateDbContext();
    if (dbContext.Database.GetPendingMigrations().Any())
        dbContext.Database.Migrate();

    await scope.ServiceProvider.GetRequiredService<ISettingService>().EnsureDefaultsInitializedAsync();
    await scope.ServiceProvider.GetRequiredService<ISystemMessageService>().EnsureDefaultsInitializedAsync();
}

// Telegram bot hosted service will manage startup and shutdown

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable CORS
app.UseCors();

// Enable static files
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program
{ }