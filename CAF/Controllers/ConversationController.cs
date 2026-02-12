using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationController(
    IConversationPipeline pipeline,
    ITurnService turnService,
    IConversationContextBuilder contextBuilder,
    ISessionService sessionService,
    ITurnStripperService turnStripper,
    IDbContextFactory<GeneralDbContext> dbFactory,
    IOptions<GeminiOptions> options,
    ILLMProviderFactory llmProviderFactory) : ControllerBase
{
    private static JsonResult OkJson(object? value)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return new JsonResult(value, options)
        {
            StatusCode = StatusCodes.Status200OK
        };
    }

    [HttpPost]
    public async Task<ActionResult<Turn>> ProcessInput([FromBody] ConversationRequest request)
    {
        try
        {
            var turn = await pipeline.ProcessInputAsync(request.Input);
            return OkJson(turn);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("turns/{sessionId}")]
    public async Task<ActionResult<List<Turn>>> GetTurnsBySession(int sessionId)
    {
        var turns = await turnService.GetTurnsBySessionIdAsync(sessionId);
        return OkJson(turns);
    }

    [HttpPut("turns/{id}/reject")]
    public async Task<ActionResult<Turn>> RejectTurn(int id)
    {
        try
        {
            var turn = await turnService.UpdateTurnAsync(id, accepted: false);
            return OkJson(turn);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("turns/{id}/restrip")]
    public async Task<ActionResult<Turn>> RestripTurn(int id, [FromQuery] string? model = null, CancellationToken cancellationToken = default)
    {
        var turn = await turnService.GetByIdAsync(id);
        if (turn == null)
            return NotFound();

        var session = await sessionService.GetActiveSessionAsync();
        session ??= turn.Session;
        if (session == null)
            return BadRequest("No session found for turn.");

        var context = await contextBuilder.BuildContextAsync(turn, session, cancellationToken);

        // Clear strippedTurn
        await turnService.UpdateTurnAsync(turn.Id, strippedTurn: "");

        // Refresh entity
        turn = await turnService.GetByIdAsync(id) ?? turn;
        context.CurrentTurn = turn;

        await turnStripper.StripAndSaveTurnAsync(turn, context, cancellationToken, model);

        // Refresh after stripping
        turn = await turnService.GetByIdAsync(id) ?? turn;
        return OkJson(turn);
    }

    [HttpPut("turns/{id}/response")]
    public async Task<ActionResult<Turn>> UpdateResponse(int id, [FromBody] UpdateResponseRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Response))
            return BadRequest("Response cannot be empty");

        var turn = await turnService.GetByIdAsync(id);
        if (turn == null)
            return NotFound();

        // Persist new response and clear strippedTurn
        turn = await turnService.UpdateTurnAsync(id, response: request.Response, strippedTurn: "");

        var session = await sessionService.GetByIdAsync(turn.SessionId) ?? await sessionService.GetActiveSessionAsync();
        if (session == null)
            return OkJson(turn);

        var context = await contextBuilder.BuildContextAsync(turn, session, cancellationToken);
        context.CurrentTurn = turn;

        // Re-strip turn after edit
        await turnStripper.StripAndSaveTurnAsync(turn, context, cancellationToken);

        // Refresh after stripping
        turn = await turnService.GetByIdAsync(id) ?? turn;
        return OkJson(turn);
    }

    [HttpGet("sessions/{sessionId}/last-turn-tokens")]
    public async Task<ActionResult<object>> GetLastTurnTokenCount(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Get the last accepted turn for this session
        var lastTurn = await db.Turns
            .Where(t => t.SessionId == sessionId && t.Accepted)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastTurn == null)
            return OkJson(new { inputTokens = 0 });

        // Get the LLM request log for this turn
        var log = await db.LLMRequestLogs
            .Where(l => l.TurnId == lastTurn.Id && l.Operation == "GenerateContent" && l.Model != options.Value.TechnicalModel)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        return OkJson(new { inputTokens = log?.TotalTokens ?? 0 });
    }

    [HttpPut("turns/{id}/input")]
    public async Task<ActionResult<Turn>> UpdateInput(int id, [FromBody] UpdateInputRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Input))
            return BadRequest("Input cannot be empty");

        var turn = await turnService.GetByIdAsync(id);
        if (turn == null)
            return NotFound();

        // Persist new input
        turn = await turnService.UpdateTurnAsync(id, input: request.Input);

        var session = await sessionService.GetByIdAsync(turn.SessionId) ?? await sessionService.GetActiveSessionAsync();
        if (session == null)
            return OkJson(turn);

        // Refresh after update
        turn = await turnService.GetByIdAsync(id) ?? turn;
        return OkJson(turn);
    }

    [HttpPut("turns/{id}/stripped")]
    public async Task<ActionResult<Turn>> UpdateStripped(int id, [FromBody] UpdateStrippedRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Stripped))
            return BadRequest("Stripped content cannot be empty");

        var turn = await turnService.GetByIdAsync(id);
        if (turn == null)
            return NotFound();

        // Persist new stripped content
        turn = await turnService.UpdateTurnAsync(id, strippedTurn: request.Stripped);

        // Refresh after update
        turn = await turnService.GetByIdAsync(id) ?? turn;
        return OkJson(turn);
    }

    [HttpPost("sessions/{sessionId}/clear-all-stripped")]
    public async Task<ActionResult> ClearAllStripped(int sessionId)
    {
        var session = await sessionService.GetByIdAsync(sessionId);
        if (session == null)
            return NotFound();

        // Clear strippedTurn fields for all accepted turns
        var allTurns = await turnService.GetTurnsBySessionIdAsync(sessionId);
        foreach (var t in allTurns.Where(t => t.Accepted))
        {
            await turnService.UpdateTurnAsync(t.Id, strippedTurn: "");
        }

        return OkJson(new { success = true });
    }

    [HttpPost("debug")]
    public async Task<ActionResult<DebugResponse>> DebugPipeline([FromBody] ConversationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the pipeline's BuildRequestAsync method to go through all steps except LLM execution
            var (state, turn) = await pipeline.BuildRequestAsync(request.Input, cancellationToken);

            try
            {
                // Get provider name for response
                var provider = await llmProviderFactory.GetProviderAsync(cancellationToken);

                // Collect all loaded ContextData items with their token counts
                var loadedItems = new List<ContextData>();

                if (state.UserProfile != null)
                    loadedItems.Add(state.UserProfile);

                loadedItems.AddRange(state.Quotes);
                loadedItems.AddRange(state.PersonaVoiceSamples);
                loadedItems.AddRange(state.Memories);
                loadedItems.AddRange(state.Insights);
                loadedItems.AddRange(state.CharacterProfiles);
                loadedItems.AddRange(state.Data);

                var loadedItemsInfo = loadedItems
                    .Select(item => new
                    {
                        item.Id,
                        item.Name,
                        DisplayContent = item.GetDisplayContent(), // Add formatted display content
                        item.Type,
                        item.Availability,
                        item.TokenCount,
                        ContentLength = item.Content.Length
                    })
                    .Cast<object>()
                    .ToList();

                var totalTokens = loadedItems.Where(i => i.TokenCount.HasValue).Sum(i => i.TokenCount!.Value);
                var itemsWithTokens = loadedItems.Count(i => i.TokenCount.HasValue);
                var itemsWithoutTokens = loadedItems.Count(i => !i.TokenCount.HasValue);

                // Build the debug response
                var response = new DebugResponse
                {
                    ProviderName = provider.ProviderName,
                    State = new DebugConversationState
                    {
                        UserName = state.UserName,
                        PersonaName = state.PersonaName,
                        IsOOCRequest = state.IsOOCRequest,
                        PersonaName_SystemMessage = state.Persona?.Name,
                        PersonaContextCount = state.CharacterProfiles.Count,
                        TriggeredContextCount = 0, // Deprecated field
                        AlwaysOnMemoryCount = state.Memories.Count(m => m.Availability == AvailabilityType.AlwaysOn),
                        FlagCount = state.Flags.Count,
                        RecentTurnCount = state.RecentTurns.Count,
                        PerceptionCount = state.Perceptions.Count,
                        DynamicQuoteCount = state.Quotes.Count,
                        CanonQuoteCount = 0, // Not tracked separately
                        DialogueLogLength = state.DialogueLog?.Length ?? 0,
                        RecentContextLength = state.RecentContext?.Length ?? 0
                    },
                    LoadedContextData = new LoadedContextDataInfo
                    {
                        Items = loadedItemsInfo,
                        Summary = new
                        {
                            TotalItems = loadedItems.Count,
                            ItemsWithTokens = itemsWithTokens,
                            ItemsWithoutTokens = itemsWithoutTokens,
                            TotalTokens = totalTokens
                        }
                    }
                };

                if (provider.ProviderName.Equals("Claude", StringComparison.OrdinalIgnoreCase))
                {
                    response.ClaudeRequest = state.ClaudeRequest;
                }
                else
                {
                    response.GeminiRequest = state.GeminiRequest;
                }

                // Delete the temporary turn
                await using var db = await dbFactory.CreateDbContextAsync();
                var turnEntity = await db.Turns.FindAsync(turn.Id);
                if (turnEntity != null)
                {
                    db.Turns.Remove(turnEntity);
                    await db.SaveChangesAsync(cancellationToken);
                }

                return OkJson(response);
            }
            catch (Exception)
            {
                // Clean up turn on error
                await using var db = await dbFactory.CreateDbContextAsync();
                var turnEntity = await db.Turns.FindAsync(turn.Id);
                if (turnEntity != null)
                {
                    db.Turns.Remove(turnEntity);
                    await db.SaveChangesAsync(cancellationToken);
                }
                throw;
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
        }
    }

    [HttpGet("debug/profile-info")]
    public async Task<ActionResult<object>> GetProfileDebugInfo()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Get active profile
        var activeProfile = await db.Profiles.FirstOrDefaultAsync(p => p.IsActive);

        // Get all user profiles
        var userProfiles = await db.ContextData
            .Where(d => d.Type == DataType.CharacterProfile && d.IsUser)
            .Select(d => new
            {
                d.Id,
                d.ProfileId,
                d.Name,
                d.IsUser,
                d.IsEnabled,
                d.IsArchived,
                d.Availability
            })
            .ToListAsync();

        return OkJson(new
        {
            activeProfile = activeProfile == null ? null : new
            {
                activeProfile.Id,
                activeProfile.Name,
                activeProfile.IsActive
            },
            userProfiles = userProfiles
        });
    }

    [HttpGet("debug/context-token-counts")]
    public async Task<ActionResult<object>> GetContextTokenCounts()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Get all active, non-archived context data with their token counts
        var contextData = await db.ContextData
            .Where(d => !d.IsArchived && d.IsEnabled)
            .Select(d => new
            {
                d.Id,
                d.Name,
                DisplayContent = d.GetDisplayContent(), // Add formatted display content
                d.Type,
                d.Availability,
                d.TokenCount,
                ContentLength = d.Content.Length
            })
            .OrderBy(d => d.Type)
            .ThenBy(d => d.Name)
            .ToListAsync();

        var totalTokens = contextData.Where(d => d.TokenCount.HasValue).Sum(d => d.TokenCount!.Value);
        var itemsWithTokens = contextData.Count(d => d.TokenCount.HasValue);
        var itemsWithoutTokens = contextData.Count(d => !d.TokenCount.HasValue);

        return OkJson(new
        {
            items = contextData,
            summary = new
            {
                totalItems = contextData.Count,
                itemsWithTokens,
                itemsWithoutTokens,
                totalTokens
            }
        });
    }
}

public record ConversationRequest(string Input);

public record UpdateResponseRequest(string Response);

public record UpdateInputRequest(string Input);

public record UpdateStrippedRequest(string Stripped);

public class DebugResponse
{
    public string ProviderName { get; set; } = string.Empty;
    public DebugConversationState State { get; set; } = new();
    public LoadedContextDataInfo? LoadedContextData { get; set; }
    public GeminiRequest? GeminiRequest { get; set; }
    public ClaudeRequest? ClaudeRequest { get; set; }
}

public class LoadedContextDataInfo
{
    public List<object> Items { get; set; } = [];
    public object? Summary { get; set; }
}

public class DebugConversationState
{
    public string UserName { get; set; } = string.Empty;
    public string PersonaName { get; set; } = string.Empty;
    public bool IsOOCRequest { get; set; }
    public string? PersonaName_SystemMessage { get; set; }
    public int PersonaContextCount { get; set; }
    public int TriggeredContextCount { get; set; }
    public int AlwaysOnMemoryCount { get; set; }
    public int FlagCount { get; set; }
    public int RecentTurnCount { get; set; }
    public int PerceptionCount { get; set; }
    public int DynamicQuoteCount { get; set; }
    public int CanonQuoteCount { get; set; }
    public int DialogueLogLength { get; set; }
    public int RecentContextLength { get; set; }
}