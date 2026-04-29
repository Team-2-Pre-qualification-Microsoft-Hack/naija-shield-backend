using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using naija_shield_backend.Hubs;
using naija_shield_backend.Models;
using naija_shield_backend.Services;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Live demo / POC controller — proves the NaijaShield pipeline end-to-end
/// without requiring a connected Jambonz switch.
///
/// GET  /api/demo/scam-scripts          — list built-in Nigerian scam transcripts
/// POST /api/demo/simulate-scam-call    — run the full real pipeline on any transcript,
///                                        see every step with ms timing, optionally
///                                        make a real AT voice call to the victim.
/// </summary>
[ApiController]
[Route("api/demo")]
public sealed class DemoController : ControllerBase
{
    // ── Built-in Nigerian scam scripts ────────────────────────────────────────
    // Cover all 5 languages and the most common scam typologies.
    private static readonly DemoScript[] Scripts =
    [
        new("en-otp-gtbank",
            "en", "SMS", "+2348031234567",
            "Your GTBank account has been flagged for suspicious activity. " +
            "Send me the OTP that was just sent to your number to restore access now. " +
            "Failure to act in 10 minutes will result in permanent account suspension.",
            "English OTP phish impersonating GTBank — urgency + account-suspension threat"),

        new("pidgin-otp-social",
            "pidgin", "SMS", "+2347051234568",
            "Oga abeg help me na. Dem send OTP come your number by mistake. " +
            "Send am give me make I fix your account. No worry na bank work I dey do.",
            "Nigerian Pidgin social-engineering OTP request"),

        new("yoruba-otp-gtbank",
            "yo", "SMS", "+2348121234569",
            "Ẹ jọ, fi OTP rẹ ranṣẹ sí mi láti mú akọọlẹ rẹ ṣiṣẹ. " +
            "GTBank ni mo ń sọ. Àkókò rẹ fẹ́ parí ní ìṣẹ́jú mẹ́wàá.",
            "Yoruba OTP phish — GTBank impersonation with time pressure"),

        new("hausa-otp-zenith",
            "ha", "SMS", "+2348091234570",
            "Aiki na banki ya tsaya saboda dalilai na tsaro. " +
            "Ka aiko mana OTP ɗinka domin maido da aiki. Wannan saƙo ne daga Zenith Bank.",
            "Hausa OTP phish impersonating Zenith Bank"),

        new("igbo-otp-uba",
            "ig", "SMS", "+2348171234571",
            "Biko ziga m OTP gị ka m nwee ike ịmeghe akaụntụ gị. " +
            "A na-eche gị. Nke a bụ ozi sitere na UBA.",
            "Igbo OTP phish impersonating UBA"),

        new("en-vishing-cbn",
            "en", "Voice", "+2347031234572",
            "Hello, I am calling from the CBN fraud prevention unit. " +
            "Your BVN has been linked to a suspicious transaction of N2.5 million. " +
            "Please provide your account PIN and OTP to clear the flag before arrest warrants are issued.",
            "English vishing — CBN impersonation with legal threat and credential harvest"),

        new("en-family-impersonation",
            "en", "SMS", "+2348051234573",
            "Hi Mum, I changed my number. I am at the police station and they need N80,000 bail. " +
            "Please send to this account: 0123456789 Access Bank. I will explain everything later.",
            "Family impersonation social engineering — distress + urgent transfer request"),

        new("pidgin-ussd-trick",
            "pidgin", "SMS", "+2348081234574",
            "Oga, dem transfer N150,000 enter your account by mistake. " +
            "Abeg dial *737*4*150000*9981# to reverse am make we no get problem with EFCC.",
            "Pidgin USSD reversal trick — the code actually initiates a transfer FROM the victim"),
    ];

    private static readonly Dictionary<string, string> SmsWarnings = new()
    {
        ["en"]     = "NaijaShield Alert: The message you just received is likely a scam. Do NOT share your OTP, PIN, or bank details with anyone. Call your bank on their official number.",
        ["pidgin"] = "NaijaShield Alert: The message wey dem send you fit be scam. Abeg no give anybody your OTP, PIN, or bank details. Call your bank for their correct number.",
        ["yo"]     = "NaijaShield Ìkìlọ̀: Ifiranṣẹ tí o gba le jẹ ìdán. Má fún ẹnikẹní ní OTP, PIN, tàbí àwọn aláyé ilé-ifowópamọ́ rẹ. Pè ilé-ifowópamọ́ rẹ.",
        ["ha"]     = "NaijaShield Gargadi: Sakon da ka karba na iya zama zamba. Kada ka ba kowa OTP, PIN, ko bayanan bankin ka. Kira bankin ka.",
        ["ig"]     = "NaijaShield Ọchọcha: Ozi i nwetara nwere ike ịbụ nzuzo. Emekwala ka onye ọ bụla nweta OTP, PIN, ma ọ bụ ozi banki gị. Kpọọ banki gị.",
    };

    private static readonly Dictionary<string, string> VoiceWarnings = new()
    {
        ["en"]     = "NaijaShield warning. This caller is attempting to steal your personal information. Do not share your OTP, PIN, or bank details. This call is now being terminated.",
        ["pidgin"] = "NaijaShield dey talk. This person wey dey call you wan steal your information. Abeg no give dem your OTP, PIN, or bank details. We don cut this call.",
        ["yo"]     = "NaijaShield ikilo. Eni to n pe yin fe je yin je. Ma fun ni OTP, PIN, tabi awon alaye ile-ifowopamo re. A n pa ipe yii duro.",
        ["ha"]     = "NaijaShield gargadi. Wannan mai kira yana kokarin sata bayanankan ka. Kada ka ba shi OTP, PIN, ko bayanan banki. Muna yanke wannan kiran yanzu.",
        ["ig"]     = "NaijaShield ochọcha. Onye a na-akpo gi na-acho izu gi. Emekwala ka ha nweta OTP, PIN, ma obu ozi banki gi. Anyị na-akwusị oku a ugbu a.",
    };

    private readonly IPiiRedactionService _pii;
    private readonly IThreatScoringService _scoring;
    private readonly IIncidentRepository _repository;
    private readonly IHubContext<ThreatHub> _hub;
    private readonly AfricasTalkingService _at;
    private readonly PhoneLocationService _location;
    private readonly ILogger<DemoController> _logger;

    public DemoController(
        IPiiRedactionService pii,
        IThreatScoringService scoring,
        IIncidentRepository repository,
        IHubContext<ThreatHub> hub,
        AfricasTalkingService at,
        PhoneLocationService location,
        ILogger<DemoController> logger)
    {
        _pii        = pii;
        _scoring    = scoring;
        _repository = repository;
        _hub        = hub;
        _at         = at;
        _location   = location;
        _logger     = logger;
    }

    // ── GET /api/demo/scam-scripts ────────────────────────────────────────────
    [HttpGet("scam-scripts")]
    public IActionResult GetScamScripts() =>
        Ok(Scripts.Select(s => new
        {
            s.Id,
            s.Language,
            s.Channel,
            s.Description,
            s.Transcript
        }));

    // ── POST /api/demo/simulate-scam-call ─────────────────────────────────────
    /// <summary>
    /// Runs the full NaijaShield detection pipeline on a scam transcript and returns
    /// a timestamped audit trail of every step. Real Cosmos DB save + SignalR broadcast
    /// happen so the live dashboard updates in real-time during the demo.
    ///
    /// Body options (pick one):
    ///   { "scriptId": "yoruba-otp-gtbank" }                          — use a built-in script
    ///   { "transcript": "Send me your OTP...", "channel": "SMS" }    — provide your own
    ///
    /// Optional:
    ///   "from":         simulated scammer number (overrides script default)
    ///   "victimNumber": real number to send the SMS warning / voice call to
    ///   "makeRealCall": true — actually call victimNumber via Africa's Talking Voice API
    /// </summary>
    [HttpPost("simulate-scam-call")]
    public async Task<IActionResult> SimulateScamCall(
        [FromBody] SimulateCallRequest req,
        CancellationToken cancellationToken)
    {
        var steps = new List<object>();
        var sw    = System.Diagnostics.Stopwatch.StartNew();

        // ── Resolve transcript + metadata ─────────────────────────────────────
        string transcript, from, channel;

        if (!string.IsNullOrWhiteSpace(req.ScriptId))
        {
            var script = Scripts.FirstOrDefault(s => s.Id == req.ScriptId);
            if (script is null)
                return BadRequest(new
                {
                    error   = $"Unknown scriptId '{req.ScriptId}'.",
                    hint    = "Call GET /api/demo/scam-scripts for the full list."
                });

            transcript = script.Transcript;
            from       = req.From ?? script.From;
            channel    = script.Channel;
        }
        else if (!string.IsNullOrWhiteSpace(req.Transcript))
        {
            transcript = req.Transcript;
            from       = req.From    ?? "+2348031234567";
            channel    = req.Channel ?? "SMS";
        }
        else
        {
            // Default to the English GTBank OTP script so demo works with an empty body
            var s  = Scripts[0];
            transcript = s.Transcript;
            from       = req.From ?? s.From;
            channel    = s.Channel;
        }

        var victimNumber = req.VictimNumber ?? string.Empty;

        steps.Add(Step("input", new { transcript, from, channel, victimNumber }, sw));

        // ── Step 1: PII Redaction ─────────────────────────────────────────────
        var redacted = await _pii.RedactAsync(transcript, cancellationToken);
        steps.Add(Step("piiRedaction", new { redacted }, sw));

        // ── Step 2: LLM Threat Scoring ────────────────────────────────────────
        var analysis = await _scoring.AnalyzeAsync(redacted, channel, cancellationToken);
        steps.Add(Step("llmScoring", new
        {
            analysis.RiskScore,
            analysis.Classification,
            analysis.Confidence,
            analysis.DetectedLanguage,
            analysis.Explanation,
            analysis.RecommendedAction
        }, sw));

        // ── Step 3: Status determination ──────────────────────────────────────
        var status = analysis.RiskScore switch
        {
            >= 85 => "Blocked",
            >= 50 => "Monitoring",
            _     => "Allowed"
        };
        steps.Add(Step("statusDetermination", new { status, riskScore = analysis.RiskScore }, sw));

        // ── Step 4: Geolocation ───────────────────────────────────────────────
        var (lat, lng, state, lga) = _location.Lookup(from);
        steps.Add(Step("geolocation", new { lat, lng, state, lga }, sw));

        // ── Step 5: Cosmos DB persist ─────────────────────────────────────────
        var incident = new ThreatIncident
        {
            Id             = $"INC-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            Timestamp      = DateTimeOffset.UtcNow.ToString("o"),
            Channel        = channel,
            From           = from,
            Preview        = redacted.Length > 80 ? redacted[..80] : redacted,
            RiskScore      = analysis.RiskScore,
            Classification = analysis.Classification,
            Explanation    = analysis.Explanation,
            Status         = status,
            RawPayload     = redacted,
            Lat            = lat,
            Lng            = lng,
            State          = state,
            Lga            = lga
        };

        await _repository.SaveAsync(incident, cancellationToken);
        steps.Add(Step("cosmosDb", new { saved = incident.Id }, sw));

        // ── Step 6: SignalR broadcast → dashboard updates live ────────────────
        var evt = new ThreatFeedEvent
        {
            Id        = incident.Id,
            Time      = incident.Timestamp,
            Channel   = incident.Channel,
            Preview   = incident.Preview,
            RiskScore = incident.RiskScore,
            Status    = incident.Status
        };
        await _hub.Clients.All.SendAsync("NewThreatDetected", evt, cancellationToken);
        steps.Add(Step("signalRBroadcast", new { message = "Dashboard updated in real-time" }, sw));

        // ── Step 7: Intervention ──────────────────────────────────────────────
        string? callSessionId = null;
        bool    smsSent       = false;
        string? warningText   = null;
        string? voiceScript   = null;

        if (status is "Blocked" or "Monitoring")
        {
            var lang = analysis.DetectedLanguage;
            warningText = SmsWarnings.GetValueOrDefault(lang, SmsWarnings["en"]);
            voiceScript = VoiceWarnings.GetValueOrDefault(lang, VoiceWarnings["en"]);

            // Send real SMS warning if victim number provided
            if (!string.IsNullOrWhiteSpace(victimNumber))
                smsSent = await _at.SendSmsAsync(victimNumber, warningText);

            // Optionally make a real outbound warning call via AT Voice API
            if (req.MakeRealCall && !string.IsNullOrWhiteSpace(victimNumber))
                callSessionId = await _at.MakeOutboundCallAsync(victimNumber, lang, cancellationToken);

            steps.Add(Step("intervention", new
            {
                action             = status,
                language           = lang,
                smsWarning         = warningText,
                voiceWarning       = voiceScript,
                smsSent,
                voiceCallInitiated = callSessionId is not null,
                callSessionId,
                note               = callSessionId is null && req.MakeRealCall
                    ? "Voice call skipped — AT-VoiceNumber not configured or victimNumber not provided"
                    : null
            }, sw));
        }
        else
        {
            steps.Add(Step("intervention", new
            {
                action  = "NONE",
                message = "Message scored below threshold — no intervention required"
            }, sw));
        }

        sw.Stop();
        _logger.LogInformation(
            "[Demo] Pipeline complete incidentId={Id} score={Score} lang={Lang} totalMs={Ms}",
            incident.Id, incident.RiskScore, analysis.DetectedLanguage, sw.ElapsedMilliseconds);

        return Ok(new
        {
            demo           = true,
            totalElapsedMs = sw.ElapsedMilliseconds,
            incidentId     = incident.Id,
            verdict = new
            {
                riskScore        = incident.RiskScore,
                classification   = incident.Classification,
                status           = incident.Status,
                detectedLanguage = analysis.DetectedLanguage,
                confidence       = analysis.Confidence
            },
            pipeline        = steps,
            warningTexts = status is "Blocked" or "Monitoring" ? new
            {
                sms   = warningText,
                voice = voiceScript,
                note  = "Voice text would be spoken via Spitch TTS injected into the live call stream"
            } : null,
            incident
        });
    }

    // ── POST /api/demo/seed-reputation ───────────────────────────────────────
    /// <summary>
    /// Seeds the Cosmos DB with 12 pre-built incidents across multiple channels and
    /// languages all originating from +2348099000000, the canonical "repeat offender"
    /// demo number. Call this once, then hit GET /api/numbers/+2348099000000/reputation
    /// to see a fully populated HIGH_RISK profile.
    /// </summary>
    [HttpPost("seed-reputation")]
    public async Task<IActionResult> SeedReputation(CancellationToken cancellationToken)
    {
        const string scammerNumber = "+2348099000000";

        // Hardcoded incidents — no LLM calls, instant seeding
        var seeds = new[]
        {
            ("SMS",   "OTP_PHISH",          91, "Blocked",    "Your GTBank account is suspended. Send OTP 483921 to verify.", "en",     -1),
            ("SMS",   "OTP_PHISH",          95, "Blocked",    "UBA ALERT: Enter OTP 776234 to avoid account suspension.",     "en",     -3),
            ("Voice", "VISHING",            88, "Blocked",    "CBN fraud unit. Provide your PIN to clear suspicious flag.",    "en",     -5),
            ("SMS",   "IMPERSONATION",      89, "Blocked",    "NIBSS: Your NIN is due for revalidation. Click to update.",    "en",     -7),
            ("SMS",   "OTP_PHISH",          96, "Blocked",    "Ẹ jọ, fi OTP rẹ ranṣẹ sí mi. GTBank ni mo ń sọ.",              "yo",     -9),
            ("SMS",   "SOCIAL_ENGINEERING", 86, "Blocked",    "Oga abeg send me your OTP make I fix your account.",           "pidgin", -12),
            ("Voice", "VISHING",            93, "Blocked",    "Zenith Bank security. Verify PIN to unblock your account.",    "en",     -15),
            ("SMS",   "OTP_PHISH",          94, "Blocked",    "Biko ziga m OTP gị. Nke a bụ UBA.",                            "ig",     -18),
            ("SMS",   "IMPERSONATION",      90, "Blocked",    "EFCC: Your account linked to fraud. Call 08099123456.",        "en",     -22),
            ("SMS",   "OTP_PHISH",          94, "Blocked",    "Aiki na banki ya tsaya. Ka aiko OTP ɗinka. Zenith Bank.",      "ha",     -28),
            ("SMS",   "SOCIAL_ENGINEERING", 87, "Blocked",    "Hi Mum new number. Send N80k bail money urgent.",              "en",     -35),
            ("Voice", "VISHING",            82, "Monitoring", "Your insurance policy needs urgent renewal payment.",          "en",     -40),
        };

        var now    = DateTimeOffset.UtcNow;
        var (lat, lng, state, lga) = _location.Lookup(scammerNumber);
        var saved  = new List<string>();

        foreach (var (channel, classification, risk, status, preview, lang, daysBack) in seeds)
        {
            var incident = new ThreatIncident
            {
                Id             = $"INC-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                Timestamp      = now.AddDays(daysBack).ToString("o"),
                Channel        = channel,
                From           = scammerNumber,
                Preview        = preview,
                RiskScore      = risk,
                Classification = classification,
                Explanation    = $"Demo seed: {classification} detected from repeat-offender number.",
                Status         = status,
                RawPayload     = preview,
                Lat            = lat,
                Lng            = lng,
                State          = state,
                Lga            = lga
            };

            await _repository.SaveAsync(incident, cancellationToken);
            saved.Add(incident.Id);
        }

        _logger.LogInformation("[Demo] Seeded {Count} reputation incidents for {Number}", saved.Count, scammerNumber);

        return Ok(new
        {
            seeded        = saved.Count,
            scammerNumber,
            ids           = saved,
            nextStep      = $"GET /api/numbers/{Uri.EscapeDataString(scammerNumber)}/reputation"
        });
    }

    private static object Step(string name, object result, System.Diagnostics.Stopwatch sw) =>
        new { step = name, result, elapsedMs = sw.ElapsedMilliseconds };
}

public sealed record DemoScript(
    string Id,
    string Language,
    string Channel,
    string From,
    string Transcript,
    string Description);

public sealed record SimulateCallRequest
{
    public string? ScriptId     { get; init; }
    public string? Transcript   { get; init; }
    public string? From         { get; init; }
    public string? VictimNumber { get; init; }
    public string? Channel      { get; init; }
    public bool    MakeRealCall { get; init; } = false;
}
