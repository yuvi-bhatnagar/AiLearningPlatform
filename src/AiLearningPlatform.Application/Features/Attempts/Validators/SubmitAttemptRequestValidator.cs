using FluentValidation;
using AiLearningPlatform.Application.Features.Attempts.DTOs;

namespace AiLearningPlatform.Application.Features.Attempts.Validators;

public class SubmitAttemptRequestValidator : AbstractValidator<SubmitAttemptRequest>
{
    public SubmitAttemptRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotNull().WithMessage("Answers list cannot be null.");

        RuleForEach(x => x.Answers).ChildRules(ans =>
        {
            ans.RuleFor(a => a.QuestionId)
                .NotEmpty().WithMessage("QuestionId is required for all answer submissions.");
        });
    }
}
