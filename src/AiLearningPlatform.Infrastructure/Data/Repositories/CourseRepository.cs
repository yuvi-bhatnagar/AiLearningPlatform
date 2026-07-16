using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Data.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly AppDbContext _context;

    public CourseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Course?> GetByIdAsync(Guid id)
    {
        return await _context.Courses
            .Include(c => c.Instructor)
            .Include(c => c.Quizzes)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Course>> GetAllAsync()
    {
        return await _context.Courses
            .Include(c => c.Instructor)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task AddAsync(Course course)
    {
        await _context.Courses.AddAsync(course);
    }

    public void Update(Course course)
    {
        _context.Courses.Update(course);
    }

    public void Delete(Course course)
    {
        _context.Courses.Remove(course);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
