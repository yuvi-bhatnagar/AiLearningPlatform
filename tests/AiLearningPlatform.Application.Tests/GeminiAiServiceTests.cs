using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using AiLearningPlatform.Domain.Enums;
using AiLearningPlatform.Infrastructure.Services;
using AiLearningPlatform.Application.Features.AI.DTOs;

namespace AiLearningPlatform.Application.Tests;

public class GeminiAiServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock = new();
    private readonly IConfiguration _configuration;

    public GeminiAiServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?> {
            {"AiSettings:ApiKey", "TestKey123"},
            {"AiSettings:Model", "gemini-1.5-flash"}
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task GenerateQuizAsync_WithValidResponse_ShouldReturnList()
    {
        // Arrange
        var geminiResponse = new GeminiResponse
        {
            Candidates = new[]
            {
                new GeminiCandidate
                {
                    Content = new GeminiResponseContent
                    {
                        Parts = new[]
                        {
                            new GeminiResponsePart
                            {
                                Text = "[{\"text\": \"What is C#?\", \"options\": [\"Language\", \"OS\"], \"correctAnswer\": \"Language\", \"points\": 5}]"
                            }
                        }
                    }
                }
            }
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(geminiResponse)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        var service = new GeminiAiService(httpClient, _configuration);

        // Act
        var result = await service.GenerateQuizAsync("C#", 1);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("What is C#?");
        result[0].CorrectAnswer.Should().Be("Language");
        result[0].Points.Should().Be(5);
    }

    [Fact]
    public async Task EvaluateAnswerAsync_WithValidResponse_ShouldReturnResult()
    {
        // Arrange
        var evalResult = new AiEvaluationResultDto(8.5, true, "Great answer!", "High");
        var geminiResponse = new GeminiResponse
        {
            Candidates = new[]
            {
                new GeminiCandidate
                {
                    Content = new GeminiResponseContent
                    {
                        Parts = new[]
                        {
                            new GeminiResponsePart
                            {
                                Text = JsonSerializer.Serialize(evalResult, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                            }
                        }
                    }
                }
            }
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(geminiResponse)
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/")
        };

        var service = new GeminiAiService(httpClient, _configuration);

        // Act
        var result = await service.EvaluateAnswerAsync("What is SELECT?", "Query", "A query command");

        // Assert
        result.Should().NotBeNull();
        result.Score.Should().Be(8.5);
        result.IsCorrect.Should().BeTrue();
        result.Feedback.Should().Be("Great answer!");
        result.Confidence.Should().Be("High");
    }
}
