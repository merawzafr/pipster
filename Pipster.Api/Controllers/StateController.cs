using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Pipster.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class StateController : ControllerBase
{
    private readonly ILogger<StateController> _logger;
    private readonly PipsterState _state;

    public StateController(ILogger<StateController> logger, PipsterState state)
    {
        _logger = logger;
        _state = state;
    }

    /// <summary>
    /// Health check endpoint (anonymous access allowed)
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok(new
        {
            _state.SignalsCount,
            _state.TradesCount
        });
    }

    /// <summary>
    /// Gets detailed state information (requires authentication)
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetStatus()
    {
        var userId = User.FindFirst("sub")?.Value;
        var tenantId = User.FindFirst("tenant_id")?.Value;

        _logger.LogInformation(
            "Status requested by user {UserId} from tenant {TenantId}",
            userId, tenantId);

        return Ok(new
        {
            _state.SignalsCount,
            _state.TradesCount,
            Timestamp = DateTime.UtcNow,
            TenantId = tenantId,
            UserId = userId
        });
    }
}