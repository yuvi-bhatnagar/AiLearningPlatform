using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiLearningPlatform.Application.Features.Leaderboards;
using AiLearningPlatform.Application.Features.Leaderboards.DTOs;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class LeaderboardsController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;

    public LeaderboardsController(ILeaderboardService leaderboardService)
    {
        _leaderboardService = leaderboardService;
    }

    private (Guid UserId, string Role) GetCurrentUser()
    {
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id);
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        return (id, role);
    }

    // ============================================================
    // GET /api/v1/leaderboards
    // ============================================================
    [HttpGet]
    public async Task<ActionResult<List<LeaderboardRowDto>>> GetLeaderboard()
    {
        var leaderboard = await _leaderboardService.GetLeaderboardAsync();
        return Ok(leaderboard);
    }

    // ============================================================
    // GET /api/v1/leaderboards/summary
    // ============================================================
    [HttpGet("summary")]
    public async Task<ActionResult<StudentPerformanceSummaryDto>> GetMySummary()
    {
        var (currentUserId, _) = GetCurrentUser();
        var summary = await _leaderboardService.GetStudentPerformanceSummaryAsync(currentUserId);
        
        if (summary == null)
            return NotFound(new { message = "No performance statistics found for the current user." });

        return Ok(summary);
    }

    // ============================================================
    // GET /api/v1/leaderboards/summary/{userId}
    // ============================================================
    [HttpGet("summary/{userId:guid}")]
    public async Task<ActionResult<StudentPerformanceSummaryDto>> GetUserSummary(Guid userId)
    {
        var (currentUserId, role) = GetCurrentUser();

        // Access check: only Admin, Teacher, or the user themselves can view their performance summary
        if (role != "Admin" && role != "Teacher" && currentUserId != userId)
        {
            return Forbid();
        }

        var summary = await _leaderboardService.GetStudentPerformanceSummaryAsync(userId);
        
        if (summary == null)
            return NotFound(new { message = $"No performance statistics found for user with ID {userId}." });

        return Ok(summary);
    }
}
