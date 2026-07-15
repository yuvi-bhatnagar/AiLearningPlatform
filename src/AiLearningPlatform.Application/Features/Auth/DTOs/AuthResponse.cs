using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Auth.DTOs;

// Why AuthResponse includes both tokens:
// The client stores the AccessToken in memory (short lived, ~15 mins).
// The client stores the RefreshToken in an httpOnly cookie or secure storage.
// When the AccessToken expires, the client sends RefreshToken to get a new one
// without asking the user to log in again.
public record AuthResponse(
    Guid UserId,
    string Username,
    string Email,
    UserRole Role,
    string AccessToken,
    string RefreshToken
);
