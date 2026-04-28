using Newtonsoft.Json;

namespace naija_shield_backend.Models;

/// <summary>
/// Refresh token entity stored in Cosmos DB. Partition key: /userId
/// </summary>
public class RefreshToken
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("token")]
    public string Token { get; set; } = string.Empty;

    [JsonProperty("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
