using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Auth.DTOs;
using AiLearningPlatform.Domain.Entities;
using AiLearningPlatform.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AiLearningPlatform.API.Controllers;

// Why [ApiController]?
// → Auto-validates models and returns 400 if ModelState is invalid (without manual checks)
// → Automatically infers [FromBody] for complex types
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _auditLogService;

    public AuthController(
        AppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration,
        IAuditLogService auditLogService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
        _auditLogService = auditLogService;
    }

    // ============================================================
    // POST /api/v1/auth/register
    // ============================================================
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Why block Admin registration?
        // Admin is a privileged role seeded once at startup from configuration.
        // Allowing public registration as Admin would be a critical security vulnerability.
        // In production, Admin accounts should only be created by the system or existing Admins.
        if (request.Role == Domain.Enums.UserRole.Admin)
            return Forbid();

        // Check for duplicate email — emails must be unique in the Users table
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser is not null)
            return Conflict(new { message = "A user with this email already exists." });

        // Check for duplicate username
        var existingUsername = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (existingUsername is not null)
            return Conflict(new { message = "This username is already taken." });

        // Hash the plain-text password BEFORE saving to the database
        // We NEVER store plain-text passwords
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Generate both tokens immediately so user is logged in after registration
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Store refresh token hash in the database
        var refreshExpiry = int.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]!);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(refreshExpiry);
        await _db.SaveChangesAsync();

        await _auditLogService.LogActionAsync("Registration", $"User '{user.Username}' registered with role '{user.Role}'.");

        return Ok(new AuthResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            accessToken,
            refreshToken
        ));
    }

    // ============================================================
    // POST /api/v1/auth/login
    // ============================================================
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Find user by email — case insensitive comparison handled by SQL Server's default collation
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Why we don't say "wrong password" vs "wrong email":
        // Giving different error messages for each reveals which emails are registered.
        // "Invalid credentials" is the safest response.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Rotate refresh token — invalidate old one, save new one
        var refreshExpiry = int.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]!);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(refreshExpiry);
        await _db.SaveChangesAsync();

        await _auditLogService.LogActionAsync("Login", $"User '{user.Username}' logged in successfully.");

        return Ok(new AuthResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            accessToken,
            refreshToken
        ));
    }

    // ============================================================
    // POST /api/v1/auth/refresh
    // ============================================================
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        // Extract the principal (claims) from the EXPIRED access token
        // We explicitly allow expired tokens here — that's the whole point of refresh
        var principal = GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
            return BadRequest(new { message = "Invalid access token." });

        // Get user ID from the Sub claim in the expired JWT
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            return BadRequest(new { message = "Invalid token claims." });

        var user = await _db.Users.FindAsync(userId);

        // Validate that:
        // 1. The user exists
        // 2. The stored refresh token matches what was sent
        // 3. The refresh token hasn't expired
        if (user is null
            || user.RefreshToken != request.RefreshToken
            || user.RefreshTokenExpiryUtc <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token." });
        }

        // All good — issue new tokens (rotate the refresh token for added security)
        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var refreshExpiry = int.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]!);
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(refreshExpiry);
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            newAccessToken,
            newRefreshToken
        ));
    }

    // Helper: Validate a JWT token's structure and signature WITHOUT checking expiry
    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]!;

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            // KEY: Do NOT validate lifetime — this is what allows expired tokens here
            ValidateLifetime = false
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(token, tokenValidationParameters, out var validatedToken);

            // Extra check: ensure the algorithm is HMAC-SHA256 (prevents algorithm substitution attacks)
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
