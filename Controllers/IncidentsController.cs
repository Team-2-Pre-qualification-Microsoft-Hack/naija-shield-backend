using Microsoft.AspNetCore.Mvc;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Serves incident data to the Enterprise Dashboard.
/// — GET /api/incidents        — paginated threat feed table
/// — GET /api/incidents/heatmap — lat/lng points for Azure Maps heatmap
/// — GET /api/incidents/stats  — KPI summary cards
/// </summary>
[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IIncidentRepository _repository;
    private readonly ILogger<IncidentsController> _logger;

    public IncidentsController(IIncidentRepository repository, ILogger<IncidentsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/incidents?limit=50
    // Threat feed table — most recent incidents, newest first.
    // ─────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);
        var incidents = await _repository.GetRecentAsync(limit, cancellationToken);

        var items = incidents.Select(i => new
        {
            i.Id,
            i.Timestamp,
            i.Channel,
            i.From,
            i.Preview,
            i.RiskScore,
            i.Classification,
            i.Explanation,
            i.Status,
            i.State,
            i.Lga,
            i.DeepfakeScore
        });

        return Ok(new { total = incidents.Count, items });
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/incidents/heatmap?limit=200
    // Azure Maps heatmap — returns only the fields the map needs.
    // weight = riskScore (0–100), used as the heatmap intensity value.
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var incidents = await _repository.GetRecentAsync(limit, cancellationToken);

        // Only return incidents that have coordinates
        var points = incidents
            .Where(i => i.Lat.HasValue && i.Lng.HasValue)
            .Select(i => new
            {
                i.Id,
                lat     = i.Lat!.Value,
                lng     = i.Lng!.Value,
                i.State,
                i.Lga,
                weight  = i.RiskScore,
                i.Status,
                i.Channel,
                i.Classification
            });

        return Ok(points);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/incidents/stats
    // KPI summary cards: total, blocked, monitoring, allowed counts.
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken = default)
    {
        var incidents = await _repository.GetRecentAsync(500, cancellationToken);

        var stats = new
        {
            total      = incidents.Count,
            blocked    = incidents.Count(i => i.Status == "Blocked"),
            monitoring = incidents.Count(i => i.Status == "Monitoring"),
            allowed    = incidents.Count(i => i.Status == "Allowed"),
            avgRisk    = incidents.Count > 0
                ? (int)Math.Round(incidents.Average(i => i.RiskScore))
                : 0,
            byChannel  = incidents
                .GroupBy(i => i.Channel)
                .Select(g => new { channel = g.Key, count = g.Count() }),
            byState    = incidents
                .Where(i => i.State != null)
                .GroupBy(i => i.State!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { state = g.Key, count = g.Count() })
        };

        return Ok(stats);
    }
}
