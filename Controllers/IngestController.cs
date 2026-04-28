using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using naija_shield_backend.Hubs;
using naija_shield_backend.Models;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Controllers;

/// <summary>
/// Entry point for all raw telecom data into the NaijaShield platform.
/// Exposes two ingestion endpoints:
/// — POST /api/ingest/sms  — Africa's Talking form-encoded webhook
/// — POST /api/ingest/voice — JSON body with base64-encoded audio
/// Both pipelines: redact PII → score with LLM → persist to Cosmos DB
/// → broadcast via SignalR → optionally alert the recipient.
/// </summary>
[ApiController]
[Route("api/ingest")]
public sealed class IngestController : ControllerBase
{
    private readonly IPiiRedactionService _pii;
    private readonly IThreatScoringService _scoring;
    private readonly IIncidentRepository _repository;
    private readonly IAlertService _alerts;
    private readonly IHubContext<ThreatHub> _hub;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<IngestController> _logger;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Injects all required services via the DI container.</summary>
    public IngestController(
        IPiiRedactionService pii,
        IThreatScoringService scoring,
        IIncidentRepository repository,
        IAlertService alerts,
        IHubContext<ThreatHub> hub,
        IHttpClientFactory httpFactory,
        ILogger<IngestController> logger)
    {
        _pii = pii;
        _scoring = scoring;
        _repository = repository;
        _alerts = alerts;
        _hub = hub;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────
    // ENDPOINT 1 — SMS INGESTION
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Receives an inbound SMS event from Africa's Talking via their
    /// real-time webhook (POST, application/x-www-form-urlencoded).
    /// Always returns HTTP 200 so Africa's Talking does not retry delivery.
    /// Pipeline: parse → redact PII → LLM threat score → Cosmos DB → SignalR → alert.
    /// </summary>
    /// <remarks>
    /// AfroXLMR fast-filter step is intentionally skipped in this revision.
    /// Integrate it here between PII redaction and the LLM call once the
    /// AfroXLMR inference endpoint is available in configuration.
    /// </remarks>
    [HttpPost("sms")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> IngestSms(
        [FromForm] SmsWebhookPayload payload,
        CancellationToken cancellationToken)
    {
        // Validate required fields manually so we can still return 200
        if (string.IsNullOrWhiteSpace(payload.From) ||
            string.IsNullOrWhiteSpace(payload.Text))
        {
            _logger.LogWarning("[SMS] Rejected webhook — missing 'from' or 'text' field");
            return Ok(new { received = true, processed = false, reason = "Missing required fields: from, text" });
        }

        _logger.LogInformation(
            "[SMS] Received from={From} atId={Id} length={Len}",
            payload.From, payload.Id, payload.Text.Length);

        try
        {
            // ── Step 1: PII Redaction ──────────────────────────────────────
            var redactedText = await _pii.RedactAsync(payload.Text, cancellationToken);
            _logger.LogInformation("[SMS] PII redaction complete");

            // ── Step 2: AfroXLMR fast-filter (SKIPPED — not yet configured) ──
            // TODO: POST redactedText to AfroXLMR inference endpoint.
            // If AfroXLMR scores < threshold, skip LLM call and mark ALLOWED directly.

            // ── Step 3: LLM threat scoring ─────────────────────────────────
            var analysis = await _scoring.AnalyzeAsync(redactedText, "SMS", cancellationToken);

            // ── Step 4: Determine status from risk score (score is authoritative) ──
            var status = DetermineStatus(analysis.RiskScore);

            // ── Step 5: Build and persist the Cosmos DB document ──────────
            var incident = new ThreatIncident
            {
                Id = GenerateIncidentId(),
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Channel = "SMS",
                From = payload.From,
                Preview = redactedText.Length > 80
                    ? redactedText[..80]
                    : redactedText,
                RiskScore = analysis.RiskScore,
                Classification = analysis.Classification,
                Explanation = analysis.Explanation,
                Status = status,
                RawPayload = redactedText   // redacted — never the original PII-bearing text
            };

            await _repository.SaveAsync(incident, cancellationToken);
            _logger.LogInformation("[SMS] Incident saved Id={Id}", incident.Id);

            // ── Step 6: Broadcast to dashboard via SignalR ─────────────────
            await BroadcastToFeed(incident, cancellationToken);

            // ── Step 7: Alert the recipient if action is BLOCK or MONITOR ──
            if (status is "Blocked" or "Monitoring")
            {
                await _alerts.SendSmsAlertAsync(
                    payload.To,
                    redactedText,
                    status.ToUpperInvariant(),
                    cancellationToken);
            }

            return Ok(new
            {
                received = true,
                processed = true,
                incidentId = incident.Id,
                riskScore = incident.RiskScore,
                status = incident.Status
            });
        }
        catch (Exception ex)
        {
            // Return 200 regardless so AT does not retry; the error is logged for investigation.
            _logger.LogError(ex, "[SMS] Pipeline failed for message from={From}", payload.From);
            return Ok(new { received = true, processed = false, reason = "Internal pipeline error", detail = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // ENDPOINT 2 — VOICE INGESTION
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Receives a recorded voice segment as multipart form-data,
    /// transcribes it via the Python AI sidecar, runs the same LLM fraud-
    /// scoring pipeline as SMS, and combines the LLM score with the deepfake
    /// score using the formula: finalScore = (llmScore × 0.6) + (deepfake × 100 × 0.4).
    /// Always returns HTTP 200 so callers do not retry on transient failures.
    /// </summary>
    [HttpPost("voice")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> IngestVoice(
        [FromForm] VoiceIngestRequest payload,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Ok(new { received = true, processed = false, reason = "Invalid request payload" });
        }

        if (string.IsNullOrWhiteSpace(payload.CallId) ||
            string.IsNullOrWhiteSpace(payload.From) ||
            string.IsNullOrWhiteSpace(payload.To) ||
            string.IsNullOrWhiteSpace(payload.Timestamp) ||
            payload.AudioFile is null ||
            payload.AudioFile.Length == 0)
        {
            _logger.LogWarning("[Voice] Rejected payload — missing required fields or empty audio file");
            return Ok(new { received = true, processed = false, reason = "Missing required fields or empty audio file" });
        }

        _logger.LogInformation(
            "[Voice] Received callId={CallId} from={From} format={Fmt}",
            payload.CallId, payload.From, payload.AudioFormat);

        // ── Step 1: Call Python AI sidecar for transcription + deepfake ──
        AiSidecarResponse sidecarResult;
        try
        {
            sidecarResult = await CallAiSidecarAsync(payload, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[Voice] AI sidecar unreachable for callId={CallId}", payload.CallId);
            return Ok(new
            {
                received = true,
                processed = false,
                reason = "AI sidecar unavailable",
                detail = "The voice transcription service is currently unreachable. Try again later.",
                callId = payload.CallId
            });
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "[Voice] AI sidecar timed out for callId={CallId}", payload.CallId);
            return Ok(new
            {
                received = true,
                processed = false,
                reason = "AI sidecar timeout",
                detail = "Voice transcription timed out. The audio may be too long.",
                callId = payload.CallId
            });
        }

        _logger.LogInformation(
            "[Voice] Sidecar: transcript length={Len} deepfakeScore={Score} lang={Lang}",
            sidecarResult.Transcript.Length, sidecarResult.DeepfakeScore, sidecarResult.LanguageDetected);

        try
        {
            // ── Step 2: PII redaction on the transcript ────────────────────
            var redactedTranscript = await _pii.RedactAsync(sidecarResult.Transcript, cancellationToken);

            // ── Step 3: LLM threat scoring ─────────────────────────────────
            var analysis = await _scoring.AnalyzeAsync(redactedTranscript, "Voice", cancellationToken);

            // ── Step 4: Combine LLM risk score + deepfake score ───────────
            var finalRiskScore = (int)Math.Round(
                (analysis.RiskScore * 0.6) + (sidecarResult.DeepfakeScore * 100 * 0.4));
            finalRiskScore = Math.Clamp(finalRiskScore, 0, 100);

            _logger.LogInformation(
                "[Voice] Final risk score: llm={Llm} deepfake={Df} combined={Final}",
                analysis.RiskScore, sidecarResult.DeepfakeScore, finalRiskScore);

            // ── Step 5: Determine status ───────────────────────────────────
            var status = DetermineStatus(finalRiskScore);

            // ── Step 6: Build and persist Cosmos DB document ───────────────
            var incident = new ThreatIncident
            {
                Id = GenerateIncidentId(),
                Timestamp = DateTimeOffset.UtcNow.ToString("o"),
                Channel = "Voice",
                From = payload.From,
                Preview = redactedTranscript.Length > 80
                    ? redactedTranscript[..80]
                    : redactedTranscript,
                RiskScore = finalRiskScore,
                Classification = analysis.Classification,
                Explanation = analysis.Explanation,
                Status = status,
                RawPayload = redactedTranscript,
                DeepfakeScore = sidecarResult.DeepfakeScore
            };

            await _repository.SaveAsync(incident, cancellationToken);
            _logger.LogInformation("[Voice] Incident saved Id={Id}", incident.Id);

            // ── Step 7: Broadcast to dashboard via SignalR ─────────────────
            await BroadcastToFeed(incident, cancellationToken);

            // ── Step 8: Alert the recipient if BLOCK or MONITOR ───────────
            if (status is "Blocked" or "Monitoring")
            {
                await _alerts.SendVoiceAlertAsync(
                    payload.To,
                    status.ToUpperInvariant(),
                    cancellationToken);
            }

            return Ok(new
            {
                received = true,
                processed = true,
                incidentId = incident.Id,
                riskScore = incident.RiskScore,
                status = incident.Status
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Voice] Pipeline failed for callId={CallId}", payload.CallId);
            return Ok(new { received = true, processed = false, reason = "Internal pipeline error", detail = ex.Message });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Maps a numeric risk score to the dashboard status label.</summary>
    private static string DetermineStatus(int riskScore) => riskScore switch
    {
        >= 85 => "Blocked",
        >= 50 => "Monitoring",
        _ => "Allowed"
    };

    /// <summary>
    /// Generates a short, human-readable incident ID in the format INC-XXXXXXXX.
    /// Uses the first 8 hex characters of a new GUID to ensure uniqueness.
    /// </summary>
    private static string GenerateIncidentId()
        => $"INC-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

    /// <summary>
    /// Sends the incident to all connected dashboard clients via the
    /// "NewThreatDetected" SignalR event.
    /// </summary>
    private async Task BroadcastToFeed(ThreatIncident incident, CancellationToken cancellationToken)
    {
        var evt = new ThreatFeedEvent
        {
            Id = incident.Id,
            Time = incident.Timestamp,
            Channel = incident.Channel,
            Preview = incident.Preview,
            RiskScore = incident.RiskScore,
            Status = incident.Status
        };

        await _hub.Clients.All.SendAsync("NewThreatDetected", evt, cancellationToken);
        _logger.LogInformation("[SignalR] Broadcast NewThreatDetected Id={Id}", incident.Id);
    }

    /// <summary>
    /// POSTs the voice payload to the Python AI sidecar's /analyze-voice endpoint
    /// and deserialises the response into an <see cref="AiSidecarResponse"/>.
    /// Throws <see cref="HttpRequestException"/> if the sidecar is unreachable.
    /// </summary>
    private async Task<AiSidecarResponse> CallAiSidecarAsync(
        VoiceIngestRequest payload,
        CancellationToken cancellationToken)
    {
        var client = _httpFactory.CreateClient("AiSidecar");

        using var content = new MultipartFormDataContent();

        var fileStream = payload.AudioFile.OpenReadStream();
        var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "audioFile", payload.AudioFile.FileName);
        content.Add(new StringContent(payload.AudioFormat), "audioFormat");

        _logger.LogInformation("[Voice] Calling AI sidecar POST /ingest/voice");
        var response = await client.PostAsync("/ingest/voice", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<AiSidecarResponse>(responseBody, CamelCaseOptions);

        return result ?? throw new InvalidOperationException("AI sidecar returned null response");
    }
}
