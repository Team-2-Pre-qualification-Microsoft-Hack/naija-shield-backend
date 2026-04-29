using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using naija_shield_backend.Services;

namespace naija_shield_backend.Endpoints;

public static class JambonzEndpoints
{
    // Keywords that indicate the caller is phishing for sensitive data.
    private static readonly string[] ScamKeywords =
    [
        "otp", "bvn", "pin", "password", "atm", "cvv",
        "account number", "transfer code", "token", "verification code", "sort code"
    ];

    public static void MapJambonzEndpoints(this WebApplication app)
    {
        app.MapPost("/api/jambonz/incoming", IncomingCall).AllowAnonymous();
        app.MapPost("/api/jambonz/warning",  WarningWebhook).AllowAnonymous();
        app.MapPost("/api/jambonz/status",   CallStatusWebhook).AllowAnonymous();
        app.Map("/ws/audio", HandleAudioWebSocket);
    }

    // ── Task 1: Incoming call webhook ─────────────────────────────────────────
    // Jambonz hits this when a call arrives. We return two verbs:
    //   listen → streams mixed audio to our WebSocket for scam detection
    //   dial   → bridges the call to the victim's real number
    private static IResult IncomingCall(IConfiguration config)
    {
        var serverUrl    = config["Jambonz:ServerUrl"] ?? "https://localhost:5000";
        var victimNumber = config["Jambonz:VictimPhoneNumber"] ?? string.Empty;

        // Convert HTTPS base URL to WSS for the listen verb
        var wsUrl = serverUrl
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://",  "ws://",  StringComparison.OrdinalIgnoreCase)
            + "/ws/audio";

        return Results.Ok(new object[]
        {
            new { verb = "listen", url = wsUrl, mixType = "mixed" },
            new
            {
                verb   = "dial",
                target = new[] { new { type = "phone", number = victimNumber } }
            }
        });
    }

    // Voice warning texts keyed by language code — spoken via Jambonz TTS (say verb).
    // Spitch TTS (for richer African-language audio) is a planned upgrade.
    private static readonly Dictionary<string, string> VoiceWarningTexts = new()
    {
        ["en"]     = "NaijaShield warning. This caller is attempting to steal your personal information. Do not share your OTP, PIN, or bank details. This call is now being terminated.",
        ["pidgin"] = "NaijaShield dey talk. This person wey dey call you wan steal your information. Abeg no give dem your OTP, PIN, or bank details. We don cut this call.",
        ["yo"]     = "NaijaShield ikilo. Eni to n pe yin fe je yin je. Ma fun ni OTP, PIN, tabi awon alaye ile-ifowopamo re. A n pa ipe yii duro.",
        ["ha"]     = "NaijaShield gargadi. Wannan mai kira yana kokarin sata bayanankan ka. Kada ka ba shi OTP, PIN, ko bayanan banki. Muna yanke wannan kiran yanzu.",
        ["ig"]     = "NaijaShield ochọcha. Onye a na-akpo gi na-acho izu gi. Emekwala ka ha nweta OTP, PIN, ma obu ozi banki gi. Anyị na-akwusị oku a ugbu a."
    };

    // ── Task 4: Warning webhook ───────────────────────────────────────────────
    // Jambonz hits this after Task 3 redirects the live call here.
    // lang query param carries the detected language from the audio scanner.
    private static IResult WarningWebhook(string? lang = "en")
    {
        var language = lang ?? "en";
        var warningText = VoiceWarningTexts.GetValueOrDefault(language, VoiceWarningTexts["en"]);

        return Results.Ok(new object[]
        {
            new { verb = "say", text = warningText },
            new { verb = "hangup" }
        });
    }

    // ── Call status webhook ───────────────────────────────────────────────────
    // Jambonz posts call lifecycle events here (ringing, answered, completed,
    // failed, etc.). Must return 200 — Jambonz may retry on non-2xx.
    private static IResult CallStatusWebhook(
        HttpRequest request,
        ILogger<JambonzService> logger)
    {
        // Read call_sid and call_status from the form or JSON body Jambonz sends.
        // Jambonz sends application/x-www-form-urlencoded for status hooks.
        var callSid    = request.Form["call_sid"].FirstOrDefault()    ?? "(unknown)";
        var callStatus = request.Form["call_status"].FirstOrDefault() ?? "(unknown)";
        var from       = request.Form["from"].FirstOrDefault()        ?? string.Empty;
        var duration   = request.Form["duration"].FirstOrDefault()    ?? "0";

        logger.LogInformation(
            "[Jambonz Status] callSid={CallSid} status={Status} from={From} duration={Duration}s",
            callSid, callStatus, from, duration);

        return Results.Ok();
    }

    // ── Task 2: WebSocket audio stream ────────────────────────────────────────
    // Jambonz connects here after the listen verb fires.
    // First frame from Jambonz is a JSON metadata message containing call_sid.
    // Subsequent frames are raw binary PCM audio (8kHz 16-bit mono).
    private static async Task HandleAudioWebSocket(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var jambonz = context.RequestServices.GetRequiredService<JambonzService>();
        var logger   = context.RequestServices.GetRequiredService<ILogger<JambonzService>>();

        using var ws = await context.WebSockets.AcceptWebSocketAsync();

        // Jambonz may also pass callSid as a query param on the WebSocket URL
        var callSid = context.Request.Query["callSid"].FirstOrDefault();
        var audioBuffer  = new List<byte>(65_536);
        var receiveBuffer = new byte[8_192];
        bool warningFired = false;

        logger.LogInformation("[Jambonz WS] Connection open callSid={CallSid}", callSid ?? "(pending)");

        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(receiveBuffer, context.RequestAborted);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "done", context.RequestAborted);
                    break;
                }

                // ── Text frame: session metadata sent by Jambonz on connect ──
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                    callSid = ExtractCallSid(json) ?? callSid;
                    logger.LogInformation(
                        "[Jambonz WS] Session metadata received callSid={CallSid}", callSid);
                    continue;
                }

                // ── Binary frame: PCM audio ────────────────────────────────────
                if (result.MessageType != WebSocketMessageType.Binary || warningFired) continue;

                audioBuffer.AddRange(receiveBuffer[..result.Count]);

                // Transcribe every ~32 KB (~1 second at 8kHz 16-bit mono PCM)
                if (audioBuffer.Count < 32_000) continue;

                var transcript = await TranscribeChunkAsync(audioBuffer.ToArray(), context.RequestAborted);
                audioBuffer.Clear();

                logger.LogDebug("[Jambonz WS] Transcript chunk callSid={CallSid} text='{T}'",
                    callSid, transcript);

                // ── Task 2 → Task 3: scan for scam keywords ───────────────────
                if (!ContainsScamKeyword(transcript) || callSid is null) continue;

                var detectedLang = DetectLanguage(transcript);

                logger.LogWarning(
                    "[Jambonz WS] Scam keyword detected callSid={CallSid} lang={Lang} excerpt='{T}'",
                    callSid, detectedLang, transcript[..Math.Min(80, transcript.Length)]);

                warningFired = true;
                await jambonz.TriggerWarningAsync(callSid, detectedLang, context.RequestAborted);
            }
        }
        catch (OperationCanceledException) { /* client disconnected cleanly */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Jambonz WS] Unhandled error callSid={CallSid}", callSid);
        }

        logger.LogInformation("[Jambonz WS] Connection closed callSid={CallSid}", callSid ?? "(unknown)");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? ExtractCallSid(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Top-level call_sid (some Jambonz versions)
            if (root.TryGetProperty("call_sid", out var sid)) return sid.GetString();

            // Nested data.call_sid (session:new event format)
            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("call_sid", out var nested))
                return nested.GetString();
        }
        catch { /* malformed JSON — not a session frame */ }

        return null;
    }

    /// <summary>
    /// Sends a raw PCM chunk to the Python AI sidecar's /transcribe endpoint.
    /// Returns the transcript string, or empty on failure.
    /// Replace the stub body below with a real HTTP call once the sidecar
    /// exposes a streaming /transcribe route.
    /// </summary>
    private static Task<string> TranscribeChunkAsync(byte[] _, CancellationToken __)
    {
        // TODO: POST audio bytes to Python sidecar /transcribe (Whisper streaming).
        // For dev: return empty so no false-positive warnings fire.
        return Task.FromResult(string.Empty);
    }

    private static bool ContainsScamKeyword(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return false;
        var lower = transcript.ToLowerInvariant();
        return ScamKeywords.Any(lower.Contains);
    }

    // Distinctive marker words for each Nigerian language.
    // Order matters: pidgin is checked first because its markers are unambiguous.
    private static readonly (string Lang, string[] Markers)[] LanguageMarkers =
    [
        ("pidgin", ["abeg", "oga ", "wahala", "wetin", "how far", "na wa", "dem go", "no be", "e don"]),
        ("yo",     ["bawo ni", "e joor", "àwọn", "mo fẹ", "ẹ dupe", "ẹ jọ", "jẹ kin", "ẹyin"]),
        ("ha",     ["ina kwana", "sannu", "wallahi", "gaskiya", "kai tsaye", "ina son", "yaya kake"]),
        ("ig",     ["biko ", "kedu", "nna m", "nne m", "chukwu", "anyị", "ọ bụ"]),
    ];

    /// <summary>
    /// Detects the dominant language of a transcript using distinctive marker words.
    /// Returns one of: en | pidgin | yo | ha | ig.
    /// Falls back to "en" when no markers are found (runs correctly even on empty input).
    /// </summary>
    private static string DetectLanguage(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return "en";
        var lower = transcript.ToLowerInvariant();

        foreach (var (lang, markers) in LanguageMarkers)
            if (markers.Any(lower.Contains))
                return lang;

        return "en";
    }
}
