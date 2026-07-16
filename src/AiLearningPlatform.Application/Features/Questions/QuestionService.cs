using System.Text.Json;
using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.Application.Features.Questions;

public class QuestionService : IQuestionService
{
    private readonly IQuestionRepository _questionRepository;
    private readonly IQuizRepository _quizRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IValidator<CreateQuestionRequest> _createValidator;
    private readonly IValidator<UpdateQuestionRequest> _updateValidator;

    public QuestionService(
        IQuestionRepository questionRepository,
        IQuizRepository quizRepository,
        ICourseRepository courseRepository,
        IValidator<CreateQuestionRequest> createValidator,
        IValidator<UpdateQuestionRequest> updateValidator)
    {
        _questionRepository = questionRepository;
        _quizRepository = quizRepository;
        _courseRepository = courseRepository;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<QuestionDto> GetByIdAsync(Guid id)
    {
        var question = await _questionRepository.GetByIdAsync(id);
        if (question is null)
            throw new NotFoundException(nameof(Question), id);

        return MapToDto(question);
    }

    public async Task<IEnumerable<QuestionDto>> GetByQuizIdAsync(Guid quizId)
    {
        var quiz = await _quizRepository.GetByIdAsync(quizId);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), quizId);

        var questions = await _questionRepository.GetByQuizIdAsync(quizId);
        return questions.Select(MapToDto);
    }

    public async Task<QuestionDto> CreateAsync(CreateQuestionRequest request, Guid currentUserId, string currentUserRole)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var quiz = await _quizRepository.GetByIdAsync(request.QuizId);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), request.QuizId);

        var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), quiz.CourseId);

        // Authorization: Only the course's instructor or Admin can add questions
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to create questions for this quiz.");

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuizId = request.QuizId,
            Text = request.Text,
            Type = request.Type,
            OptionsJson = JsonSerializer.Serialize(request.Options),
            CorrectAnswer = request.CorrectAnswer,
            Points = request.Points
        };

        await _questionRepository.AddAsync(question);
        await _questionRepository.SaveChangesAsync();

        return MapToDto(question);
    }

    public async Task<QuestionDto> UpdateAsync(Guid id, UpdateQuestionRequest request, Guid currentUserId, string currentUserRole)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var question = await _questionRepository.GetByIdAsync(id);
        if (question is null)
            throw new NotFoundException(nameof(Question), id);

        var quiz = await _quizRepository.GetByIdAsync(question.QuizId);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), question.QuizId);

        var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), quiz.CourseId);

        // Authorization: Only the course's instructor or Admin can update questions
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to update this question.");

        question.Text = request.Text;
        question.Type = request.Type;
        question.OptionsJson = JsonSerializer.Serialize(request.Options);
        question.CorrectAnswer = request.CorrectAnswer;
        question.Points = request.Points;

        _questionRepository.Update(question);
        await _questionRepository.SaveChangesAsync();

        return MapToDto(question);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, string currentUserRole)
    {
        var question = await _questionRepository.GetByIdAsync(id);
        if (question is null)
            throw new NotFoundException(nameof(Question), id);

        var quiz = await _quizRepository.GetByIdAsync(question.QuizId);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), question.QuizId);

        var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), quiz.CourseId);

        // Authorization: Only the course's instructor or Admin can delete questions
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to delete this question.");

        _questionRepository.Delete(question);
        await _questionRepository.SaveChangesAsync();
    }

    private static QuestionDto MapToDto(Question q)
    {
        List<string> options;
        try
        {
            options = string.IsNullOrEmpty(q.OptionsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(q.OptionsJson) ?? new List<string>();
        }
        catch
        {
            options = new List<string>();
        }

        return new(q.Id, q.QuizId, q.Text, q.Type, options, q.CorrectAnswer, q.Points);
    }

    private static IDictionary<string, string[]> ToDictionary(FluentValidation.Results.ValidationResult result) =>
        result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
