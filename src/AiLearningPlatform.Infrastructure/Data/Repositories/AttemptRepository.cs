using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Data.Repositories;

public class AttemptRepository : IAttemptRepository
{
    private readonly AppDbContext _context;

    public AttemptRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Attempt?> GetByIdAsync(Guid id)
    {
        return await _context.Attempts
            .Include(a => a.Quiz)
                .ThenInclude(q => q.Questions)
            .Include(a => a.AnswerSubmissions)
                .ThenInclude(ans => ans.Question)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Attempt>> GetByUserIdAsync(Guid userId)
    {
        return await _context.Attempts
            .AsNoTracking()
            .Include(a => a.Quiz)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartedAtUtc)
            .ToListAsync();
    }

    public async Task<Attempt?> GetActiveAttemptAsync(Guid quizId, Guid userId)
    {
        return await _context.Attempts
            .FirstOrDefaultAsync(a => a.QuizId == quizId && a.UserId == userId && a.Status == AttemptStatus.InProgress);
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<IEnumerable<Attempt>> GetLowConfidenceAttemptsByInstructorAsync(Guid instructorId)
    {
        return await _context.Attempts
            .Include(a => a.User)
            .Include(a => a.Quiz)
            .Include(a => a.AnswerSubmissions)
                .ThenInclude(ans => ans.Question)
            .Where(a => a.Quiz.Course.InstructorId == instructorId &&
                        a.AnswerSubmissions.Any(ans => ans.Confidence == "Low"))
            .ToListAsync();
    }

    public async Task AddAsync(Attempt attempt)
    {
        await _context.Attempts.AddAsync(attempt);
    }

    public void Update(Attempt attempt)
    {
        _context.Attempts.Update(attempt);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
