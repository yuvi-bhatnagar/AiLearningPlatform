using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Domain.Entities;

public class Question
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;
    
    // For MultipleChoice questions, options will be stored as serialized JSON array of strings
    public string OptionsJson { get; set; } = string.Empty;
    
    // For MCQs: index or text of correct answer. For Subjective: keywords/expected answer for AI evaluation
    public string CorrectAnswer { get; set; } = string.Empty;
    
    public int Points { get; set; }

    // Navigation properties
    public Quiz Quiz { get; set; } = null!;
    public ICollection<AnswerSubmission> AnswerSubmissions { get; set; } = new List<AnswerSubmission>();
}
