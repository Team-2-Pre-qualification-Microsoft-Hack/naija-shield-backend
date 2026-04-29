using Microsoft.AspNetCore.Mvc;
using naija_shield_backend.Models;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Public-facing Scammer Number Reputation API.
/// Any bank, fintech, or individual can query a phone number's fraud history
/// before picking up a call or responding to a message.
/// GET /api/numbers/{phone}/reputation
/// </summary>
[ApiController]
[Route("api/numbers")]
public sealed class ReputationController : ControllerBase
{
    private readonly IIncidentRepository _repository;
    private readonly ILogger<ReputationController> _logger;

    public ReputationController(IIncidentRepository repository, ILogger<ReputationController> logger)
    {
        _repository = repository;
        _logger     = logger;
    }

    /// <summary>
    /// Returns the fraud reputation of a phone number based on all NaijaShield-detected incidents.
    /// The reputation score is a weighted average of risk scores, boosted by recency and severity.
    /// A score of 0 means no known incidents — this does NOT guarantee the number is safe.
    /// </summary>
    [HttpGet("{phone}/reputation")]
    public async Task<IActionResult> GetReputation(
        string phone,
        CancellationToken cancellationToken = default)
    {
        // Normalise: strip spaces
        phone = phone.Trim();

        _logger.LogInformation("[Reputation] Query for phone={Phone}", phone);

        var incidents = await _repository.GetByPhoneAsync(phone, limit: 100, cancellationToken);

        if (incidents.Count == 0)
        {
            return Ok(new
            {
                phone,
                reputationScore = 0,
                verdict         = "UNKNOWN",
                totalIncidents  = 0,
                message         = "No incidents on record. This does not guarantee the number is safe — it may not have been seen by NaijaShield yet."
            });
        }

        var score   = CalculateReputationScore(incidents);
        var verdict = score switch
        {
            >= 80 => "HIGH_RISK",
            >= 50 => "SUSPICIOUS",
            >= 20 => "LOW_RISK",
            _     => "CLEAN"
        };

        // Top classifications by frequency
        var topClassifications = incidents
            .GroupBy(i => i.Classification)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => new { classification = g.Key, count = g.Count() });

        // Five most recent incidents for context
        var recent = incidents
            .Take(5)
            .Select(i => new
            {
                i.Id,
                i.Timestamp,
                i.Channel,
                i.RiskScore,
                i.Classification,
                i.Status,
                i.Preview
            });

        return Ok(new
        {
            phone,
            reputationScore    = score,
            verdict,
            totalIncidents     = incidents.Count,
            firstSeen          = incidents.Min(i => i.Timestamp),
            lastSeen           = incidents.Max(i => i.Timestamp),
            breakdown          = new
            {
                blocked    = incidents.Count(i => i.Status == "Blocked"),
                monitoring = incidents.Count(i => i.Status == "Monitoring"),
                allowed    = incidents.Count(i => i.Status == "Allowed")
            },
            topClassifications,
            channels           = incidents.Select(i => i.Channel).Distinct(),
            recentIncidents    = recent
        });
    }

    // Weighted average of risk scores.
    // Recency and severity both boost weight so recent/blocked incidents dominate.
    private static int CalculateReputationScore(IReadOnlyList<ThreatIncident> incidents)
    {
        if (incidents.Count == 0) return 0;

        var now            = DateTimeOffset.UtcNow;
        double totalWeight = 0;
        double weighted    = 0;

        foreach (var inc in incidents)
        {
            var ts      = DateTimeOffset.TryParse(inc.Timestamp, out var dt) ? dt : now;
            var daysAgo = (now - ts).TotalDays;

            var recency = daysAgo <= 7  ? 2.0
                        : daysAgo <= 30 ? 1.5
                        : 1.0;

            var severity = inc.Status switch
            {
                "Blocked"    => 1.3,
                "Monitoring" => 1.0,
                _            => 0.4
            };

            var w       = recency * severity;
            weighted   += inc.RiskScore * w;
            totalWeight += w;
        }

        return (int)Math.Round(Math.Clamp(weighted / totalWeight, 0, 100));
    }
}
