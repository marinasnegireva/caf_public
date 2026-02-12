using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController(ISessionService sessionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Session>>> GetAll()
    {
        var sessions = await sessionService.GetAllSessionsAsync();
        return Ok(sessions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Session>> GetById(int id)
    {
        var session = await sessionService.GetByIdAsync(id);
        return session == null ? (ActionResult<Session>)NotFound() : (ActionResult<Session>)Ok(session);
    }

    [HttpGet("active")]
    public async Task<ActionResult<Session>> GetActive()
    {
        var session = await sessionService.GetActiveSessionAsync();
        return session == null ? (ActionResult<Session>)NotFound("No active session found") : (ActionResult<Session>)Ok(session);
    }

    [HttpPost]
    public async Task<ActionResult<Session>> Create([FromBody] CreateSessionRequest request)
    {
        var session = request.SourceSessionId.HasValue && request.TurnCount.HasValue
            ? await sessionService.CreateSessionWithDuplicateTurnsAsync(
                request.Name,
                request.SourceSessionId.Value,
                request.TurnCount.Value)
            : await sessionService.CreateSessionAsync(request.Name);

        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Session>> Update(int id, [FromBody] UpdateSessionRequest request)
    {
        try
        {
            var session = await sessionService.UpdateSessionAsync(id, request.Name, request.IsActive);
            return Ok(session);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id}/activate")]
    public async Task<ActionResult> Activate(int id)
    {
        var success = await sessionService.SetActiveSessionAsync(id);
        return !success ? NotFound() : Ok();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var success = await sessionService.DeleteSessionAsync(id);
        return !success ? NotFound() : NoContent();
    }
}

public record CreateSessionRequest(string Name, int? SourceSessionId = null, int? TurnCount = null);
public record UpdateSessionRequest(string Name, bool IsActive);