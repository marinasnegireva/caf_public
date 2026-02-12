using CAF.Interfaces;
using CAF.LLM.Logging;
using Microsoft.Extensions.Configuration;

namespace Tests.IntegrationTests;

[Ignore("Integration")]
[TestFixture]
public class GeminiClientGenerateContentIntegrationTests
{
    private IGeminiClient? _geminiClient;
    private GeminiOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        // Load configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "CAF"))
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        _options = new GeminiOptions();
        configuration.GetSection(GeminiOptions.SectionName).Bind(_options);

        // Skip tests if no API key is configured
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            Assert.Ignore("Gemini API key not configured");
        }

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var llmLogger = loggerFactory.CreateLogger<LLMLogger>();
        var httpClient = new HttpClient();

        // Setup Gemini client with real API key from config
        var geminiOptions = Options.Create(_options);

        _geminiClient = new GeminiClient(geminiOptions, httpClient, null, llmLogger);
    }

    #region GenerateContentAsync Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_SimpleMessage_ReturnsSuccess()
    {
        // Arrange
        var request = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "Say 'Hello, World!' and nothing else." }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 100,
                Temperature = 0.1f,
                ThinkingConfig = new ThinkingConfig
                {
                    IncludeThoughts = false // Disable thinking for predictable test results
                }
            }
        };

        // Act
        var response = await _geminiClient!.GenerateContentAsync(request);

        // Log response for debugging
        TestContext.WriteLine($"Success: {response.success}");
        TestContext.WriteLine($"Result is null: {response.result == null}");
        TestContext.WriteLine($"Result: {response.result ?? "<NULL>"}");

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(response.success, Is.True, "API call should succeed");
            Assert.That(response.result, Is.Not.Null.And.Not.Empty, "Response should not be empty");
        });
        Assert.That(response.result.ToLower(), Does.Contain("hello"), "Response should contain greeting");

        var response2 = await _geminiClient!.StreamGenerateContentAsync(request);
        Assert.Multiple(() =>
        {
            Assert.That(response2.success, Is.True, "Streaming API call should succeed");
            Assert.That(response2.result, Is.Not.Null.And.Not.Empty, "Streaming response should not be empty");
        });
        Assert.That(response2.result.ToLower(), Does.Contain("hello"), "Streaming response should contain greeting");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithSystemInstruction_FollowsSystemPrompt()
    {
        // Arrange
        var request = new GeminiRequest
        {
            SystemInstruction = new SystemInstruction
            {
                Parts =
                [
                    new() { Text = "You are a pirate. Always respond in pirate speak using phrases like 'arr' and 'matey'." }
                ]
            },
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "Introduce yourself in one sentence." }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 200,
                Temperature = 0.7f,
                ThinkingConfig = new ThinkingConfig
                {
                    ThinkingLevel = "LOW",
                    IncludeThoughts = false
                }
            }
        };

        // Act
        var response = await _geminiClient!.GenerateContentAsync(request);

        // Log response for debugging
        TestContext.WriteLine($"Success: {response.success}");
        TestContext.WriteLine($"Result: {response.result ?? "<NULL>"}");

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(response.success, Is.True, "API call should succeed");
            Assert.That(response.result, Is.Not.Null.And.Not.Empty, "Response should not be empty");
        });

        // Check for pirate-like language indicators
        var lowerResult = response.result.ToLower();
        var hasPirateLanguage = lowerResult.Contains("arr") ||
                               lowerResult.Contains("matey") ||
                               lowerResult.Contains("pirate") ||
                               lowerResult.Contains("ahoy");
        Assert.That(hasPirateLanguage, Is.True, "Response should contain pirate-like language");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithConversationHistory_MaintainsContext()
    {
        // Arrange
        var request = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "My favorite color is blue." }]
                },
                new()
                {
                    Role = "model",
                    Parts = [new() { Text = "That's nice! Blue is a calming color." }]
                },
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "What did I just say my favorite color was?" }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 100,
                Temperature = 0.1f,
                ThinkingConfig = new ThinkingConfig
                {
                    IncludeThoughts = false // Disable thinking for predictable test results
                }
            }
        };

        // Act
        var response = await _geminiClient!.GenerateContentAsync(request);

        // Log response for debugging
        TestContext.WriteLine($"Success: {response.success}");
        TestContext.WriteLine($"Result: {response.result ?? "<NULL>"}");

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(response.success, Is.True, "API call should succeed");
            Assert.That(response.result, Is.Not.Null.And.Not.Empty, "Response should not be empty");
        });
        Assert.That(response.result.ToLower(), Does.Contain("blue"), "Response should recall the favorite color");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var request = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "Write a very long story about a dragon." }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 5000
            }
        };

        var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await _geminiClient!.GenerateContentAsync(request, cancellationToken: cts.Token);
        });
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithTemperatureControl_AffectsCreativity()
    {
        // Arrange - Same prompt with different temperatures
        var baseRequest = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "Complete this sentence: The cat sat on the" }]
                }
            ]
        };

        var lowTempRequest = new GeminiRequest
        {
            Contents = baseRequest.Contents,
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 20,
                Temperature = 0.0f,
                ThinkingConfig = new ThinkingConfig
                {
                    IncludeThoughts = false // Disable thinking for predictable test results
                }
            }
        };

        var highTempRequest = new GeminiRequest
        {
            Contents = baseRequest.Contents,
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 20,
                Temperature = 2.0f,
                ThinkingConfig = new ThinkingConfig
                {
                    IncludeThoughts = false // Disable thinking for predictable test results
                }
            }
        };

        // Act
        var lowTempResponse = await _geminiClient!.GenerateContentAsync(lowTempRequest);
        var highTempResponse = await _geminiClient!.GenerateContentAsync(highTempRequest);

        // Log responses for debugging
        TestContext.WriteLine($"Low temp - Success: {lowTempResponse.success}, Result: {lowTempResponse.result ?? "<NULL>"}");
        TestContext.WriteLine($"High temp - Success: {highTempResponse.success}, Result: {highTempResponse.result ?? "<NULL>"}");

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(lowTempResponse.success, Is.True, "Low temperature request should succeed");
            Assert.That(highTempResponse.success, Is.True, "High temperature request should succeed");
            Assert.That(lowTempResponse.result, Is.Not.Null.And.Not.Empty, "Low temp response should not be empty");
            Assert.That(highTempResponse.result, Is.Not.Null.And.Not.Empty, "High temp response should not be empty");
        });

        // Responses should be different (though not guaranteed)
        // This test mainly verifies both temperature settings work
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GenerateContentAsync_WithMaxTokenLimit_RespectsLimit()
    {
        // Arrange
        var request = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "Write a long essay about artificial intelligence." }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 50, // Very small limit
                ThinkingConfig = new ThinkingConfig
                {
                    IncludeThoughts = false // Disable thinking for predictable test results
                }
            }
        };

        // Act
        var response = await _geminiClient!.GenerateContentAsync(request);

        // Log response for debugging
        TestContext.WriteLine($"Success: {response.success}");
        TestContext.WriteLine($"Result: {response.result ?? "<NULL>"}");

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(response.success, Is.True, "API call should succeed");
            Assert.That(response.result, Is.Not.Null.And.Not.Empty, "Response should not be empty");
        });

        // Response should be relatively short due to token limit
        var wordCount = response.result.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.That(wordCount, Is.LessThan(100), "Response should be constrained by token limit");
    }

    #endregion GenerateContentAsync Tests

    #region BatchGenerateContentAsync Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    [Category("Slow")]
    public async Task BatchGenerateContentAsync_SingleRequest_ReturnsOperation()
    {
        // Arrange
        var request = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "What is 2 + 2?" }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 50,
                ThinkingConfig = new ThinkingConfig
                {
                    ThinkingLevel = "LOW",
                    IncludeThoughts = false // Disable thinking for predictable test results
                }
            }
        };

        var requests = new List<(GeminiRequest request, Dictionary<string, object> metadata)>
        {
            (request, new Dictionary<string, object> { ["testId"] = "test1" })
        };

        var displayName = $"Test_Batch_{DateTime.UtcNow:yyyyMMddHHmmss}";

        // Act
        var operation = await _geminiClient!.BatchGenerateContentAsync(requests, displayName);

        for (var i = 0; i < 15; i++)
        {
            // Log operation details for debugging
            TestContext.WriteLine($"Operation Name: {operation?.Name ?? "<NULL>"}");
            TestContext.WriteLine($"Operation Done: {operation?.Done}");
            if (operation?.Done == true)
            {
                TestContext.WriteLine("Operation completed: " + operation.Response.ToJson());
                break;
            }
            await Task.Delay(1000);
            operation = await _geminiClient!.GetBatchOperationAsync(operation.Name);
        }

        // Assert
        Assert.That(operation, Is.Not.Null, "Operation should not be null");
        Assert.Multiple(() =>
        {
            Assert.That(operation.Done, Is.True, "Operation should be completed");
            Assert.That(operation.Name, Is.Not.Null.And.Not.Empty, "Operation name should not be empty");
        });
        Assert.That(operation.Name, Does.Contain("batches/"), "Operation name should contain 'batches/'");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    [Category("Slow")]
    public async Task BatchGenerateContentAsync_MultipleRequests_ReturnsOperation()
    {
        // Arrange
        var requests = new List<(GeminiRequest request, Dictionary<string, object> metadata)>
        {
            (new GeminiRequest
            {
                Contents =
                [
                    new()
                    {
                        Role = "user",
                        Parts = [new() { Text = "What is the capital of France?" }]
                    }
                ],
                GenerationConfig = new GenerationConfig
                {
                    MaxOutputTokens = 50,
                    ThinkingConfig = new ThinkingConfig { IncludeThoughts = false }
                }
            }, new Dictionary<string, object> { ["questionId"] = "1" }),

            (new GeminiRequest
            {
                Contents =
                [
                    new()
                    {
                        Role = "user",
                        Parts = [new() { Text = "What is the capital of Germany?" }]
                    }
                ],
                GenerationConfig = new GenerationConfig
                {
                    MaxOutputTokens = 50,
                    ThinkingConfig = new ThinkingConfig { IncludeThoughts = false }
                }
            }, new Dictionary<string, object> { ["questionId"] = "2" }),

            (new GeminiRequest
            {
                Contents =
                [
                    new()
                    {
                        Role = "user",
                        Parts = [new() { Text = "What is the capital of Italy?" }]
                    }
                ],
                GenerationConfig = new GenerationConfig
                {
                    MaxOutputTokens = 50,
                    ThinkingConfig = new ThinkingConfig { IncludeThoughts = false }
                }
            }, new Dictionary<string, object> { ["questionId"] = "3" })
        };

        var displayName = $"Test_MultiBatch_{DateTime.UtcNow:yyyyMMddHHmmss}";

        // Act
        var operation = await _geminiClient!.BatchGenerateContentAsync(requests, displayName);

        // Log operation details for debugging
        TestContext.WriteLine($"Operation Name: {operation?.Name ?? "<NULL>"}");
        TestContext.WriteLine($"Operation Done: {operation?.Done}");

        // Assert
        Assert.That(operation, Is.Not.Null, "Operation should not be null");
        Assert.That(operation.Name, Is.Not.Null.And.Not.Empty, "Operation name should not be empty");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task BatchGenerateContentAsync_EmptyRequests_ThrowsArgumentException()
    {
        // Arrange
        var emptyRequests = new List<(GeminiRequest request, Dictionary<string, object> metadata)>();
        var displayName = "Test_Empty_Batch";

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _geminiClient!.BatchGenerateContentAsync(emptyRequests, displayName);
        });

        Assert.That(ex.Message, Does.Contain("request"), "Exception message should mention requests");
    }

    #endregion BatchGenerateContentAsync Tests

    #region GetBatchOperationAsync Tests

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    [Category("Slow")]
    public async Task GetBatchOperationAsync_ValidOperation_ReturnsStatus()
    {
        // Arrange - First create a batch operation
        var request = new GeminiRequest
        {
            Contents =
            [
                new()
                {
                    Role = "user",
                    Parts = [new() { Text = "Say hello" }]
                }
            ],
            GenerationConfig = new GenerationConfig
            {
                MaxOutputTokens = 50,
                ThinkingConfig = new ThinkingConfig { IncludeThoughts = false }
            }
        };

        var requests = new List<(GeminiRequest request, Dictionary<string, object> metadata)>
        {
            (request, new Dictionary<string, object> { ["testId"] = "status_test" })
        };

        var displayName = $"Test_Status_{DateTime.UtcNow:yyyyMMddHHmmss}";
        var batchOperation = await _geminiClient!.BatchGenerateContentAsync(requests, displayName);

        // Act - Poll for the operation status
        var statusOperation = await _geminiClient!.GetBatchOperationAsync(batchOperation.Name);

        // Log status for debugging
        TestContext.WriteLine($"Original Operation Name: {batchOperation.Name}");
        TestContext.WriteLine($"Status Operation Name: {statusOperation?.Name ?? "<NULL>"}");
        TestContext.WriteLine($"Status Operation Done: {statusOperation?.Done}");
        TestContext.WriteLine($"Status Operation State: {statusOperation?.Response?.State ?? "<NULL>"}");

        // Assert
        Assert.That(statusOperation, Is.Not.Null, "Status operation should not be null");
        Assert.That(statusOperation.Name, Is.EqualTo(batchOperation.Name), "Operation names should match");
    }

    [Test]
    [Category("Integration")]
    [Category("ExternalAPI")]
    public async Task GetBatchOperationAsync_EmptyOperationName_ThrowsArgumentException()
    {
        // Arrange
        var emptyOperationName = string.Empty;

        // Act & Assert
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await _geminiClient!.GetBatchOperationAsync(emptyOperationName);
        });

        Assert.That(ex.Message, Does.Contain("Operation name"), "Exception message should mention operation name");
    }

    #endregion GetBatchOperationAsync Tests
}