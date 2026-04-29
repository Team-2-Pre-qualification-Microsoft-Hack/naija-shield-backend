using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using naija_shield_backend.Models;
using naija_shield_backend.Services;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Serves incident data to the Enterprise Dashboard.
/// — GET  /api/incidents           — paginated threat feed table
/// — GET  /api/incidents/heatmap   — lat/lng points for Azure Maps heatmap
/// — GET  /api/incidents/stats     — KPI summary cards
/// — POST /api/incidents/seed      — populate demo data (authenticated)
/// </summary>
[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IIncidentRepository _repository;
    private readonly PhoneLocationService _location;
    private readonly ILogger<IncidentsController> _logger;

    public IncidentsController(
        IIncidentRepository repository,
        PhoneLocationService location,
        ILogger<IncidentsController> logger)
    {
        _repository = repository;
        _location = location;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/incidents?limit=50
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
    // ─────────────────────────────────────────────────────────────────
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);
        var incidents = await _repository.GetRecentAsync(limit, cancellationToken);

        var points = incidents
            .Where(i => i.Lat.HasValue && i.Lng.HasValue)
            .Select(i => new
            {
                i.Id,
                lat           = i.Lat!.Value,
                lng           = i.Lng!.Value,
                i.State,
                i.Lga,
                weight        = i.RiskScore,
                i.Status,
                i.Channel,
                i.Classification
            });

        return Ok(points);
    }

    // ─────────────────────────────────────────────────────────────────
    // GET /api/incidents/stats
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

    // ─────────────────────────────────────────────────────────────────
    // POST /api/incidents/seed
    // Populates the Incidents container with realistic demo data.
    // Requires authentication — call with a valid JWT bearer token.
    // Query param: ?count=40 (1–100)
    // ─────────────────────────────────────────────────────────────────
    [HttpPost("seed")]
    [Authorize]
    public async Task<IActionResult> SeedDemoData(
        [FromQuery] int count = 40,
        CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 100);

        var incidents = BuildSeedIncidents(count);
        var savedIds  = new List<string>(incidents.Count);

        foreach (var incident in incidents)
        {
            await _repository.SaveAsync(incident, cancellationToken);
            savedIds.Add(incident.Id);
        }

        _logger.LogInformation("[Seed] Inserted {Count} demo incidents", savedIds.Count);
        return Ok(new { seeded = savedIds.Count, ids = savedIds });
    }

    // ─────────────────────────────────────────────────────────────────
    // SEED HELPERS
    // ─────────────────────────────────────────────────────────────────

    private List<ThreatIncident> BuildSeedIncidents(int count)
    {
        // Spread of Nigerian phone numbers — hashed by PhoneLocationService
        // to give geographic variety across all 36 states + FCT.
        var phones = new[]
        {
            "+2348031234567", "+2348061234568", "+2348121234569",
            "+2348031234570", "+2347031234571", "+2348021234572",
            "+2348081234573", "+2348111234574", "+2348051234575",
            "+2348071234576", "+2347051234577", "+2348091234578",
            "+2348171234579", "+2348031234580", "+2348061234581",
            "+2348121234582", "+2348031234583", "+2347061234584",
            "+2348031234585", "+2348061234586", "+2348121234587",
            "+2348021234588", "+2348081234589", "+2347031234590",
            "+2348031234591", "+2348031234592", "+2348061234593",
            "+2348091234594", "+2348171234595", "+2348051234596",
        };

        var smsTemplates = new[]
        {
            ("OTP_PHISH",
             "Your GTBank OTP is 483921. Share this code to complete your transfer. Valid 5 mins.",
             "Message requests OTP sharing — a classic phishing pattern used by account-takeover fraudsters.",
             92),
            ("OTP_PHISH",
             "UBA ALERT: Verify your account now. Enter OTP 776234 sent to your number to avoid suspension.",
             "Fake bank alert combining urgency with OTP harvesting. UBA does not send OTPs via SMS for verification.",
             95),
            ("OTP_PHISH",
             "Access Bank: Your token for the transfer of N250,000 is 334872. DO NOT share.",
             "OTP embedded in transfer confirmation — social engineering to make victim believe it is legitimate.",
             88),
            ("IMPERSONATION",
             "CBN DIRECTIVE: Your BVN 22198765432 has been flagged. Call 08099123456 to resolve immediately.",
             "Impersonates the Central Bank of Nigeria. Contains BVN reference to appear authentic. Callback number is fraudulent.",
             91),
            ("IMPERSONATION",
             "NIBSS: Your NIN is due for revalidation. Click http://nibss-ng.com to update within 24 hours.",
             "Impersonates NIBSS with a lookalike domain. The link leads to a credential harvesting page.",
             89),
            ("SOCIAL_ENGINEERING",
             "Hello Mum, I changed my number. Please send N20,000 urgent, I'm in trouble. Will explain later.",
             "Family impersonation scam. Sender pretends to be a family member in distress to trigger emotional response.",
             86),
            ("SOCIAL_ENGINEERING",
             "Congratulations! You won N5,000,000 in the MTN Anniversary promo. Call 07041234567 to claim now.",
             "Prize scam impersonating a major telecom. Designed to extract personal and financial information.",
             94),
            ("SOCIAL_ENGINEERING",
             "TRANSFER REVERSAL: Dial *737*4*20000*1234# to reverse the wrong transfer sent to your account.",
             "USSD transfer trick. The USSD code actually initiates a payment FROM the victim, not a reversal.",
             97),
            ("VISHING",
             "This is a recorded message from your bank fraud department. Press 1 to speak with an agent now.",
             "Automated call script designed to initiate voice phishing. Urgency framing to bypass critical thinking.",
             85),
            ("SAFE",
             "Your Kuda Bank statement for April 2025 is ready. Log in to view it at app.kuda.com.",
             "Legitimate bank statement notification. No sensitive data requested, link points to official domain.",
             12),
            ("SAFE",
             "Reminder: Your Flutterwave payment of N15,000 to Shoprite was successful. Ref: FLW-2025.",
             "Legitimate payment confirmation from a known fintech. No action requested from recipient.",
             8),
            ("OTP_PHISH",
             "Zenith Bank: Urgent! Your account will be suspended. Send your ATM PIN to verify: 08071234567.",
             "Requests ATM PIN over SMS — no legitimate bank ever does this. High-confidence phishing.",
             98),
            ("IMPERSONATION",
             "EFCC ALERT: Your account has been linked to a fraud investigation. Call our desk: 07031234567.",
             "EFCC impersonation scam. Creates fear of legal action to pressure victim into calling fraudsters.",
             90),
            ("SOCIAL_ENGINEERING",
             "Brother, it's me Emeka. New number. Send 50k now, will return double tomorrow. Urgent!",
             "Friend/family impersonation with investment angle. Classic advance-fee social engineering.",
             87),
            ("SAFE",
             "Your DSTV subscription of N24,500 has been renewed successfully. Enjoy uninterrupted viewing.",
             "Routine subscription renewal notification. No links, no requests. Legitimate transaction confirmation.",
             6),
        };

        var voiceTemplates = new[]
        {
            ("VISHING",
             "Hello, I am calling from Stanbic IBTC fraud prevention. We detected suspicious login on your account...",
             "Bank impersonation vishing call. Script designed to extract account credentials under guise of security.",
             88, 0.72),
            ("VISHING",
             "This is CBN enforcement. Your BVN has been suspended due to illegal activity. Press 1 to speak with officer...",
             "CBN impersonation with legal threat. High deepfake probability indicates synthetic voice generation.",
             93, 0.81),
            ("SOCIAL_ENGINEERING",
             "Oga, I need your help urgently. I am at the police station and they need bail money of N100,000...",
             "Distress call impersonating a known contact. Emotional manipulation to bypass rational evaluation.",
             82, 0.31),
            ("SAFE",
             "This is an automated reminder from your insurance provider about your policy renewal due next week...",
             "Legitimate insurance reminder call. No financial requests, standard notification pattern.",
             15, 0.04),
            ("VISHING",
             "Congratulations! You have been selected for a N2 million CBN palliative grant. Provide your account details...",
             "Grant scam using CBN branding. Requests account details — primary objective is account takeover.",
             96, 0.68),
        };

        var rng      = new Random(42); // fixed seed for reproducibility
        var now      = DateTimeOffset.UtcNow;
        var result   = new List<ThreatIncident>(count);

        for (var i = 0; i < count; i++)
        {
            var phone   = phones[i % phones.Length];
            var (lat, lng, state, lga) = _location.Lookup(phone);

            // Spread timestamps over the last 14 days
            var hoursBack = rng.Next(0, 14 * 24);
            var ts        = now.AddHours(-hoursBack);

            // 75% SMS, 25% Voice
            bool isVoice = (i % 4 == 3) && voiceTemplates.Length > 0;

            ThreatIncident incident;

            if (isVoice)
            {
                var tmpl = voiceTemplates[i % voiceTemplates.Length];
                var risk = Math.Clamp(tmpl.Item4 + rng.Next(-5, 6), 0, 100);
                incident = new ThreatIncident
                {
                    Id             = $"INC-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    Timestamp      = ts.ToString("o"),
                    Channel        = "Voice",
                    From           = phone,
                    Preview        = tmpl.Item1.Length > 80 ? tmpl.Item1[..80] : tmpl.Item1,
                    RiskScore      = risk,
                    Classification = tmpl.Item1 == "SAFE" ? "SAFE" : tmpl.Item1,
                    Explanation    = tmpl.Item2,
                    Status         = DetermineStatus(risk),
                    RawPayload     = tmpl.Item1,
                    DeepfakeScore  = tmpl.Item5 + (rng.NextDouble() * 0.1 - 0.05),
                    Lat            = lat,
                    Lng            = lng,
                    State          = state,
                    Lga            = lga
                };
            }
            else
            {
                var tmpl = smsTemplates[i % smsTemplates.Length];
                var risk = Math.Clamp(tmpl.Item4 + rng.Next(-5, 6), 0, 100);
                incident = new ThreatIncident
                {
                    Id             = $"INC-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                    Timestamp      = ts.ToString("o"),
                    Channel        = "SMS",
                    From           = phone,
                    Preview        = tmpl.Item2.Length > 80 ? tmpl.Item2[..80] : tmpl.Item2,
                    RiskScore      = risk,
                    Classification = tmpl.Item1,
                    Explanation    = tmpl.Item3,
                    Status         = DetermineStatus(risk),
                    RawPayload     = tmpl.Item2,
                    Lat            = lat,
                    Lng            = lng,
                    State          = state,
                    Lga            = lga
                };
            }

            result.Add(incident);
        }

        return result;
    }

    private static string DetermineStatus(int riskScore) => riskScore switch
    {
        >= 85 => "Blocked",
        >= 50 => "Monitoring",
        _     => "Allowed"
    };
}
