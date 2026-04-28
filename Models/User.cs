using Newtonsoft.Json;

namespace naija_shield_backend.Models;

public class User
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;

    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("organisation")]
    public string Organisation { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = UserStatus.Pending;

    [JsonProperty("inviteToken")]
    public string? InviteToken { get; set; }

    [JsonProperty("inviteExpiry")]
    public DateTime? InviteExpiry { get; set; }

    [JsonProperty("lastActive")]
    public DateTime? LastActive { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonProperty("refreshTokenExpiry")]
    public DateTime? RefreshTokenExpiry { get; set; }

    [JsonProperty("failedLoginAttempts")]
    public int FailedLoginAttempts { get; set; } = 0;

    [JsonProperty("lockoutUntil")]
    public DateTime? LockoutUntil { get; set; }

    // Cosmos DB partition key
    [JsonProperty("type")]
    public string Type { get; set; } = "user";
}
