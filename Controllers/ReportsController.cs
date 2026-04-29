using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using naija_shield_backend.DTOs;
using naija_shield_backend.Models;
using naija_shield_backend.Services;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Generates and serves compliance-ready regulatory reports for agencies
/// such as CBN, NCC, EFCC, and NDPC.
///
/// — POST /api/reports          — generate + persist a new report
/// — GET  /api/reports          — list saved reports (metadata only)
/// — GET  /api/reports/{id}     — retrieve a full saved report
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private static readonly HashSet<string> ValidAgencyTypes =
        ["CBN", "NCC", "EFCC", "NDPC", "GENERAL"];

    private static readonly Dictionary<string, string> AgencyFrameworks = new()
    {
        ["CBN"]     = "CBN Cybersecurity Framework 2023",
        ["NCC"]     = "NCC Consumer Code of Practice Regulations",
        ["EFCC"]    = "EFCC (Establishment) Act — Financial Crimes Reporting",
        ["NDPC"]    = "Nigeria Data Protection Act (NDPA) 2023",
        ["GENERAL"] = "NaijaShield Internal Security Policy v1.0"
    };

    private readonly IIncidentRepository _incidents;
    private readonly IReportRepository _reports;
    private readonly ReportNarrativeService _narrative;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IIncidentRepository incidents,
        IReportRepository reports,
        ReportNarrativeService narrative,
        ILogger<ReportsController> logger)
    {
        _incidents = incidents;
        _reports   = reports;
        _narrative = narrative;
        _logger    = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // POST /api/reports
    // Generates a compliance-ready report for the requested agency and
    // persists it so it can be retrieved or shared later.
    // ─────────────────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> GenerateReport(
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken)
    {
        request.AgencyType = request.AgencyType.ToUpperInvariant();

        if (!ValidAgencyTypes.Contains(request.AgencyType))
            return BadRequest(new { error = $"Invalid agencyType. Must be one of: {string.Join(", ", ValidAgencyTypes)}" });

        if (request.PeriodFrom >= request.PeriodTo)
            return BadRequest(new { error = "PeriodFrom must be earlier than PeriodTo." });

        if ((request.PeriodTo - request.PeriodFrom).TotalDays > 366)
            return BadRequest(new { error = "Reporting period cannot exceed 366 days." });

        request.MaxIncidentDetails = Math.Clamp(request.MaxIncidentDetails, 1, 50);

        var generatedBy = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue("email")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";

        _logger.LogInformation(
            "[Reports] Generating {Agency} report for {From} → {To} by {User}",
            request.AgencyType, request.PeriodFrom, request.PeriodTo, generatedBy);

        var incidents = await _incidents.GetByDateRangeAsync(
            request.PeriodFrom, request.PeriodTo, cancellationToken);

        var report = BuildReport(request, incidents, generatedBy);

        // Generate AI-authored prose narrative from the computed statistics
        report.Narrative = await _narrative.GenerateAsync(report, cancellationToken);

        await _reports.SaveAsync(report, cancellationToken);

        _logger.LogInformation("[Reports] Report saved Id={Id}", report.Id);
        return Ok(report);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/reports?limit=20
    // Returns report metadata (no incident detail lists) for the dashboard.
    // ─────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ListReports(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var reports = await _reports.GetRecentAsync(limit, cancellationToken);
        return Ok(new { total = reports.Count, items = reports });
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/reports/{id}?agencyType=CBN
    // Retrieves a full previously generated report.
    // agencyType is required because it is the Cosmos partition key.
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> GetReport(
        string id,
        [FromQuery] string agencyType = "GENERAL",
        CancellationToken cancellationToken = default)
    {
        agencyType = agencyType.ToUpperInvariant();

        var report = await _reports.GetByIdAsync(id, agencyType, cancellationToken);
        if (report is null)
            return NotFound(new { error = $"Report '{id}' not found for agencyType '{agencyType}'." });

        return Ok(report);
    }

    // ─────────────────────────────────────────────────────────────────
    // REPORT ASSEMBLY
    // ─────────────────────────────────────────────────────────────────

    private static Report BuildReport(
        ReportRequest request,
        IReadOnlyList<ThreatIncident> incidents,
        string generatedBy)
    {
        var total      = incidents.Count;
        var blocked    = incidents.Count(i => i.Status == "Blocked");
        var monitoring = incidents.Count(i => i.Status == "Monitoring");
        var allowed    = incidents.Count(i => i.Status == "Allowed");

        var byClassification = incidents
            .GroupBy(i => i.Classification ?? "UNKNOWN")
            .OrderByDescending(g => g.Count())
            .Select(g => new ClassificationBreakdown
            {
                Classification = g.Key,
                Count          = g.Count(),
                PercentOfTotal = total > 0 ? Math.Round(g.Count() * 100.0 / total, 1) : 0,
                AvgRiskScore   = g.Count() > 0 ? (int)Math.Round(g.Average(i => i.RiskScore)) : 0
            })
            .ToList();

        var byChannel = incidents
            .GroupBy(i => i.Channel)
            .OrderByDescending(g => g.Count())
            .Select(g => new ChannelBreakdown
            {
                Channel = g.Key,
                Count   = g.Count(),
                Blocked = g.Count(i => i.Status == "Blocked")
            })
            .ToList();

        var byState = incidents
            .Where(i => i.State is not null)
            .GroupBy(i => i.State!)
            .OrderByDescending(g => g.Count())
            .Take(15)
            .Select(g => new StateBreakdown
            {
                State   = g.Key,
                Count   = g.Count(),
                Blocked = g.Count(i => i.Status == "Blocked")
            })
            .ToList();

        // Top incidents by risk score — already PII-free (ThreatIncident stores only redacted data)
        var topIncidents = incidents
            .OrderByDescending(i => i.RiskScore)
            .Take(request.MaxIncidentDetails)
            .Select(i => new IncidentSummary
            {
                Id             = i.Id,
                Timestamp      = i.Timestamp,
                Channel        = i.Channel,
                Classification = i.Classification,
                RiskScore      = i.RiskScore,
                Status         = i.Status,
                State          = i.State,
                Preview        = i.Preview
            })
            .ToList();

        return new Report
        {
            Id         = $"RPT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            AgencyType = request.AgencyType,
            GeneratedAt = DateTimeOffset.UtcNow.ToString("o"),
            GeneratedBy = generatedBy,
            PeriodFrom  = request.PeriodFrom.ToString("o"),
            PeriodTo    = request.PeriodTo.ToString("o"),

            Summary = new ReportSummary
            {
                TotalIncidents       = total,
                Blocked              = blocked,
                Monitoring           = monitoring,
                Allowed              = allowed,
                AverageRiskScore     = total > 0 ? (int)Math.Round(incidents.Average(i => i.RiskScore)) : 0,
                InterventionsTriggered = blocked + monitoring,
                UniqueSourceNumbers  = incidents.Select(i => i.From).Distinct().Count()
            },

            ByClassification = byClassification,
            ByChannel        = byChannel,
            ByState          = byState,
            TopIncidents     = topIncidents,

            ComplianceMetadata = new ComplianceMetadata
            {
                Framework       = AgencyFrameworks.GetValueOrDefault(request.AgencyType, AgencyFrameworks["GENERAL"]),
                DataProtection  = "Nigeria Data Protection Act (NDPA) 2023",
                ReportVersion   = "1.0",
                Classification  = "CONFIDENTIAL",
                ReportingEntity = "NaijaShield Platform"
            }
        };
    }
}
