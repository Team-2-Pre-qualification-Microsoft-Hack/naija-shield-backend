using Newtonsoft.Json;

namespace naija_shield_backend.Models;

/// <summary>
/// Cosmos DB document that persists every analysed telecom event.
/// The partition key path is /channel, so SMS and Voice incidents
/// are stored in separate logical partitions for efficient querying.
/// Newtonsoft.Json attributes are used because the Cosmos DB SDK v3
/// defaults to Newtonsoft for serialisation.
/// </summary>
public class ThreatIncident
{
    /// <summary>Unique incident identifier in the format INC-XXXXXXXX.</summary>
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    /// <summary>ISO 8601 UTC timestamp of when the event was ingested.</summary>
    [JsonProperty("timestamp")]
    public string Timestamp { get; set; } = default!;

    /// <summary>Telecom channel: SMS | Voice | WhatsApp.</summary>
    [JsonProperty("channel")]
    public string Channel { get; set; } = default!;

    /// <summary>Sender MSISDN after PII redaction applied by Presidio.</summary>
    [JsonProperty("from")]
    public string From { get; set; } = default!;

    /// <summary>First 80 characters of the redacted message text or transcript.</summary>
    [JsonProperty("preview")]
    public string Preview { get; set; } = default!;

    /// <summary>Composite risk score 0–100 (for Voice this blends LLM + deepfake scores).</summary>
    [JsonProperty("riskScore")]
    public int RiskScore { get; set; }

    /// <summary>LLM classification label: OTP_PHISH | VISHING | IMPERSONATION | SOCIAL_ENGINEERING | SAFE.</summary>
    [JsonProperty("classification")]
    public string Classification { get; set; } = default!;

    /// <summary>LLM explanation for the risk score.</summary>
    [JsonProperty("explanation")]
    public string Explanation { get; set; } = default!;

    /// <summary>Action taken: Blocked | Monitoring | Allowed.</summary>
    [JsonProperty("status")]
    public string Status { get; set; } = default!;

    /// <summary>Full redacted payload text. Raw PII is never stored.</summary>
    [JsonProperty("rawPayload")]
    public string RawPayload { get; set; } = default!;

    /// <summary>Voice-only: deepfake probability score 0.0–1.0 returned by the AI sidecar.</summary>
    [JsonProperty("deepfakeScore", NullValueHandling = NullValueHandling.Ignore)]
    public double? DeepfakeScore { get; set; }

    /// <summary>Approximate latitude of the sender derived from phone prefix lookup.</summary>
    [JsonProperty("lat", NullValueHandling = NullValueHandling.Ignore)]
    public double? Lat { get; set; }

    /// <summary>Approximate longitude of the sender derived from phone prefix lookup.</summary>
    [JsonProperty("lng", NullValueHandling = NullValueHandling.Ignore)]
    public double? Lng { get; set; }

    /// <summary>Nigerian state the sender number is mapped to.</summary>
    [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
    public string? State { get; set; }

    /// <summary>Local Government Area within the state.</summary>
    [JsonProperty("lga", NullValueHandling = NullValueHandling.Ignore)]
    public string? Lga { get; set; }

    /// <summary>Destination MSISDN — the victim or recipient being targeted by the scammer.</summary>
    [JsonProperty("to", NullValueHandling = NullValueHandling.Ignore)]
    public string? To { get; set; }

    /// <summary>BCP-47 language code detected by the LLM: en | pidgin | yo | ha | ig.</summary>
    [JsonProperty("detectedLanguage", NullValueHandling = NullValueHandling.Ignore)]
    public string? DetectedLanguage { get; set; }
}
