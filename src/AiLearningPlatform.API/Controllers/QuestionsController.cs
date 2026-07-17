using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiLearningPlatform.Application.Features.Questions;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class QuestionsController : ControllerBase
{
    private readonly IQuestionService _questionService;

    public QuestionsController(IQuestionService questionService)
    {
        _questionService = questionService;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : Guid.Empty;

    private string CurrentUserRole =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // GET /api/v1/questions/by-quiz/{quizId}
    [HttpGet("by-quiz/{quizId}")]
    public async Task<IActionResult> GetByQuizId(Guid quizId)
    {
        var questions = await _questionService.GetByQuizIdAsync(quizId);
        return Ok(questions);
    }

    // GET /api/v1/questions/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var question = await _questionService.GetByIdAsync(id);
        return Ok(question);
    }

    // POST /api/v1/questions
    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuestionRequest request)
    {
        var question = await _questionService.CreateAsync(request, CurrentUserId, CurrentUserRole);
        return CreatedAtAction(nameof(GetById), new { id = question.Id }, question);
    }

    // PUT /api/v1/questions/{id}
    [Authorize(Roles = "Teacher,Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateQuestionRequest request)
    {
        var question = await _questionService.UpdateAsync(id, request, CurrentUserId, CurrentUserRole);
        return Ok(question);
    }

    // DELETE /api/v1/questions/{id}
    [Authorize(Roles = "Teacher,Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _questionService.DeleteAsync(id, CurrentUserId, CurrentUserRole);
        return NoContent();
    }
}
