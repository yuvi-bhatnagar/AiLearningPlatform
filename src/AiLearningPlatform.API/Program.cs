using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Infrastructure.Data;
using AiLearningPlatform.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// 1. DATABASE
// ============================================================
// Why: AppDbContext is the EF Core bridge between our C# entities and SQL Server.
// Registered as Scoped — a new instance per HTTP request (important for EF Core).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ============================================================
// 2. AUTHENTICATION (JWT Bearer)
// ============================================================
// Why JWT Bearer? The client includes "Authorization: Bearer <token>" in request headers.
// ASP.NET Core reads this header, validates the token cryptographically, and populates
// HttpContext.User with the claims — all BEFORE the controller action runs.
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secret = jwtSettings["Secret"]!;

builder.Services.AddAuthentication(options =>
{
    // Tell ASP.NET Core to use JWT Bearer as the DEFAULT scheme for authentication
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // These parameters are checked on EVERY request to a [Authorize] endpoint
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,           // Reject tokens with wrong Issuer
        ValidateAudience = true,         // Reject tokens with wrong Audience
        ValidateLifetime = true,         // Reject expired tokens
        ValidateIssuerSigningKey = true, // Reject tokens with invalid signature
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        // By default, JWT allows a 5-minute clock skew. Setting to zero enforces exact expiry.
        ClockSkew = TimeSpan.Zero
    };
});

// ============================================================
// 3. AUTHORIZATION
// ============================================================
// Why AddAuthorization? This registers the authorization services that process
// [Authorize] and [Authorize(Roles = "...")] attributes on controllers.
builder.Services.AddAuthorization();

// ============================================================
// 4. DEPENDENCY INJECTION — Security Services
// ============================================================
// Why AddScoped? Both services are stateless but depend on IConfiguration (singleton).
// Scoped means: one instance per HTTP request, shared within that request.
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

// ============================================================
// 5. CONTROLLERS & SWAGGER
// ============================================================
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// ============================================================
// 6. MIDDLEWARE PIPELINE
// ============================================================
// Why order matters: middleware runs top-to-bottom in the pipeline.
// UseAuthentication MUST come BEFORE UseAuthorization.
// → UseAuthentication reads the token and populates HttpContext.User
// → UseAuthorization then checks HttpContext.User for [Authorize] attributes
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();

// ORDER IS CRITICAL: Authentication → Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
