using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;

namespace AiLearningPlatform.Infrastructure.Data.Repositories;

public class QuestionRepository : IQuestionRepository
{
    private readonly AppDbContext _context;

    public QuestionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Question?> GetByIdAsync(Guid id)
    {
        return await _context.Questions
            .Include(q => q.Quiz)
            .FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<IEnumerable<Question>> GetByQuizIdAsync(Guid quizId)
    {
        return await _context.Questions
            .AsNoTracking()
            .Where(q => q.QuizId == quizId)
            .ToListAsync();
    }

    public async Task AddAsync(Question question)
    {
        await _context.Questions.AddAsync(question);
    }

    public void Update(Question question)
    {
        _context.Questions.Update(question);
    }

    public void Delete(Question question)
    {
        _context.Questions.Remove(question);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
