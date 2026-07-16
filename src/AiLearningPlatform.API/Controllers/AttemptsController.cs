using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using AiLearningPlatform.Application.Features.Attempts;
using AiLearningPlatform.Application.Features.Attempts.DTOs;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AttemptsController : ControllerBase
{
    private readonly IAttemptService _attemptService;

    public AttemptsController(IAttemptService attemptService)
    {
        _attemptService = attemptService;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : Guid.Empty;

    private string CurrentUserRole =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // POST /api/v1/attempts/start
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartAttemptRequest request)
    {
        var attempt = await _attemptService.StartAttemptAsync(request.QuizId, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = attempt.Id }, attempt);
    }

    // POST /api/v1/attempts/{id}/submit
    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitAttemptRequest request)
    {
        var result = await _attemptService.SubmitAttemptAsync(id, request, CurrentUserId);
        return Ok(result);
    }

    // GET /api/v1/attempts/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _attemptService.GetAttemptByIdAsync(id, CurrentUserId, CurrentUserRole);
        return Ok(result);
    }
}
