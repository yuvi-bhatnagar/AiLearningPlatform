using FluentAssertions;

namespace AiLearningPlatform.Application.Tests;

public class UnitTest1
{
    [Fact]
    public void PlaceholderTest_ShouldPass_WhenFluentAssertionsWorks()
    {
        var message = "Hello from Application.Tests";
        message.Should().NotBeNullOrEmpty();
        message.Should().Contain("Application");
    }
}

