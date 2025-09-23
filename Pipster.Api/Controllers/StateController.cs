using Microsoft.AspNetCore.Mvc;

namespace Pipster.Api.Controllers
{
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

        [HttpGet("health")]
        public IActionResult Get()
        {
            return Ok(new
            {
                _state.SignalsCount,
                _state.TradesCount
            });
        }
    }
}
