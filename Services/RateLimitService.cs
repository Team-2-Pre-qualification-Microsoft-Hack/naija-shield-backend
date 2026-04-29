using System.Collections.Concurrent;

namespace naija_shield_backend.Services;

/// <summary>
/// In-memory login rate limiter. After 5 failed attempts from the same email,
/// the account is locked for 15 minutes and returns 429.
/// </summary>
public class RateLimitService
{
    private readonly ConcurrentDictionary<string, LoginAttemptInfo> _attempts = new();

    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Checks whether the given email is currently rate-limited.
    /// </summary>
    public bool IsRateLimited(string email)
    {
        var key = email.ToLowerInvariant();
        if (_attempts.TryGetValue(key, out var info))
        {
            if (info.LockedUntil.HasValue && info.LockedUntil.Value > DateTime.UtcNow)
            {
                return true;
            }

            // Lockout expired — reset
            if (info.LockedUntil.HasValue && info.LockedUntil.Value <= DateTime.UtcNow)
            {
                _attempts.TryRemove(key, out _);
            }
        }
        return false;
    }

    /// <summary>
    /// Records a failed login attempt. Triggers lockout after MaxAttempts.
    /// </summary>
    public void RecordFailedAttempt(string email)
    {
        var key = email.ToLowerInvariant();
        var info = _attempts.GetOrAdd(key, _ => new LoginAttemptInfo());

        info.FailedCount++;
        if (info.FailedCount >= MaxAttempts)
        {
            info.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
        }
    }

    /// <summary>
    /// Clears failed attempt tracking on successful login.
    /// </summary>
    public void ResetAttempts(string email)
    {
        var key = email.ToLowerInvariant();
        _attempts.TryRemove(key, out _);
    }

    private class LoginAttemptInfo
    {
        public int FailedCount { get; set; }
        public DateTime? LockedUntil { get; set; }
    }
}
