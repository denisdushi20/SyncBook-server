using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SyncBook.Server.Services;

namespace SyncBook.Server.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize(Policy = "BusinessOwner")]
public class AnalyticsController : ControllerBase
{
    private readonly AnalyticsQueryService _analytics;
    private readonly CurrentUserService _currentUser;

    public AnalyticsController(AnalyticsQueryService analytics, CurrentUserService currentUser)
    {
        _analytics = analytics;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboard(
        [FromQuery] string? range = "month",
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        var businessId = _currentUser.BusinessId;
        if (string.IsNullOrEmpty(businessId))
        {
            return Forbid();
        }

        var dashboard = await _analytics.GetDashboardAsync(businessId, range ?? "month", year, month);
        return Ok(dashboard);
    }
}
