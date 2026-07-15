using AiLearningPlatform.Domain.Entities;

namespace AiLearningPlatform.Application.Common.Interfaces;

// Why this interface exists:
// Same Dependency Inversion principle as IPasswordHasher.
// The Application layer needs to generate tokens but doesn't care about the JWT library or algorithm.
// Infrastructure provides the implementation using System.IdentityModel.Tokens.Jwt.
// This interface also makes token logic trivially mockable in unit tests — tests never need to
// construct or parse real JWTs.
public interface ITokenService
{
    // Generates a short-lived JWT access token (e.g. 15 minutes)
    // Embeds user's Id, Username, Email, and Role as JWT claims
    string GenerateAccessToken(User user);

    // Generates a long-lived cryptographically random refresh token string
    // This is NOT a JWT — it's just a random opaque value stored in the database
    // The client sends it back to exchange for a new access token
    string GenerateRefreshToken();
}
