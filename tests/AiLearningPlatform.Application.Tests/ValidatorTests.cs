using FluentAssertions;
using AiLearningPlatform.Application.Features.Courses.DTOs;
using AiLearningPlatform.Application.Features.Courses.Validators;
using AiLearningPlatform.Application.Features.Questions.DTOs;
using AiLearningPlatform.Application.Features.Questions.Validators;
using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Tests;

public class ValidatorTests
{
    private readonly CreateCourseRequestValidator _createCourseValidator = new();
    private readonly CreateQuestionRequestValidator _createQuestionValidator = new();

    [Fact]
    public void CourseValidator_WithValidData_ShouldPass()
    {
        var request = new CreateCourseRequest("Introduction to C#", "Learn C# basics");
        var result = _createCourseValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "Valid description")]
    [InlineData("Valid Title", "")]
    [InlineData("", "")]
    public void CourseValidator_WithEmptyFields_ShouldFail(string title, string description)
    {
        var request = new CreateCourseRequest(title, description);
        var result = _createCourseValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void QuestionValidator_WithValidMCQ_ShouldPass()
    {
        var request = new CreateQuestionRequest(
            Guid.NewGuid(),
            "What is 2+2?",
            QuestionType.MultipleChoice,
            new List<string> { "3", "4", "5" },
            "4",
            10
        );

        var result = _createQuestionValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void QuestionValidator_MCQWithFewerThanTwoOptions_ShouldFail()
    {
        var request = new CreateQuestionRequest(
            Guid.NewGuid(),
            "What is 2+2?",
            QuestionType.MultipleChoice,
            new List<string> { "4" }, // Only 1 option
            "4",
            10
        );

        var result = _createQuestionValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Options" && e.ErrorMessage.Contains("at least 2 options"));
    }

    [Fact]
    public void QuestionValidator_WithNegativePoints_ShouldFail()
    {
        var request = new CreateQuestionRequest(
            Guid.NewGuid(),
            "Explain recursion.",
            QuestionType.Subjective,
            new List<string>(),
            "Expected keywords",
            -5 // Invalid points
        );

        var result = _createQuestionValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Points" && e.ErrorMessage.Contains("greater than 0"));
    }
}
