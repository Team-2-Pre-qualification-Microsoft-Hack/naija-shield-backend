using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Microsoft.SemanticKernel;
using naija_shield_backend.DTOs;
using naija_shield_backend.Models;

namespace naija_shield_backend.Services;

public class SmsService
{
    private readonly Container _smsEventsContainer;
    private readonly AfricasTalkingService _at;
    private readonly Kernel? _kernel;
    private readonly ILogger<SmsService> _logger;

    // Nigerian PII patterns
    private static readonly Regex BvnNin = new(@"\b\d{11}\b", RegexOptions.Compiled);
    private static readonly Regex AccountNumber = new(@"\b\d{10}\b", RegexOptions.Compiled);
    private static readonly Regex OtpCode = new(@"\b\d{4,8}\b", RegexOptions.Compiled);

    private const string WarningMessage =
        "NaijaShield Alert: The message you just received may be a scam attempt. " +
        "Do NOT share your OTP, PIN, password, or bank details with anyone. " +
        "If you are unsure, call your bank directly on their official number.";

    public SmsService(
        CosmosClient cosmosClient,
        AfricasTalkingService at,
        IServiceProvider services,
        ILogger<SmsService> logger)
    {
        _smsEventsContainer = cosmosClient.GetDatabase("NaijaShieldDB").GetContainer("SmsEvents");
        _at = at;
        _kernel = services.GetService<Kernel>();
        _logger = logger;
    }

    public async Task<IResult> ProcessIncomingSmsAsync(SmsIngestRequest request)
    {
        _logger.LogInformation("[SMS] Received from {From}: \"{Text}\"", request.From, request.Text);

        var redacted = RedactPii(request.Text);

        var (decision, confidence, reason) = await ClassifyAsync(redacted, request.From);

        bool warningSent = false;
        if (decision == "BLOCK")
        {
            _logger.LogWarning("[SMS] BLOCK decision ({Confidence:P0}) for message from {From}. Reason: {Reason}",
                confidence, request.From, reason);

            // Warn the recipient (the subscriber who received the scam SMS)
            warningSent = await _at.SendSmsAsync(request.To, WarningMessage);
        }

        var smsEvent = new SmsEvent
        {
            AtMessageId = request.Id,
            From = request.From,
            To = request.To,
            RawText = request.Text,
            RedactedText = redacted,
            Decision = decision,
            Confidence = confidence,
            Reason = reason,
            WarningSent = warningSent,
            ReceivedAt = DateTime.UtcNow
        };

        await _smsEventsContainer.CreateItemAsync(smsEvent, new PartitionKey(smsEvent.Id));

        // Africa's Talking expects a 200 OK with an empty body to acknowledge receipt
        return Results.Ok();
    }

    // ─────────────────────────────────────────────
    // PII REDACTION
    // ─────────────────────────────────────────────
    private static string RedactPii(string text)
    {
        text = BvnNin.Replace(text, "[ID-REDACTED]");
        text = AccountNumber.Replace(text, "[ACCT-REDACTED]");
        text = OtpCode.Replace(text, "[CODE-REDACTED]");
        return text;
    }

    // ─────────────────────────────────────────────
    // LLM CLASSIFICATION
    // ─────────────────────────────────────────────
    private async Task<(string decision, float confidence, string reason)> ClassifyAsync(
        string text, string from)
    {
        if (_kernel == null)
        {
            _logger.LogWarning("[SMS] Kernel not available — defaulting to ALLOW");
            return ("ALLOW", 0f, "AI unavailable");
        }

        var prompt = $"""
            You are a fraud detection AI embedded in a Nigerian telecom security system.
            Analyze the SMS below and decide if it is a scam or fraud attempt.

            Sender number: {from}
            Message: "{text}"

            Nigerian fraud patterns to detect:
            - OTP, PIN, or password requests ("send your OTP", "enter your PIN to verify")
            - Bank/institution impersonation ("I am calling from GTBank", "CBN directive")
            - Urgency manipulation ("Your account will be blocked", "Act now or lose access")
            - Social engineering ("Hi Mum, I need urgent money", "I am in trouble")
            - Prize/lottery scams ("You have won", "Claim your reward")
            - Credential harvesting links (suspicious URLs)
            - USSD transfer tricks ("Dial *737# to reverse a wrong transfer")

            Respond with ONLY a valid JSON object — no markdown, no explanation:
            {{"decision":"BLOCK","confidence":0.95,"reason":"brief explanation"}}
            or
            {{"decision":"ALLOW","confidence":0.98,"reason":"legitimate message"}}
            """;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var json = result.ToString().Trim();

            // Strip markdown code fences if the model adds them
            if (json.StartsWith("```")) json = Regex.Replace(json, @"```[a-z]*\n?", "").Trim().TrimEnd('`');

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var decision = root.GetProperty("decision").GetString() ?? "ALLOW";
            var confidence = root.GetProperty("confidence").GetSingle();
            var reason = root.GetProperty("reason").GetString() ?? string.Empty;

            return (decision.ToUpperInvariant(), confidence, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMS] Classification failed — defaulting to ALLOW");
            return ("ALLOW", 0f, "Classification error");
        }
    }
}
