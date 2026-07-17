using FluentValidation;
using AiLearningPlatform.Application.Features.Attempts.DTOs;

namespace AiLearningPlatform.Application.Features.Attempts.Validators;

public class StartAttemptRequestValidator : AbstractValidator<StartAttemptRequest>
{
    public StartAttemptRequestValidator()
    {
        RuleFor(x => x.QuizId)
            .NotEmpty().WithMessage("QuizId is required.");
    }
}
