namespace AiLearningPlatform.Application.Features.Auth.DTOs;

// Why both tokens are needed for refresh:
// The client must prove it has a valid (unexpired) RefreshToken AND that the
// AccessToken belongs to the same user session. This prevents replay attacks where
// only the refresh token was stolen.
public record RefreshRequest(
    string AccessToken,
    string RefreshToken
);
