using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController(ISettingService settingService, ILogger<SettingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Setting>>> GetAll(CancellationToken cancellationToken = default)
    {
        var settings = await settingService.GetAllAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Setting>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var setting = await settingService.GetByIdAsync(id, cancellationToken);
        return setting == null ? (ActionResult<Setting>)NotFound() : (ActionResult<Setting>)Ok(setting);
    }

    [HttpGet("by-name/{name}")]
    public async Task<ActionResult<Setting>> GetByName(string name, CancellationToken cancellationToken = default)
    {
        var key = SettingsKeysExtensions.FromKey(name);
        if (key == null)
        {
            return BadRequest(new { error = $"Invalid setting name: {name}" });
        }

        var setting = await settingService.GetByNameAsync(key.Value, cancellationToken);
        return setting == null ? (ActionResult<Setting>)NotFound() : (ActionResult<Setting>)Ok(setting);
    }

    [HttpGet("value/{name}")]
    public async Task<ActionResult<string>> GetValue(string name, CancellationToken cancellationToken = default)
    {
        var key = SettingsKeysExtensions.FromKey(name);
        if (key == null)
        {
            return BadRequest(new { error = $"Invalid setting name: {name}" });
        }

        var value = await settingService.GetValueAsync(key.Value, cancellationToken);
        return value == null ? (ActionResult<string>)NotFound() : (ActionResult<string>)Ok(new { value });
    }

    [HttpPost]
    public async Task<ActionResult<Setting>> CreateOrUpdate([FromBody] CreateOrUpdateSettingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = SettingsKeysExtensions.FromKey(request.Name);
            if (key == null)
            {
                return BadRequest(new { error = $"Invalid setting name: {request.Name}" });
            }

            var setting = await settingService.CreateOrUpdateAsync(key.Value, request.Value, cancellationToken);
            return Ok(setting);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating/updating setting");
            return StatusCode(500, new { error = "An error occurred while saving the setting" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(long id, CancellationToken cancellationToken = default)
    {
        var success = await settingService.DeleteAsync(id, cancellationToken);
        return !success ? NotFound() : NoContent();
    }
}

public record CreateOrUpdateSettingRequest(string Name, string Value);