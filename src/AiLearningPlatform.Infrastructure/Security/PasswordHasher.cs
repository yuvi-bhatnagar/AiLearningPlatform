using AiLearningPlatform.Application.Common.Interfaces;

namespace AiLearningPlatform.Infrastructure.Security;

// Why BCrypt?
// 1. It auto-generates a unique random salt per password (stored within the hash string itself)
//    → two users with the same password will have different hashes
//    → prevents precomputed "rainbow table" attacks
// 2. It has a configurable "work factor" that makes hashing intentionally slow (~100ms)
//    → brute-force attacks become computationally infeasible
// 3. The hash string contains the algorithm, work factor, and salt all in one:
//    $2a$12$[22 chars salt][31 chars hash]
//    BCrypt.Verify() reads the embedded salt from the stored hash automatically
public class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        // WorkFactor 12 = 2^12 = 4096 iterations. Slower than 10 (1024) but still < 200ms on modern hardware.
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool Verify(string password, string hash)
    {
        // BCrypt.Verify internally extracts the salt from the stored hash string
        // and re-hashes the provided password with that same salt, then compares
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
