using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController(IProfileService profileService, ILogger<ProfilesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProfileResponse>>> GetAll()
    {
        try
        {
            var profiles = await profileService.GetAllProfilesAsync();
            return Ok(profiles);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting all profiles");
            return StatusCode(500, "Error retrieving profiles");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Profile>> Create([FromBody] Profile profile)
    {
        try
        {
            var created = await profileService.CreateProfileAsync(profile);
            return Ok(created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating profile");
            return StatusCode(500, "Error creating profile");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Profile>> Update(int id, [FromBody] Profile profile)
    {
        try
        {
            if (id != profile.Id)
            {
                return BadRequest("ID mismatch");
            }

            var updated = await profileService.UpdateProfileAsync(profile);
            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating profile {ProfileId}", id);
            return StatusCode(500, "Error updating profile");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await profileService.DeleteProfileAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting profile {ProfileId}", id);
            return StatusCode(500, "Error deleting profile");
        }
    }

    [HttpPost("{id}/activate")]
    public async Task<ActionResult<Profile>> Activate(int id)
    {
        try
        {
            var profile = await profileService.ActivateProfileAsync(id);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error activating profile {ProfileId}", id);
            return StatusCode(500, "Error activating profile");
        }
    }

    [HttpPost("{id}/duplicate")]
    public async Task<ActionResult<Profile>> Duplicate(int id, [FromBody] DuplicateProfileRequest request)
    {
        try
        {
            var profile = await profileService.DuplicateProfileAsync(id, request.NewName);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error duplicating profile {ProfileId}", id);
            return StatusCode(500, "Error duplicating profile");
        }
    }

    [HttpPost("{id}/move-entities")]
    public async Task<ActionResult<int>> MoveEntities(int id, [FromBody] MoveEntitiesRequest request)
    {
        try
        {
            var count = await profileService.MoveEntitiesToProfileAsync(
                id,
                request.SystemMessageIds);
            return Ok(new { movedCount = count });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error moving entities to profile {ProfileId}", id);
            return StatusCode(500, "Error moving entities");
        }
    }

    [HttpPost("create-default")]
    public async Task<ActionResult<Profile>> CreateDefault([FromBody] CreateDefaultProfileRequest? request = null)
    {
        try
        {
            var name = request?.Name ?? "Default Profile";
            var profile = await profileService.CreateDefaultProfileAsync(name);
            return Ok(profile);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating default profile");
            return StatusCode(500, "Error creating default profile");
        }
    }

    public record DuplicateProfileRequest(string NewName);

    public record MoveEntitiesRequest(
        List<int>? ContextTriggerIds = null,
        List<int>? SystemMessageIds = null
    );

    public record CreateDefaultProfileRequest(string? Name = null);
}