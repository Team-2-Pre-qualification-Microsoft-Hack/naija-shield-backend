using Newtonsoft.Json;

namespace naija_shield_backend.Models;

/// <summary>
/// SMS threat event stored in Cosmos DB. Partition key: /id
/// </summary>
public class SmsEvent
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("atMessageId")]
    public string AtMessageId { get; set; } = string.Empty;

    [JsonProperty("from")]
    public string From { get; set; } = string.Empty;

    [JsonProperty("to")]
    public string To { get; set; } = string.Empty;

    [JsonProperty("rawText")]
    public string RawText { get; set; } = string.Empty;

    [JsonProperty("redactedText")]
    public string RedactedText { get; set; } = string.Empty;

    [JsonProperty("decision")]
    public string Decision { get; set; } = "ALLOW";

    [JsonProperty("confidence")]
    public float Confidence { get; set; }

    [JsonProperty("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonProperty("warningSent")]
    public bool WarningSent { get; set; }

    [JsonProperty("receivedAt")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
