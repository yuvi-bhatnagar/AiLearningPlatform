# AI Learning Platform — 12-Module Step-by-Step Build Plan (Local-First)

**Approach:** Build and run everything locally first (Docker for SQL Server/Redis/RabbitMQ if you want them containerized). Deploy to free-tier cloud only after each module works locally. Each module = one learning milestone with clear "done when" criteria.

**Testing approach:** Write tests as you go, not at the end. Add xUnit test projects in Module 1 (`Application.Tests` for unit tests, `API.IntegrationTests` for integration tests), then add tests incrementally in every module below. This mirrors how real teams work and is a strong resume signal ("tests written alongside features, not bolted on at the end").

**Testing stack:**
| Purpose | Tool |
|---|---|
| Unit testing framework | **xUnit** |
| Mocking dependencies | **Moq** (or NSubstitute) |
| Fluent assertions | **FluentAssertions** |
| Integration testing (API + DB) | **WebApplicationFactory** (`Microsoft.AspNetCore.Mvc.Testing`) |
| Real DB in tests (no mocks) | **Testcontainers** (spins up real SQL Server/Redis in Docker for test runs) |
| Frontend testing | **Vitest** + **React Testing Library** |
| API contract testing (manual) | Swagger/Postman (already covered) |
| Load/perf sanity check (optional) | **k6** (free, scriptable) — lets you *simulate* concurrent users locally even without real 1 lakh traffic |

---

## Module 1 — Project Setup & Solution Structure
**Learn:** Clean architecture folder structure, .NET solution/project references

**Tasks:**
- Install .NET SDK, Node.js, Docker Desktop, SQL Server (local install or Docker container)
- Create solution: `dotnet new sln -n AiLearningPlatform`
- Create projects: `API`, `Application`, `Domain`, `Infrastructure`
- Add project references (API → Application → Domain, Infrastructure → Application)
- Create React app: `npm create vite@latest client -- --template react`
- Create test projects: `Application.Tests` (xUnit) and `API.IntegrationTests` (xUnit + `Microsoft.AspNetCore.Mvc.Testing`)
- Add `Moq` and `FluentAssertions` NuGet packages to `Application.Tests`
- Write one placeholder test in each test project to confirm the test runner works (`dotnet test`)
- Push initial commit to GitHub

**Done when:** `dotnet run` on API returns default Swagger page; `npm run dev` on client shows default React page; `dotnet test` runs and passes the placeholder tests.

---

## Module 2 — Database Setup + EF Core + First Migration
**Learn:** EF Core Code-First, migrations, DbContext, local SQL Server via Docker

**Tasks:**
- Run SQL Server in Docker (`docker run` with SQL Server image) OR install locally
- Create entities: `User`, `Course`, `Quiz`, `Question`, `Attempt`, `AnswerSubmission`
- Create `AppDbContext`, register in `Program.cs`
- Add connection string to `appsettings.Development.json` (local only, never commit real secrets)
- Run first migration: `dotnet ef migrations add InitialCreate`, `dotnet ef database update`
- **Test:** Write a simple integration test that spins up `AppDbContext` against a test database (or in-memory provider for now) and confirms it can save/retrieve a `Course` entity

**Done when:** Tables exist in local SQL Server, verified via SSMS/Azure Data Studio; DbContext test passes.

---

## Module 3 — Auth & RBAC (JWT)
**Learn:** JWT issuing/validation, password hashing, role-based authorization

**Tasks:**
- Add `Register`/`Login` endpoints, hash passwords (BCrypt or ASP.NET Identity)
- Issue JWT access token + refresh token on login
- Add roles: `Student`, `Teacher`, `Admin` as claims
- Protect endpoints with `[Authorize(Roles = "...")]`
- Test with Swagger's "Authorize" button or Postman
- **Test (unit):** Password hashing/verification logic, JWT token generation returns expected claims
- **Test (integration):** `WebApplicationFactory` test hitting `/register` and `/login`, asserting token is returned; a protected endpoint returns 401 without token and 200 with a valid token

**Done when:** You can register a user, log in, get a token, and hit a protected endpoint successfully/unsuccessfully based on role; auth tests pass in `dotnet test`.

---

## Module 4 — Core CRUD: Courses, Quizzes, Questions
**Learn:** Repository pattern, DTOs, FluentValidation, API versioning

**Tasks:**
- Repository + Service layer for Course/Quiz/Question CRUD
- DTOs for request/response (never expose entities directly)
- FluentValidation validators for all incoming DTOs
- API versioning setup (`/api/v1/...`)
- Global exception handling middleware (consistent error JSON shape)
- **Test (unit):** FluentValidation validators — valid DTO passes, invalid DTO (missing title, negative values, etc.) fails with expected error
- **Test (unit):** Service layer methods (mock the repository with Moq) — e.g. `CreateCourseAsync` calls repository once with correct data
- **Test (integration):** Full CRUD flow for Course via `WebApplicationFactory` (create → get → update → delete), including a 403 test for wrong role

**Done when:** Teacher role can create/edit/delete a course and quiz with questions via Swagger, with validation errors returning clean responses; CRUD + validation tests pass.

---

## Module 5 — Quiz Attempt Flow (Student Side)
**Learn:** Business logic layering, timed operations, status tracking

**Tasks:**
- Endpoint: start attempt (records `StartedAtUtc`)
- Endpoint: submit answers (records `SubmittedAtUtc`, calculates MCQ score immediately)
- Attempt status: `InProgress` → `PendingGrading` → `Graded`
- Basic scoring logic for MCQs (auto-graded)
- **Test (unit):** MCQ scoring logic — correct answer scores full marks, wrong answer scores zero, edge case (unanswered question)
- **Test (unit):** Attempt status transitions (`InProgress` → `PendingGrading` → `Graded`) follow expected rules

**Done when:** A student can start a quiz, submit answers, and see MCQ score instantly; subjective answers sit as `PendingGrading`; scoring logic tests pass.

---

## Module 6 — AI Integration (Quiz Generation + Answer Evaluation)
**Learn:** External API integration, abstraction/interfaces, Polly resilience

**Tasks:**
- Get free API key (Google Gemini or Groq)
- Create `IAiService` interface + concrete implementation (`GeminiAiService`)
- Method 1: `GenerateQuizAsync(topic, count)` → returns MCQs to save via Teacher flow
- Method 2: `EvaluateAnswerAsync(question, studentAnswer)` → returns score + feedback + confidence
- Wrap external calls in Polly (retry + circuit breaker)
- Add rate limiting middleware on AI endpoints
- **Test (unit):** Mock `IAiService` in tests that depend on it — never call the real Gemini/Groq API in automated tests (costs quota, flaky, slow)
- **Test (unit):** Polly retry policy triggers correctly on a simulated transient failure (use a fake `HttpMessageHandler`)
- **Test (unit):** Rate limiter blocks requests beyond the configured threshold

**Done when:** Teacher can generate a quiz from a topic string; a subjective answer gets AI feedback when called directly (still synchronous at this stage — async comes in Module 7); AI service is fully mockable and tested without hitting the real API.

---

## Module 7 — Background Processing (Hangfire)
**Learn:** Async job processing, decoupling slow operations from HTTP requests

**Tasks:**
- Add Hangfire with SQL Server storage (same local DB)
- On quiz submission with subjective answers → enqueue background job instead of calling AI synchronously
- Background job calls `IAiService.EvaluateAnswerAsync`, updates `AnswerSubmission` + attempt status to `Graded`
- Add Hangfire dashboard (secured, Admin-only)
- **Test (unit):** The job method itself (e.g. `GradeAnswerJob.ExecuteAsync`) — mock `IAiService`, assert it updates the answer and attempt status correctly
- **Test (integration):** Submitting a quiz enqueues a job (can assert via Hangfire's in-memory test storage or by checking the job table)

**Done when:** Submitting a quiz with subjective questions returns instantly; grading completes a few seconds later in the background, visible in Hangfire dashboard; job logic is unit tested independent of Hangfire's scheduler.

---

## Module 8 — Batch Jobs, Stored Procedures/Views, Optimized Queries
**Learn:** SQL views, recurring jobs, query optimization, indexing

**Tasks:**
- Create SQL View: `LeaderboardView` (aggregated scores/ranks)
- Create stored procedure: student performance summary
- Recurring Hangfire job (nightly): recompute leaderboard cache, reset streaks
- Add indexes on FK columns and frequently filtered columns (`UserId`, `QuizId`)
- Refactor key queries to use `AsNoTracking()` and projections (`Select()`)
- **Test (integration, Testcontainers):** Spin up a real SQL Server container in tests, seed data, query the `LeaderboardView`/stored procedure, assert correct ranking output — this is the point where in-memory EF provider isn't enough, since views/SPs need a real SQL engine
- **Test (optional, k6):** Simple load script hitting the leaderboard endpoint with simulated concurrent requests, just to see cache/query behavior under load locally

**Done when:** Leaderboard data comes from the SQL view, recurring job runs nightly (test by triggering manually), query execution plans show index usage, and Testcontainers test confirms the view/SP output is correct.

---

## Module 9 — Caching (Redis)
**Learn:** Cache-aside pattern, cache invalidation

**Tasks:**
- Run Redis locally via Docker (`docker run redis`)
- Add `StackExchange.Redis` client
- Cache: leaderboard results, course list
- Invalidate cache on relevant writes (new course, leaderboard recompute)
- **Test (unit):** Cache-aside logic — mock `IDistributedCache`/Redis client, assert cache is checked first, DB is only hit on miss, and cache is set after a miss
- **Test (integration, Testcontainers):** Real Redis container — write, read, invalidate, confirm behavior end-to-end

**Done when:** Leaderboard endpoint serves from cache on repeat calls (verify via logs/timing), cache clears correctly after nightly recompute, and cache-aside logic is tested against both mocked and real Redis.

---

## Module 10 — Logging, Health Checks, Exception Handling Polish
**Learn:** Structured logging, observability basics

**Tasks:**
- Add Serilog (console + rolling file sink)
- Add `/health` endpoint checking DB, Redis, Hangfire
- Add audit log table for key actions (login, quiz submit, grade override)
- Review/finalize global exception middleware (map exception types to proper HTTP status codes)
- **Test (integration):** `/health` endpoint returns healthy when dependencies are up; simulate a dependency being down (e.g. wrong connection string in a test config) and assert it reports unhealthy
- **Test (unit):** Global exception middleware maps a thrown `ValidationException`/`NotFoundException` to the correct HTTP status + error shape

**Done when:** Logs show structured entries for key actions; `/health` returns healthy/unhealthy per dependency; health check and exception-mapping tests pass.

---

## Module 11 — React Frontend (Full Integration)
**Learn:** Role-based routing, API integration, responsive design, timezone handling

**Tasks:**
- Build Student dashboard: browse courses, attempt quiz, view results/leaderboard
- Build Teacher dashboard: manage courses/quizzes, AI-generate quiz, review flagged low-confidence AI answers
- Build Admin dashboard: user management, AI usage stats, health check status view
- Role-based routing (redirect based on JWT role claim)
- Tailwind CSS responsive layout (mobile breakpoints)
- Display all timestamps converted from UTC to browser local time
- **Test (frontend, Vitest + React Testing Library):** Key components render correctly (e.g. quiz timer counts down, leaderboard renders rows, role-based route redirects unauthenticated users to login)
- **Test (frontend):** Timezone conversion utility — given a UTC ISO string, returns correct local display string

**Done when:** All three roles have a working, responsive dashboard fully wired to the API — no more Swagger-only testing; core component tests pass with `npm test`.

---

## Module 12 — Dockerize, Secrets Cleanup, and Deploy
**Learn:** Containerization, environment-based config, free-tier cloud deployment

**Tasks:**
- Write `Dockerfile` for API, `docker-compose.yml` for full local stack (API + SQL Server + Redis + RabbitMQ if used)
- Move all secrets to environment variables (remove anything from `appsettings.json`)
- Test full stack locally via `docker-compose up`
- Deploy: API → Render (env vars set in dashboard), React → Vercel, DB → Azure SQL free tier / Supabase, Redis → Upstash
- Lock down CORS to Vercel domain only
- Write final README (architecture diagram, setup steps, free-tier limitations, screenshots)
- **GitHub Actions CI:** workflow runs `dotnet test` and `npm test` on every push/PR; block merges (or at least flag) if tests fail
- **Test:** Run the full test suite one final time against the Dockerized stack (`docker-compose up` + `dotnet test`) to confirm nothing breaks in the containerized environment before deploying

**Done when:** App is live on public URLs, working end-to-end, with README complete, repo public on GitHub, and CI pipeline green with all tests passing.

---

## How to Use This Plan
- Do modules **in order** — each depends on the previous one working.
- Commit to GitHub at the end of every module (small, reviewable commits = good resume signal too).
- Don't touch Docker/cloud until Module 12 unless you want Docker for local DB/Redis convenience earlier — that's optional and doesn't change the order.
- If you get stuck on any module, that's the point to ask for a deep dive (code-level walkthrough) rather than skipping ahead.
- **Run `dotnet test` (and `npm test` from Module 11 onward) at the end of every module** — never let a module count as "done" if its tests are red. Testcontainers-based tests (Modules 8–9) need Docker running locally to pass.
- Never call the real AI API (Gemini/Groq) inside automated tests — always mock `IAiService`. This keeps your test suite fast, free, and not rate-limited.
