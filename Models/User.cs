using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace naija_shield_backend.Models;

/// <summary>
/// User entity stored in Cosmos DB. Partition key: /id
/// </summary>
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
    [JsonConverter(typeof(StringEnumConverter))]
    public UserRole Role { get; set; }

    [JsonProperty("organisation")]
    public string Organisation { get; set; } = string.Empty;

    [JsonProperty("status")]
    [JsonConverter(typeof(StringEnumConverter))]
    public UserStatus Status { get; set; }

    [JsonProperty("inviteToken")]
    public string? InviteToken { get; set; }

    [JsonProperty("inviteExpiry")]
    public DateTime? InviteExpiry { get; set; }

    [JsonProperty("lastActive")]
    public DateTime LastActive { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}
