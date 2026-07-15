using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiLearningPlatform.API.Controllers;

// This controller exists purely for manual testing and integration test verification.
// It contains endpoints protected by different role requirements.
// In Swagger, paste your JWT token in the "Authorize" button and call each endpoint
// to verify that:
//   ✅ /student returns 200 for Student tokens
//   ✅ /teacher returns 200 for Teacher tokens
//   ✅ /admin returns 200 for Admin tokens
//   ❌ /teacher returns 403 Forbidden for Student tokens
//   ❌ Any endpoint returns 401 Unauthorized with no token

[ApiController]
[Route("api/v1/test-auth")]
public class TestAuthController : ControllerBase
{
    // No [Authorize] — any anonymous request gets through
    [HttpGet("public")]
    public IActionResult Public()
        => Ok(new { message = "This endpoint is publicly accessible to anyone." });

    // [Authorize] with no role = any authenticated user (any valid JWT)
    [Authorize]
    [HttpGet("authenticated")]
    public IActionResult Authenticated()
    {
        var username = User.Identity?.Name;
        return Ok(new { message = $"Hello {username}, you are authenticated!" });
    }

    // Only users with Role == "Student" can access this
    [Authorize(Roles = "Student")]
    [HttpGet("student")]
    public IActionResult StudentOnly()
        => Ok(new { message = "Welcome Student! You can see your quiz results here." });

    // Both Teacher and Admin can access this — comma-separated roles act as OR condition
    [Authorize(Roles = "Teacher,Admin")]
    [HttpGet("teacher")]
    public IActionResult TeacherOrAdmin()
        => Ok(new { message = "Welcome Teacher/Admin! You can manage courses and quizzes here." });

    // Only Admin users can access this
    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public IActionResult AdminOnly()
        => Ok(new { message = "Welcome Admin! You have full system access." });
}
