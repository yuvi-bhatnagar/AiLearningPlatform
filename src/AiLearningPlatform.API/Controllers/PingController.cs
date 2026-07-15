using Microsoft.AspNetCore.Mvc;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Route("[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Pong");
    }
}
