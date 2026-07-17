using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.AI.DTOs;

namespace AiLearningPlatform.Infrastructure.Services;

public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiAiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["AiSettings:ApiKey"] ?? string.Empty;
        _model = configuration["AiSettings:Model"] ?? "gemini-1.5-flash";
    }

    public async Task<List<GeneratedQuestionDto>> GenerateQuizAsync(string topic, int questionCount)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Gemini API key is not configured.");

        var prompt = $"Generate exactly {questionCount} multiple-choice questions about the topic '{topic}'. " +
                     "Return a JSON array where each item has the following structure: " +
                     "{ \"text\": \"Question text\", \"options\": [\"Option A\", \"Option B\", \"Option C\", \"Option D\"], \"correctAnswer\": \"Option A\", \"points\": 10 }";

        var requestBody = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new[]
                    {
                        new GeminiPart { Text = prompt }
                    }
                }
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json"
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"v1beta/models/{_model}:generateContent?key={_apiKey}", requestBody);
        response.EnsureSuccessStatusCode();

        var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var jsonText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(jsonText))
            throw new Exception("AI returned empty content.");

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(jsonText, options) ?? new List<GeneratedQuestionDto>();
    }

    public async Task<AiEvaluationResultDto> EvaluateAnswerAsync(string questionText, string correctAnswerKeywords, string studentAnswer)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Gemini API key is not configured.");

        var prompt = "Evaluate the student's answer for the following subjective question.\n" +
                     $"Question: {questionText}\n" +
                     $"Expected/Key Concepts: {correctAnswerKeywords}\n" +
                     $"Student's Answer: {studentAnswer}\n\n" +
                     "Provide a score out of 10, a short feedback explanation, a boolean value indicating whether it is correct (score >= 5), and a confidence level (Low, Medium, High).\n" +
                     "Return exactly this JSON format:\n" +
                     "{ \"score\": 7.5, \"isCorrect\": true, \"feedback\": \"...\", \"confidence\": \"High\" }";

        var requestBody = new GeminiRequest
        {
            Contents = new[]
            {
                new GeminiContent
                {
                    Parts = new[]
                    {
                        new GeminiPart { Text = prompt }
                    }
                }
            },
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseMimeType = "application/json"
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"v1beta/models/{_model}:generateContent?key={_apiKey}", requestBody);
        response.EnsureSuccessStatusCode();

        var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var jsonText = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(jsonText))
            throw new Exception("AI returned empty content.");

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return JsonSerializer.Deserialize<AiEvaluationResultDto>(jsonText, options) ?? throw new Exception("Failed to deserialize AI evaluation result.");
    }
}

// REST request and response contracts for Google Gemini API
public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public GeminiContent[] Contents { get; set; } = Array.Empty<GeminiContent>();

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public GeminiPart[] Parts { get; set; } = Array.Empty<GeminiPart>();
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiGenerationConfig
{
    [JsonPropertyName("responseMimeType")]
    public string ResponseMimeType { get; set; } = string.Empty;
}

public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiResponseContent? Content { get; set; }
}

public class GeminiResponseContent
{
    [JsonPropertyName("parts")]
    public GeminiResponsePart[]? Parts { get; set; }
}

public class GeminiResponsePart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}
