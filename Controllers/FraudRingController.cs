using Microsoft.AspNetCore.Mvc;
using naija_shield_backend.Services;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Fraud Ring Intelligence API.
///
/// GET /api/fraud-rings               — all rings detected in the last N hours
/// GET /api/fraud-rings/{ringId}      — full ring profile with all incident details
///
/// No auth required — intended as a public intelligence feed for banks, fintechs,
/// and law enforcement consumers (same philosophy as the Reputation API).
/// </summary>
[ApiController]
[Route("api/fraud-rings")]
public sealed class FraudRingController : ControllerBase
{
    private readonly FraudRingService _rings;
    private readonly ILogger<FraudRingController> _logger;

    public FraudRingController(FraudRingService rings, ILogger<FraudRingController> logger)
    {
        _rings  = rings;
        _logger = logger;
    }

    /// <summary>
    /// Returns all fraud rings detected within the last <paramref name="hours"/> hours.
    /// A ring is a connected component of ≥ <paramref name="minCallers"/> distinct
    /// scammer numbers linked by shared victims or time-proximate burst activity.
    ///
    /// Results are ordered by RingSeverityScore descending (most dangerous first).
    /// </summary>
    /// <param name="hours">Look-back window in hours. Default 72 (3 days).</param>
    /// <param name="minCallers">Minimum distinct callers for a group to qualify as a ring. Default 2.</param>
    [HttpGet]
    public async Task<IActionResult> GetRings(
        [FromQuery] int hours      = 72,
        [FromQuery] int minCallers = 2,
        CancellationToken cancellationToken = default)
    {
        hours      = Math.Clamp(hours,      1, 720);  // max 30 days
        minCallers = Math.Clamp(minCallers, 2, 20);

        _logger.LogInformation("[FraudRing] Ring query hours={Hours} minCallers={Min}", hours, minCallers);

        var rings = await _rings.DetectRingsAsync(hours, minCallers, cancellationToken);

        return Ok(new
        {
            analysisWindowHours = hours,
            minCallers,
            ringsDetected       = rings.Count,
            generatedAt         = DateTimeOffset.UtcNow.ToString("o"),
            rings               = rings.Select(r => new
            {
                r.RingId,
                r.RingSeverityScore,
                r.IsActive,
                r.EdgeBasis,
                r.DominantClassification,
                r.Classifications,
                r.TotalIncidents,
                callerCount         = r.CallerNumbers.Count,
                victimCount         = r.VictimNumbers.Count,
                r.CallerNumbers,
                r.VictimNumbers,
                r.Channels,
                r.Languages,
                r.States,
                r.FirstSeen,
                r.LastSeen
            })
        });
    }

    /// <summary>
    /// Returns the full profile for one ring, including every incident attributed to it.
    /// The incident list carries lat/lng coordinates so the frontend can render a map
    /// of all activity locations for this ring.
    /// </summary>
    [HttpGet("{ringId}")]
    public async Task<IActionResult> GetRingDetail(
        string ringId,
        [FromQuery] int hours = 72,
        CancellationToken cancellationToken = default)
    {
        hours = Math.Clamp(hours, 1, 720);

        var detail = await _rings.GetRingDetailAsync(ringId, hours, cancellationToken);

        if (detail is null)
            return NotFound(new
            {
                error   = $"Ring '{ringId}' not found in the last {hours}-hour window.",
                hint    = "Ring IDs are recomputed on each query. Try GET /api/fraud-rings to see current rings."
            });

        return Ok(new
        {
            detail.RingId,
            detail.RingSeverityScore,
            detail.IsActive,
            detail.EdgeBasis,
            detail.DominantClassification,
            detail.Classifications,
            detail.TotalIncidents,
            callerCount = detail.CallerNumbers.Count,
            victimCount = detail.VictimNumbers.Count,
            detail.CallerNumbers,
            detail.VictimNumbers,
            detail.Channels,
            detail.Languages,
            detail.States,
            detail.FirstSeen,
            detail.LastSeen,
            incidents   = detail.Incidents.Select(i => new
            {
                i.Id,
                i.Timestamp,
                i.Channel,
                i.From,
                i.To,
                i.RiskScore,
                i.Classification,
                i.Status,
                i.DetectedLanguage,
                i.Preview,
                i.Lat,
                i.Lng,
                i.State,
                i.Lga
            })
        });
    }
}
