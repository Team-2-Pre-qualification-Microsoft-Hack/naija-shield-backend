namespace naija_shield_backend.Models;

/// <summary>
/// A group of phone numbers identified by the graph analysis as operating together
/// as a coordinated fraud ring. Rings are computed in-memory from recent incidents —
/// they are not persisted to Cosmos DB.
/// </summary>
public class FraudRing
{
    /// <summary>
    /// Deterministic identifier derived from a SHA-256 of the sorted caller numbers.
    /// The same real-world ring gets the same ID across successive analysis runs.
    /// Format: RING-XXXXXX.
    /// </summary>
    public string RingId { get; set; } = default!;

    /// <summary>Distinct scammer numbers (from) in this ring.</summary>
    public List<string> CallerNumbers { get; set; } = [];

    /// <summary>Distinct victim numbers (to) targeted by this ring.</summary>
    public List<string> VictimNumbers { get; set; } = [];

    /// <summary>Total number of incidents attributed to this ring.</summary>
    public int TotalIncidents { get; set; }

    /// <summary>Most frequently occurring classification across all ring incidents.</summary>
    public string DominantClassification { get; set; } = default!;

    /// <summary>All distinct classifications found in ring incidents.</summary>
    public List<string> Classifications { get; set; } = [];

    /// <summary>All distinct telecom channels used: SMS, Voice, WhatsApp.</summary>
    public List<string> Channels { get; set; } = [];

    /// <summary>All distinct detected languages across ring incidents.</summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>Nigerian states the ring's callers are mapped to.</summary>
    public List<string> States { get; set; } = [];

    /// <summary>ISO 8601 UTC timestamp of the earliest incident in the ring.</summary>
    public string FirstSeen { get; set; } = default!;

    /// <summary>ISO 8601 UTC timestamp of the most recent incident in the ring.</summary>
    public string LastSeen { get; set; } = default!;

    /// <summary>
    /// Composite severity score 0–100.
    /// Weighted by average incident risk score, number of callers, number of victims,
    /// and whether the ring is active within the last 24 hours.
    /// </summary>
    public int RingSeverityScore { get; set; }

    /// <summary>
    /// How the graph edges were established:
    /// SharedVictim | TimeProximity | Mixed.
    /// SharedVictim is the strongest signal (same victim called by multiple numbers).
    /// TimeProximity links callers with the same classification within a 90-minute burst.
    /// </summary>
    public string EdgeBasis { get; set; } = default!;

    /// <summary>True if the ring had activity in the last 24 hours.</summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Extended ring profile returned by GET /api/fraud-rings/{ringId}.
/// Includes the full incident list so the frontend can render timelines and maps.
/// </summary>
public class FraudRingDetail : FraudRing
{
    public List<ThreatIncident> Incidents { get; set; } = [];
}
