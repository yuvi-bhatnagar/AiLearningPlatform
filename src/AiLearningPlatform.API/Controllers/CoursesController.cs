using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiLearningPlatform.Application.Features.Courses;
using AiLearningPlatform.Application.Features.Courses.DTOs;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace AiLearningPlatform.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService)
    {
        _courseService = courseService;
    }

    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id)
        ? id : Guid.Empty;

    private string CurrentUserRole =>
        User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    // GET /api/v1/courses
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var courses = await _courseService.GetAllAsync();
        return Ok(courses);
    }

    // GET /api/v1/courses/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var course = await _courseService.GetByIdAsync(id);
        return Ok(course);
    }

    // POST /api/v1/courses
    [Authorize(Roles = "Teacher,Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request)
    {
        var course = await _courseService.CreateAsync(request, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
    }

    // PUT /api/v1/courses/{id}
    [Authorize(Roles = "Teacher,Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseRequest request)
    {
        var course = await _courseService.UpdateAsync(id, request, CurrentUserId, CurrentUserRole);
        return Ok(course);
    }

    // DELETE /api/v1/courses/{id}
    [Authorize(Roles = "Teacher,Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _courseService.DeleteAsync(id, CurrentUserId, CurrentUserRole);
        return NoContent();
    }
}
