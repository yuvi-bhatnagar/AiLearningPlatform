using FluentValidation;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Questions.Validators;

public class CreateQuestionRequestValidator : AbstractValidator<CreateQuestionRequest>
{
    public CreateQuestionRequestValidator()
    {
        RuleFor(x => x.QuizId)
            .NotEmpty().WithMessage("QuizId is required.");

        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Question text is required.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid question type.");

        RuleFor(x => x.Options)
            .Must(o => o != null && o.Count >= 2)
            .When(x => x.Type == QuestionType.MultipleChoice)
            .WithMessage("Multiple choice questions must have at least 2 options.");

        RuleFor(x => x.CorrectAnswer)
            .NotEmpty().WithMessage("Correct answer is required.");

        RuleFor(x => x.Points)
            .GreaterThan(0).WithMessage("Points must be greater than 0.");
    }
}
