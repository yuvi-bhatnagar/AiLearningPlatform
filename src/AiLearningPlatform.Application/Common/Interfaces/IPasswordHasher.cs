namespace AiLearningPlatform.Application.Common.Interfaces;

// Why this interface exists:
// We define password hashing as an INTERFACE in Application, not a concrete class.
// This follows the Dependency Inversion Principle — Application says "I need something that can hash
// passwords", but doesn't care HOW it's done. Infrastructure provides the actual BCrypt implementation.
// This allows us to swap BCrypt for Argon2 or PBKDF2 in the future without touching Application code.
// It also allows unit tests to mock this dependency easily.
public interface IPasswordHasher
{
    // Takes a plain-text password and returns a bcrypt hash string
    string Hash(string password);

    // Compares a plain-text password against a stored bcrypt hash
    // Returns true if they match, false otherwise
    bool Verify(string password, string hash);
}
