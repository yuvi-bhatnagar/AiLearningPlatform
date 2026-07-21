using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AiLearningPlatform.Infrastructure.Security;

// Why JWT?
// A JSON Web Token (JWT) consists of three base64url-encoded parts separated by dots:
//   HEADER.PAYLOAD.SIGNATURE
//
// HEADER: algorithm + token type
//   { "alg": "HS256", "typ": "JWT" }
//
// PAYLOAD: claims (user data embedded in token — readable by anyone)
//   { "sub": "userId", "name": "yuvi", "role": "Teacher", "exp": 1234567890 }
//
// SIGNATURE: HMACSHA256(base64(header) + "." + base64(payload), secretKey)
//   → This cryptographic signature proves the token was created by OUR server
//   → Anyone can READ the payload, but CANNOT MODIFY it without invalidating the signature
//   → The server validates by re-computing the signature — NO DATABASE LOOKUP NEEDED
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(User user)
    {
        // Read JWT settings from appsettings.json
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"]!;
        var issuer = jwtSettings["Issuer"]!;
        var audience = jwtSettings["Audience"]!;
        var expiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"]!);

        // Convert the plain-text secret into a cryptographic signing key
        // SymmetricSecurityKey = same key is used for signing AND verifying (shared secret)
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claims are key-value pairs embedded in the token payload
        // These are automatically read by ASP.NET Core's authorization middleware
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,        // Who issued the token
            audience: audience,    // Who the token is intended for
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        // Serialize the JWT object to the compact dotted string format: header.payload.signature
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // The refresh token is NOT a JWT — it's a random 64-byte opaque value
        // Why random bytes? Because they are cryptographically unpredictable.
        // We convert to base64 for safe storage/transmission.
        // The server stores this in the database and validates it on refresh requests.
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
