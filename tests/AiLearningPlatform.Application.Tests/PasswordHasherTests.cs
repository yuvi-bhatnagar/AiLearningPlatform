using FluentAssertions;
using AiLearningPlatform.Infrastructure.Security;

namespace AiLearningPlatform.Application.Tests;

// Unit tests for the BCrypt PasswordHasher implementation
// These tests verify the hashing behavior WITHOUT needing a database or HTTP layer
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ShouldReturnNonEmptyString()
    {
        var hash = _hasher.Hash("MyPassword123!");
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_ShouldNotEqualPlainTextPassword()
    {
        var password = "MyPassword123!";
        var hash = _hasher.Hash(password);
        hash.Should().NotBe(password);
    }

    [Fact]
    public void Hash_CalledTwiceWithSamePassword_ShouldReturnDifferentHashes()
    {
        // Why this test matters:
        // BCrypt generates a unique salt each time Hash() is called.
        // Two identical passwords should produce different hashes.
        // This prevents: if attacker knows one person's password, they can't
        // find all users with the same password by looking for matching hashes.
        var password = "SamePassword!";
        var hash1 = _hasher.Hash(password);
        var hash2 = _hasher.Hash(password);

        hash1.Should().NotBe(hash2, "BCrypt embeds a random salt so each hash is unique");
    }

    [Fact]
    public void Verify_WithCorrectPassword_ShouldReturnTrue()
    {
        var password = "CorrectPassword123!";
        var hash = _hasher.Hash(password);

        var result = _hasher.Verify(password, hash);
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithWrongPassword_ShouldReturnFalse()
    {
        var hash = _hasher.Hash("OriginalPassword!");
        var result = _hasher.Verify("WrongPassword!", hash);
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithEmptyPassword_ShouldReturnFalse()
    {
        var hash = _hasher.Hash("RealPassword!");
        var result = _hasher.Verify("", hash);
        result.Should().BeFalse();
    }
}
