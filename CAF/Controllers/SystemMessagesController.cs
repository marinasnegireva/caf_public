using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SystemMessagesController(
    ISystemMessageService systemMessageService,
    IProfileService profileService,
    ILogger<SystemMessagesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SystemMessage>>> GetAll(
        [FromQuery] SystemMessage.SystemMessageType? type = null,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var messages = await systemMessageService.GetAllAsync(type, includeArchived, cancellationToken);
        return Ok(messages);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SystemMessage>> GetById(int id, CancellationToken cancellationToken = default)
    {
        var message = await systemMessageService.GetByIdAsync(id, cancellationToken);
        return message == null ? (ActionResult<SystemMessage>)NotFound() : (ActionResult<SystemMessage>)Ok(message);
    }

    [HttpPost]
    public async Task<ActionResult<SystemMessage>> Create([FromBody] SystemMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var created = await systemMessageService.CreateAsync(message, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating system message");
            return StatusCode(500, new { error = "An error occurred while creating system message" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<SystemMessage>> Update(int id, [FromBody] SystemMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var updated = await systemMessageService.UpdateAsync(id, message, cancellationToken);
            return updated == null ? (ActionResult<SystemMessage>)NotFound() : (ActionResult<SystemMessage>)Ok(updated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating system message {Id}", id);
            return StatusCode(500, new { error = "An error occurred while updating system message" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await systemMessageService.DeleteAsync(id, cancellationToken);
        return !deleted ? NotFound() : NoContent();
    }

    [HttpPost("{id}/version")]
    public async Task<ActionResult<SystemMessage>> CreateNewVersion(int id, [FromBody] CreateVersionRequest? request = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var newVersion = await systemMessageService.CreateNewVersionAsync(id, request?.ModifiedBy, cancellationToken);
            return newVersion is null ? (ActionResult<SystemMessage>)NotFound() : (ActionResult<SystemMessage>)CreatedAtAction(nameof(GetById), new { id = newVersion.Id }, newVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating version for system message {Id}", id);
            return StatusCode(500, new { error = "An error occurred while creating version" });
        }
    }

    [HttpGet("{id}/versions")]
    public async Task<ActionResult<List<SystemMessage>>> GetVersionHistory(int id, CancellationToken cancellationToken = default)
    {
        var versions = await systemMessageService.GetVersionHistoryAsync(id, cancellationToken);
        return Ok(versions);
    }

    [HttpPost("{id}/activate")]
    public async Task<ActionResult> SetActiveVersion(int id, CancellationToken cancellationToken = default)
    {
        var success = await systemMessageService.SetActiveVersionAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    [HttpGet("active/persona")]
    public async Task<ActionResult<SystemMessage>> GetActivePersona(CancellationToken cancellationToken = default)
    {
        var persona = await systemMessageService.GetActivePersonaAsync(cancellationToken);
        return persona == null ? (ActionResult<SystemMessage>)NotFound(new { error = "No active persona found" }) : (ActionResult<SystemMessage>)Ok(persona);
    }

    [HttpGet("active/perceptions")]
    public async Task<ActionResult<List<SystemMessage>>> GetActivePerceptions(CancellationToken cancellationToken = default)
    {
        var perceptions = await systemMessageService.GetActivePerceptionsAsync(cancellationToken);
        return Ok(perceptions);
    }

    [HttpGet("active/technical")]
    public async Task<ActionResult<List<SystemMessage>>> GetActiveTechnical(CancellationToken cancellationToken = default)
    {
        var technical = await systemMessageService.GetActiveTechnicalMessagesAsync(cancellationToken);
        return Ok(technical);
    }

    [HttpGet("build")]
    public async Task<ActionResult<BuildSystemMessageResponse>> BuildCompleteSystemMessage([FromQuery] int? personaId = null, CancellationToken cancellationToken = default)
    {
        var systemMessage = await systemMessageService.BuildCompleteSystemMessageAsync(personaId, cancellationToken);
        return Ok(new BuildSystemMessageResponse { SystemMessage = systemMessage });
    }

    [HttpGet("preview")]
    public async Task<ActionResult<PreviewResponse>> GetPreview(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = new List<PreviewItem>();
            var parts = new List<string>();

            var persona = await systemMessageService.GetActivePersonaAsync(cancellationToken);

            if (persona != null)
            {
                var personaContent = $"# PERSONA\n{persona.Content}";
                var personaTokens = await systemMessageService.CalculateTokenCountAsync(personaContent, cancellationToken);
                parts.Add(personaContent);
                items.Add(new PreviewItem
                {
                    Name = persona.Name,
                    Type = "Persona",
                    TokenCount = personaTokens
                });
            }

            var perceptions = await systemMessageService.GetActivePerceptionsAsync(cancellationToken);
            foreach (var perception in perceptions)
            {
                var perceptionContent = $"# PERCEPTION\n{perception.Content}";
                var perceptionTokens = await systemMessageService.CalculateTokenCountAsync(perceptionContent, cancellationToken);
                parts.Add(perceptionContent);
                items.Add(new PreviewItem
                {
                    Name = perception.Name,
                    Type = "Perception",
                    TokenCount = perceptionTokens
                });
            }

            var technicals = await systemMessageService.GetActiveTechnicalMessagesAsync(cancellationToken);
            foreach (var technical in technicals)
            {
                var technicalContent = $"# TECHNICAL: {technical.Name}\n{technical.Content}";
                var technicalTokens = await systemMessageService.CalculateTokenCountAsync(technicalContent, cancellationToken);
                parts.Add(technicalContent);
                items.Add(new PreviewItem
                {
                    Name = technical.Name,
                    Type = "Technical",
                    TokenCount = technicalTokens
                });
            }

            var systemMessage = string.Join("\n\n---\n\n", parts);
            var tokenCount = await systemMessageService.CalculateTokenCountAsync(systemMessage, cancellationToken);

            return Ok(new PreviewResponse
            {
                CompleteMessage = systemMessage,
                HasPersona = persona != null,
                PerceptionCount = perceptions.Count,
                HasTechnical = technicals.Count > 0,
                ContextCount = 0,
                TokenCount = tokenCount,
                Items = items
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating preview");
            return StatusCode(500, new { error = "An error occurred while generating preview" });
        }
    }

    [HttpPost("{id}/archive")]
    public async Task<ActionResult> Archive(int id, CancellationToken cancellationToken = default)
    {
        var success = await systemMessageService.ArchiveAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult> Restore(int id, CancellationToken cancellationToken = default)
    {
        var success = await systemMessageService.RestoreAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }

    /// <summary>
    /// Initializes default technical messages if they don't exist.
    /// Creates messages from DefaultTechnicalMessages constants.
    /// </summary>
    [HttpPost("initialize-defaults")]
    public async Task<ActionResult<InitializeDefaultsResponse>> InitializeDefaults(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new InitializeDefaultsResponse();

            var activeProfileId = await profileService.GetActiveProfileIdAsync();
            if (activeProfileId == 0)
            {
                return BadRequest(new { error = "No active profile found. Please activate a profile first." });
            }

            logger.LogInformation("Initializing default technical messages for profile ID: {ProfileId}", activeProfileId);

            var existingTechnical = await systemMessageService.GetAllAsync(
                SystemMessage.SystemMessageType.Technical,
                includeArchived: false,
                cancellationToken);

            var existingNames = existingTechnical
                .Select(t => t.Name.ToLowerInvariant())
                .ToHashSet();

            var defaults = GetDefaultTechnicalMessages();

            foreach (var (name, content, description) in defaults)
            {
                var normalizedName = name.ToLowerInvariant();

                if (existingNames.Contains(normalizedName))
                {
                    result.Skipped.Add(new InitializeResult
                    {
                        Name = name,
                        Message = "Already exists"
                    });
                    continue;
                }

                try
                {
                    var message = new SystemMessage
                    {
                        Name = name,
                        Content = content,
                        Description = description,
                        Type = SystemMessage.SystemMessageType.Technical,
                        IsActive = true,
                        IsArchived = false,
                        Version = 1,
                        ProfileId = activeProfileId,
                        CreatedAt = DateTime.UtcNow,
                        ModifiedAt = DateTime.UtcNow,
                        ModifiedBy = "System"
                    };

                    var created = await systemMessageService.CreateAsync(message, cancellationToken);

                    result.Created.Add(new InitializeResult
                    {
                        Name = name,
                        Id = created.Id,
                        Message = "Successfully created"
                    });

                    logger.LogInformation("Created default technical message: {Name} (ID: {Id}) for profile {ProfileId}",
                        name, created.Id, activeProfileId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error creating default technical message: {Name}", name);
                    result.Errors.Add(new InitializeResult
                    {
                        Name = name,
                        Message = $"Error: {ex.Message}"
                    });
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing default technical messages");
            return StatusCode(500, new { error = $"An error occurred: {ex.Message}" });
        }
    }

    private static List<(string Name, string Content, string Description)> GetDefaultTechnicalMessages()
    {
        return
        [
            (
                ConversationConstants.TechnicalMessages.TurnStripperInstructions,
                DefaultTechnicalMessages.TurnStripperInstructions,
                "Instructions for stripping turns to physical actions and dialogue only"
            ),
            (
                ConversationConstants.TechnicalMessages.MemorySummaryInstructions,
                DefaultTechnicalMessages.MemorySummaryInstructions,
                "Instructions for creating concise summaries of narrative memories"
            ),
            (
                ConversationConstants.TechnicalMessages.MemoryCoreFactsInstructions,
                DefaultTechnicalMessages.MemoryCoreFactsInstructions,
                "Instructions for extracting objective facts from memory data"
            ),
            (
                ConversationConstants.TechnicalMessages.QuoteQueryTransformer,
                DefaultTechnicalMessages.QuoteQueryTransformer,
                "Instructions for generating search queries for quote vector database"
            ),
            (
                ConversationConstants.TechnicalMessages.QuoteMapper,
                DefaultTechnicalMessages.QuoteMapper,
                "Instructions for extracting structured metadata from roleplay quotes"
            )
        ];
    }
}