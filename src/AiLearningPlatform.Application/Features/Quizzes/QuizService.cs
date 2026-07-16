using System.Text.Json;
using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Quizzes.DTOs;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Domain.Exceptions;

namespace AiLearningPlatform.Application.Features.Quizzes;

public class QuizService : IQuizService
{
    private readonly IQuizRepository _quizRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IQuestionRepository _questionRepository;
    private readonly IAiService _aiService;
    private readonly IValidator<CreateQuizRequest> _createValidator;
    private readonly IValidator<UpdateQuizRequest> _updateValidator;

    public QuizService(
        IQuizRepository quizRepository,
        ICourseRepository courseRepository,
        IQuestionRepository questionRepository,
        IAiService aiService,
        IValidator<CreateQuizRequest> createValidator,
        IValidator<UpdateQuizRequest> updateValidator)
    {
        _quizRepository = quizRepository;
        _courseRepository = courseRepository;
        _questionRepository = questionRepository;
        _aiService = aiService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<QuizDto> GetByIdAsync(Guid id)
    {
        var quiz = await _quizRepository.GetByIdAsync(id);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), id);

        return MapToDto(quiz);
    }

    public async Task<IEnumerable<QuizDto>> GetByCourseIdAsync(Guid courseId)
    {
        // First check course exists
        var course = await _courseRepository.GetByIdAsync(courseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), courseId);

        var quizzes = await _quizRepository.GetByCourseIdAsync(courseId);
        return quizzes.Select(MapToDto);
    }

    public async Task<QuizDto> CreateAsync(CreateQuizRequest request, Guid currentUserId, string currentUserRole)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        // Get underlying course
        var course = await _courseRepository.GetByIdAsync(request.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), request.CourseId);

        // Authorization: Only the course's instructor or Admin can add a quiz
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to create a quiz in this course.");

        var quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            CourseId = request.CourseId,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _quizRepository.AddAsync(quiz);
        await _quizRepository.SaveChangesAsync();

        return MapToDto(quiz);
    }

    public async Task<QuizDto> UpdateAsync(Guid id, UpdateQuizRequest request, Guid currentUserId, string currentUserRole)
    {
        var validationResult = await _updateValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
            throw new Domain.Exceptions.ValidationException(ToDictionary(validationResult));

        var quiz = await _quizRepository.GetByIdAsync(id);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), id);

        // Fetch course to check authorship
        var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), quiz.CourseId);

        // Authorization: Only the course's instructor or Admin can update its quizzes
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to update this quiz.");

        quiz.Title = request.Title;
        quiz.Description = request.Description;

        _quizRepository.Update(quiz);
        await _quizRepository.SaveChangesAsync();

        return MapToDto(quiz);
    }

    public async Task DeleteAsync(Guid id, Guid currentUserId, string currentUserRole)
    {
        var quiz = await _quizRepository.GetByIdAsync(id);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), id);

        var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), quiz.CourseId);

        // Authorization: Only the course's instructor or Admin can delete its quizzes
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to delete this quiz.");

        _quizRepository.Delete(quiz);
        await _quizRepository.SaveChangesAsync();
    }

    public async Task<IEnumerable<QuestionDto>> GenerateQuestionsAsync(Guid quizId, string topic, int questionCount, Guid currentUserId, string currentUserRole)
    {
        if (questionCount <= 0)
        {
            var errors = new Dictionary<string, string[]> { { "QuestionCount", new[] { "Question count must be greater than 0." } } };
            throw new Domain.Exceptions.ValidationException(errors);
        }
        if (string.IsNullOrWhiteSpace(topic))
        {
            var errors = new Dictionary<string, string[]> { { "Topic", new[] { "Topic cannot be empty." } } };
            throw new Domain.Exceptions.ValidationException(errors);
        }

        var quiz = await _quizRepository.GetByIdAsync(quizId);
        if (quiz is null)
            throw new NotFoundException(nameof(Quiz), quizId);

        var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
        if (course is null)
            throw new NotFoundException(nameof(Course), quiz.CourseId);

        // Authorization: Only the course's instructor or Admin can generate questions
        if (currentUserRole != "Admin" && course.InstructorId != currentUserId)
            throw new UnauthorizedAccessException("You are not authorized to generate questions for this quiz.");

        var generated = await _aiService.GenerateQuizAsync(topic, questionCount);
        var createdQuestions = new List<QuestionDto>();

        foreach (var gen in generated)
        {
            var question = new Question
            {
                Id = Guid.NewGuid(),
                QuizId = quizId,
                Text = gen.Text,
                Type = QuestionType.MultipleChoice,
                OptionsJson = JsonSerializer.Serialize(gen.Options),
                CorrectAnswer = gen.CorrectAnswer,
                Points = gen.Points
            };

            await _questionRepository.AddAsync(question);
            createdQuestions.Add(new QuestionDto(
                question.Id,
                question.QuizId,
                question.Text,
                question.Type,
                gen.Options,
                question.CorrectAnswer,
                question.Points
            ));
        }

        await _questionRepository.SaveChangesAsync();
        return createdQuestions;
    }

    private static QuizDto MapToDto(Quiz quiz) =>
        new(quiz.Id, quiz.Title, quiz.Description, quiz.CourseId, quiz.CreatedAtUtc);

    private static IDictionary<string, string[]> ToDictionary(FluentValidation.Results.ValidationResult result) =>
        result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
