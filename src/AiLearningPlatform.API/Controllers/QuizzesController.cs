using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AiLearningPlatform.Application.Features.Quizzes;
using AiLearningPlatform.Application.Features.Quizzes.DTOs;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class QuizzesController : ControllerBase
{
    private readonly IQuizService _quizService;

    public QuizzesController(IQuizService quizService)
    {
        _quizService = quizService;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : Guid.Empty;

    private string CurrentUserRole =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // GET /api/v1/quizzes/by-course/{courseId}
    [HttpGet("by-course/{courseId}")]
    public async Task<IActionResult> GetByCourseId(Guid courseId)
    {
        var quizzes = await _quizService.GetByCourseIdAsync(courseId);
        return Ok(quizzes);
    }

    // GET /api/v1/quizzes/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var quiz = await _quizService.GetByIdAsync(id);
        return Ok(quiz);
    }

    // POST /api/v1/quizzes
    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuizRequest request)
    {
        var quiz = await _quizService.CreateAsync(request, CurrentUserId, CurrentUserRole);
        return CreatedAtAction(nameof(GetById), new { id = quiz.Id }, quiz);
    }

    // PUT /api/v1/quizzes/{id}
    [Authorize(Roles = "Teacher,Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuizRequest request)
    {
        var quiz = await _quizService.UpdateAsync(id, request, CurrentUserId, CurrentUserRole);
        return Ok(quiz);
    }

    // DELETE /api/v1/quizzes/{id}
    [Authorize(Roles = "Teacher,Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _quizService.DeleteAsync(id, CurrentUserId, CurrentUserRole);
        return NoContent();
    }

    // POST /api/v1/quizzes/{quizId}/generate-questions
    [Authorize(Roles = "Teacher,Admin")]
    [EnableRateLimiting("AiEndpointPolicy")]
    [HttpPost("{quizId}/generate-questions")]
    public async Task<IActionResult> GenerateQuestions(Guid quizId, [FromBody] GenerateQuestionsRequest request)
    {
        var questions = await _quizService.GenerateQuestionsAsync(quizId, request.Topic, request.QuestionCount, CurrentUserId, CurrentUserRole);
        return Ok(questions);
    }
}
