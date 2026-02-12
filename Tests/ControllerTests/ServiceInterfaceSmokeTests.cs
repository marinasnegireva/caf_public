using CAF.Interfaces;
using System.Reflection;
using Tests.Infrastructure;

namespace Tests.ControllerTests;

/// <summary>
/// Smoke tests that verify all service interfaces can be resolved from DI
/// and their methods can be invoked with test data.
/// </summary>
[TestFixture]
[Category("Integration")]
public sealed class ServiceInterfaceSmokeTests : IntegrationTestBase
{
    // Test data IDs populated during setup
    private int _testProfileId;

    private int _testSessionId;
    private int _testFlagId;
    private int _testSettingId;
    private int _testSystemMessageId;

    /// <summary>
    /// Methods that require complex setup and should be skipped in smoke tests.
    /// Format: "InterfaceName.MethodName"
    /// </summary>
    private static readonly HashSet<string> SkippedMethods =
    [
        // These methods require complex state objects that can't be auto-constructed
        "IBatchProcessingService.ProcessBatchAsync",
        "IConversationPipeline.ProcessAsync",
        "IConversationEnrichmentOrchestrator.EnrichAsync",
        "IConversationContextBuilder.BuildContextAsync",

        // These methods require specific non-null string parameters
        "ILLMProviderFactory.GetProvider",
        "IContextFileService.GetContextFileAsync",
        "IContextFileService.GetContextFilesAsync",

        // LLM clients require actual API calls
        "IGeminiClient.GenerateContentAsync",
        "IGeminiClient.GetEmbeddingsAsync",
        "IClaudeClient.GenerateContentAsync",
        "IDeepSeekClient.GenerateContentAsync",

        // Quote services that require Qdrant or complex setup
        "IQuoteManagementService.SearchQuotesAsync",
        "IQuoteManagementService.GetRelatedQuotesAsync",
        "IQdrantService.SearchAsync",
        "IQdrantService.DeleteAsync",
        "IQdrantService.UpsertAsync",

        // Delete methods - skip to preserve test data for other tests
        "IProfileService.DeleteProfileAsync",
        "ISessionService.DeleteSessionAsync",
        "IFlagService.DeleteAsync",
        "ISettingService.DeleteAsync",
        "ISystemMessageService.DeleteAsync",
        "ITurnService.DeleteTurnAsync",
        "IContextTriggerService.DeleteAsync",
        "IQuoteManagementService.DeleteQuoteAsync",

        // Turn service - requires existing turns
        "ITurnService.UpdateTurnAsync",
        "ITurnService.GetTurnAsync",

        // Context trigger service - requires existing triggers
        "IContextTriggerService.GetByIdAsync",
        "IContextTriggerService.UpdateAsync",

        // Quote service - requires existing quotes
        "IQuoteManagementService.GetQuoteByIdAsync",
        "IQuoteManagementService.UpdateQuoteAsync",
    ];

    /// <summary>
    /// Interfaces that are conditionally registered and may not be available in test environment.
    /// </summary>
    private static readonly HashSet<string> OptionalServices =
    [
        "IQdrantService",      // Requires Qdrant server configuration
        "IVectorCollectionManager", // Requires Qdrant
    ];

    public override async Task SetUpBase()
    {
        await base.SetUpBase();
        await SeedTestDataAsync();
    }

    private async Task SeedTestDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        // Create test profile
        var profileService = sp.GetRequiredService<IProfileService>();
        var profile = await profileService.CreateProfileAsync(new Profile { Name = "SmokeTestProfile" });
        _testProfileId = profile.Id;
        await profileService.ActivateProfileAsync(_testProfileId);

        // Create test session
        var sessionService = sp.GetRequiredService<ISessionService>();
        var session = await sessionService.CreateSessionAsync("SmokeTestSession");
        _testSessionId = session.Id;

        // Create test flag
        var flagService = sp.GetRequiredService<IFlagService>();
        var flag = await flagService.CreateAsync("smoke-test-flag");
        _testFlagId = flag.Id;

        // Create test setting
        var settingService = sp.GetRequiredService<ISettingService>();
        var setting = await settingService.CreateOrUpdateAsync(CAF.Services.SettingsKeys.PreviousTurnsCount, "test-value");
        _testSettingId = (int)setting.Id;

        // Create test system message
        var systemMessageService = sp.GetRequiredService<ISystemMessageService>();
        var systemMessage = await systemMessageService.CreateAsync(new SystemMessage
        {
            Name = "SmokeTestSystemMessage",
            Type = SystemMessage.SystemMessageType.Technical,
            Content = "Smoke test content"
        });
        _testSystemMessageId = systemMessage.Id;
    }

    public static IEnumerable<Type> ServiceInterfaceTypes()
    {
        var cafAssembly = typeof(Program).Assembly;

        return [.. cafAssembly
            .GetTypes()
            .Where(t => t is { IsInterface: true, IsPublic: true } && t.Namespace == "CAF.Interfaces.Services")
            .OrderBy(t => t.FullName)];
    }

    [TestCaseSource(nameof(ServiceInterfaceTypes))]
    public async Task Resolve_Service_From_DI(Type interfaceType)
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var service = sp.GetService(interfaceType);

        if (service is null && OptionalServices.Contains(interfaceType.Name))
        {
            Assert.Pass($"{interfaceType.Name} is an optional service and not configured in test environment");
            return;
        }

        Assert.That(service, Is.Not.Null, $"DI could not resolve {interfaceType.FullName}");
    }

    [TestCaseSource(nameof(ServiceInterfaceTypes))]
    public async Task Invoke_Safe_Methods(Type interfaceType)
    {
        using var scope = _factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        var service = sp.GetService(interfaceType);
        if (service is null)
        {
            Assert.Inconclusive($"Service {interfaceType.FullName} could not be resolved");
            return;
        }

        var methods = interfaceType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName)
            .Where(m => !IsSkippedMethod(interfaceType, m))
            .Where(m => CanProvideAllArguments(m, sp))
            .ToArray();

        if (methods.Length == 0)
        {
            Assert.Pass($"{interfaceType.Name} has no safely-invokable methods (all require complex parameters)");
            return;
        }

        foreach (var method in methods)
        {
            try
            {
                var args = CreateArguments(method, sp);
                var result = method.Invoke(service, args);

                if (result is Task task)
                    await task;
            }
            catch (TargetInvocationException tie)
            {
                Assert.Fail($"{interfaceType.Name}.{method.Name} threw: {tie.InnerException?.GetType().Name}: {tie.InnerException?.Message}");
            }
        }
    }

    private static bool IsSkippedMethod(Type interfaceType, MethodInfo method)
    {
        var key = $"{interfaceType.Name}.{method.Name}";
        return SkippedMethods.Contains(key);
    }

    private static bool CanProvideAllArguments(MethodInfo method, IServiceProvider sp)
    {
        foreach (var p in method.GetParameters())
        {
            // CancellationToken is always providable
            if (p.ParameterType == typeof(CancellationToken))
                continue;

            // Can resolve from DI
            if (sp.GetService(p.ParameterType) is not null)
                continue;

            // Has a default value
            if (p.HasDefaultValue)
                continue;

            // Value types can be default-constructed (we'll provide real IDs)
            if (p.ParameterType.IsValueType)
                continue;

            // Nullable reference types are OK
            if (IsNullableReferenceType(p))
                continue;

            // String parameters without defaults are problematic
            if (p.ParameterType == typeof(string))
                return false;

            // Other reference types without defaults are problematic
            return false;
        }

        return true;
    }

    private static bool IsNullableReferenceType(ParameterInfo p)
    {
        var nullableAttr = p.GetCustomAttributes(typeof(System.Runtime.CompilerServices.NullableAttribute), true)
            .FirstOrDefault();

        return nullableAttr is System.Runtime.CompilerServices.NullableAttribute attr
            ? attr.NullableFlags.Length > 0 && attr.NullableFlags[0] == 2
            : p.HasDefaultValue && p.DefaultValue is null;
    }

    private object?[] CreateArguments(MethodInfo method, IServiceProvider sp)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];

            if (p.ParameterType == typeof(CancellationToken))
            {
                args[i] = CancellationToken.None;
                continue;
            }

            var resolved = sp.GetService(p.ParameterType);
            if (resolved is not null)
            {
                args[i] = resolved;
                continue;
            }

            if (p.HasDefaultValue)
            {
                args[i] = p.DefaultValue;
                continue;
            }

            // Provide real test data IDs for int parameters named "id"
            if (p.ParameterType == typeof(int) && IsIdParameter(p, method))
            {
                args[i] = GetTestIdForMethod(method);
                continue;
            }

            if (p.ParameterType.IsValueType)
            {
                args[i] = Activator.CreateInstance(p.ParameterType);
                continue;
            }

            args[i] = null;
        }

        return args;
    }

    private static bool IsIdParameter(ParameterInfo p, MethodInfo method)
    {
        var name = p.Name?.ToLowerInvariant() ?? "";
        return name is "id" or "profileid" or "sessionid" or "flagid" or "settingid" or "memoryid" or "messageid";
    }

    private int GetTestIdForMethod(MethodInfo method)
    {
        var declaringType = method.DeclaringType?.Name ?? "";
        var methodName = method.Name.ToLowerInvariant();

        return declaringType switch
        {
            "IProfileService" => _testProfileId,
            "ISessionService" => _testSessionId,
            "IFlagService" => _testFlagId,
            "ISettingService" => _testSettingId,
            "ISystemMessageService" => _testSystemMessageId,
            "ITurnService" => 0, // No test turn created, will be skipped anyway
            "IContextTriggerService" => 0, // No test trigger created
            "IQuoteManagementService" => 0, // No test quote created
            _ => 0
        };
    }
}