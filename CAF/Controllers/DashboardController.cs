using Microsoft.AspNetCore.Mvc;

namespace CAF.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(
    IFlagService flagService,
    ISessionService sessionService,
    ISettingService settingService,
    IProfileService profileService,
    ILogger<DashboardController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(CancellationToken cancellationToken = default)
    {
        try
        {
            // Get active profile
            var activeProfileId = await profileService.GetActiveProfileIdAsync();

            var activeFlags = await flagService.GetAllAsync(active: true, cancellationToken: cancellationToken);
            var activeSession = await sessionService.GetActiveSessionAsync();
            var settings = await settingService.GetAllAsync(cancellationToken: cancellationToken);

            var response = new DashboardResponse
            {
                ActiveFlags = activeFlags,
                ActiveSession = activeSession,
                Settings = settings,
                Statistics = new DashboardStatistics
                {
                    TotalFlags = activeFlags.Count,
                    ActiveFlags = activeFlags.Count(f => f.Active && !f.Constant),
                    ConstantFlags = activeFlags.Count(f => f.Constant),
                    TotalSettings = settings.Count
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading dashboard");
            return StatusCode(500, new { error = "An error occurred while loading the dashboard" });
        }
    }
}

public class DashboardResponse
{
    public List<Flag> ActiveFlags { get; set; } = [];
    public Session? ActiveSession { get; set; }
    public List<Setting> Settings { get; set; } = [];
    public DashboardStatistics Statistics { get; set; } = new();
}

public class DashboardStatistics
{
    public int TotalFlags { get; set; }
    public int ActiveFlags { get; set; }
    public int ConstantFlags { get; set; }
    public int TotalSettings { get; set; }
    public int ActiveContextData { get; set; }
}