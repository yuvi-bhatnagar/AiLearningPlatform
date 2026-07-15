using AiLearningPlatform.Domain.Enums;

namespace AiLearningPlatform.Application.Features.Auth.DTOs;

// Why a DTO (Data Transfer Object)?
// We NEVER expose our Domain entities directly from the API. DTOs are flat, simple models
// shaped for a specific use case. This protects our domain models from being tightly coupled
// to the API layer and prevents accidentally exposing sensitive fields (like PasswordHash).

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    // Role is specified at registration so a Teacher can register as Teacher.
    // In a real production system you'd likely restrict this — e.g. only Admins can
    // create Teacher accounts — but for our learning platform, we allow it for simplicity.
    UserRole Role
);
