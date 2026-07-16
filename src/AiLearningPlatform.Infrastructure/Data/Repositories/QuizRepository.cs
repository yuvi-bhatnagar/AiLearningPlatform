using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Data.Repositories;

public class QuizRepository : IQuizRepository
{
    private readonly AppDbContext _context;

    public QuizRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Quiz?> GetByIdAsync(Guid id)
    {
        return await _context.Quizzes
            .Include(q => q.Course)
            .Include(q => q.Questions)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<IEnumerable<Quiz>> GetByCourseIdAsync(Guid courseId)
    {
        return await _context.Quizzes
            .Where(q => q.CourseId == courseId)
            .OrderByDescending(q => q.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task AddAsync(Quiz quiz)
    {
        await _context.Quizzes.AddAsync(quiz);
    }

    public void Update(Quiz quiz)
    {
        _context.Quizzes.Update(quiz);
    }

    public void Delete(Quiz quiz)
    {
        _context.Quizzes.Remove(quiz);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
