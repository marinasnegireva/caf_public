using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlagsController(IFlagService flagService, ILogger<FlagsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Flag>>> GetAll(
        [FromQuery] bool? active = null,
        CancellationToken cancellationToken = default)
    {
        var flags = await flagService.GetAllAsync(active, cancellationToken);
        return Ok(flags);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Flag>> GetById(int id, CancellationToken cancellationToken = default)
    {
        var flag = await flagService.GetByIdAsync(id, cancellationToken);
        return flag == null ? (ActionResult<Flag>)NotFound() : (ActionResult<Flag>)Ok(flag);
    }

    [HttpPost]
    public async Task<ActionResult<Flag>> Create([FromBody] CreateFlagRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var flag = await flagService.CreateAsync(request.Value, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = flag.Id }, flag);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating flag");
            return StatusCode(500, new { error = "An error occurred while creating the flag" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Flag>> Update(int id, [FromBody] UpdateFlagRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var flag = await flagService.UpdateAsync(id, request.Value, cancellationToken);
            return flag == null ? (ActionResult<Flag>)NotFound() : (ActionResult<Flag>)Ok(flag);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating flag {Id}", id);
            return StatusCode(500, new { error = "An error occurred while updating the flag" });
        }
    }

    [HttpPost("{id}/toggle-active")]
    public async Task<ActionResult<Flag>> ToggleActive(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var flag = await flagService.ToggleActiveAsync(id, cancellationToken);
            return flag == null ? (ActionResult<Flag>)NotFound() : (ActionResult<Flag>)Ok(flag);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling active for flag {Id}", id);
            return StatusCode(500, new { error = "An error occurred while toggling active" });
        }
    }

    [HttpPost("{id}/toggle-constant")]
    public async Task<ActionResult<Flag>> ToggleConstant(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var flag = await flagService.ToggleConstantAsync(id, cancellationToken);
            return flag == null ? (ActionResult<Flag>)NotFound() : (ActionResult<Flag>)Ok(flag);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error toggling constant for flag {Id}", id);
            return StatusCode(500, new { error = "An error occurred while toggling constant" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken = default)
    {
        var success = await flagService.DeleteAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }
}

public record CreateFlagRequest(string Value);
public record UpdateFlagRequest(string Value);