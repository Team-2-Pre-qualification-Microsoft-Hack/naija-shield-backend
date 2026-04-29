namespace naija_shield_backend.Models;

/// <summary>
/// SignalR payload broadcast to connected Enterprise Dashboard clients
/// on the "NewThreatDetected" event. Property names map directly to the
/// Threat Feed table columns: ID, Time, Channel, Preview, Risk Score, Status.
/// </summary>
public class ThreatFeedEvent
{
    /// <summary>Incident identifier, e.g. INC-A3F7B2C1.</summary>
    public string Id { get; set; } = default!;

    /// <summary>ISO 8601 UTC timestamp of ingestion.</summary>
    public string Time { get; set; } = default!;

    /// <summary>Telecom channel: SMS | Voice | WhatsApp.</summary>
    public string Channel { get; set; } = default!;

    /// <summary>First 80 characters of the redacted message or transcript.</summary>
    public string Preview { get; set; } = default!;

    /// <summary>Composite risk score 0–100.</summary>
    public int RiskScore { get; set; }

    /// <summary>Current status: Blocked | Monitoring | Allowed.</summary>
    public string Status { get; set; } = default!;
}
