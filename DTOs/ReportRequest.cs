namespace naija_shield_backend.DTOs;

public sealed class ReportRequest
{
    /// <summary>Target agency: CBN | NCC | EFCC | NDPC | GENERAL</summary>
    public string AgencyType { get; set; } = "GENERAL";

    /// <summary>Start of the reporting period (UTC). Defaults to 30 days ago.</summary>
    public DateTimeOffset PeriodFrom { get; set; } = DateTimeOffset.UtcNow.AddDays(-30);

    /// <summary>End of the reporting period (UTC). Defaults to now.</summary>
    public DateTimeOffset PeriodTo { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>How many high-risk incidents to include in the detail section (max 50).</summary>
    public int MaxIncidentDetails { get; set; } = 20;
}
