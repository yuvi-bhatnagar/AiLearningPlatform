using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using FluentValidation;
using AiLearningPlatform.Application.Common.Interfaces;
using AiLearningPlatform.Application.Features.Courses;
using AiLearningPlatform.Application.Features.Quizzes;
using AiLearningPlatform.Application.Features.Questions;
using AiLearningPlatform.Infrastructure.Data;
using AiLearningPlatform.Infrastructure.Data.Repositories;
using AiLearningPlatform.Infrastructure.Security;
using AiLearningPlatform.API.Middleware;

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
// 4. DEPENDENCY INJECTION — Application & Infrastructure Services
// ============================================================
// Security Services
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();

// Repositories
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IQuizRepository, QuizRepository>();
builder.Services.AddScoped<IQuestionRepository, QuestionRepository>();

// Services
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IQuizService, QuizService>();
builder.Services.AddScoped<IQuestionService, QuestionService>();

// Validators
builder.Services.AddValidatorsFromAssembly(typeof(CourseService).Assembly);

// ============================================================
// 5. CONTROLLERS & SWAGGER (Swashbuckle with JWT Bearer)
// ============================================================
builder.Services.AddControllers();

// Why Swashbuckle instead of AddOpenApi()?
// We use Swashbuckle.AspNetCore (already installed) because it has stable, built-in
// support for JWT Bearer auth in Swagger UI via AddSecurityDefinition/AddSecurityRequirement.
// Microsoft.AspNetCore.OpenApi 10.x uses a different internal version of Microsoft.OpenApi
// that has breaking namespace changes — Swashbuckle is the battle-tested choice.
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AI Learning Platform API",
        Version = "v1",
        Description = "REST API for the AI Learning Platform"
    });

    // Step 1: Define the Bearer security scheme
    // This tells Swagger UI: "this API accepts JWT Bearer tokens"
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        // Using Type = Http with Scheme = "bearer" enables automatic prefixing of "Bearer " by Swagger UI.
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT access token here. Get one from POST /api/v1/auth/login or /register."
    });

    // Step 2: Apply the security requirement to ALL endpoints globally
    // This makes the 🔒 padlock appear on every route
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ============================================================
// 6. MIDDLEWARE PIPELINE
// ============================================================
// Why order matters: middleware runs top-to-bottom in the pipeline.
// UseMiddleware<ExceptionHandlingMiddleware> MUST run first to catch downstream exceptions.
// UseAuthentication MUST come BEFORE UseAuthorization.
// → UseAuthentication reads the token and populates HttpContext.User
// → UseAuthorization then checks HttpContext.User for [Authorize] attributes
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    // UseSwagger serves the OpenAPI JSON spec at /swagger/v1/swagger.json
    // UseSwaggerUI serves the browser UI at /swagger
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Learning Platform v1");
        // Collapse all operations by default for a cleaner first view
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

app.UseHttpsRedirection();

// ORDER IS CRITICAL: Authentication → Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ============================================================
// 7. ADMIN SEED
// ============================================================
// Why seed at startup?
// There must always be at least one Admin in the system.
// Admin cannot register via the public endpoint (by design).
// This seed runs every startup but is IDEMPOTENT — it checks if the admin
// already exists before inserting, so it never creates duplicates.
//
// Why use a scope?
// DbContext is registered as Scoped (per-request). Outside of an HTTP request
// (like startup), we must manually create a scope to resolve Scoped services.
// Using app.Services directly would fail for Scoped services.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var adminConfig = app.Configuration.GetSection("AdminSeed");

    var adminEmail = adminConfig["Email"];
    var adminUsername = adminConfig["Username"];
    var adminPassword = adminConfig["Password"];

    // Only seed if config is present AND admin doesn't already exist
    if (!string.IsNullOrEmpty(adminEmail) && !db.Users.Any(u => u.Email == adminEmail))
    {
        var admin = new AiLearningPlatform.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Username = adminUsername!,
            Email = adminEmail!,
            PasswordHash = hasher.Hash(adminPassword!),
            Role = AiLearningPlatform.Domain.Enums.UserRole.Admin,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(admin);
        db.SaveChanges();

        Console.WriteLine($"[Seed] Admin user created: {adminEmail}");
    }
}

app.Run();

public partial class Program { }
