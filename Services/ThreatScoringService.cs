using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using naija_shield_backend.Models;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

/// <summary>
/// Sends redacted text to Azure OpenAI via Semantic Kernel using a
/// few-shot prompt pre-loaded with Nigerian scam patterns, then
/// deserialises the strict JSON response into a <see cref="LlmThreatAnalysis"/>.
/// </summary>
public sealed class ThreatScoringService : IThreatScoringService
{
    private readonly Kernel _kernel;
    private readonly ILogger<ThreatScoringService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Strips ```json ... ``` markdown wrapping that some models add
    private static readonly Regex JsonBlockRegex = new(
        @"```(?:json)?\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private const string SystemPrompt = """
        You are NaijaShield, a fraud detection AI for Nigerian telecom networks.
        Analyse the provided message and return ONLY a valid JSON object — no markdown,
        no code fences, no extra text before or after the JSON.

        The JSON must have exactly these six fields:
        {
          "riskScore": <integer 0-100, higher = more dangerous>,
          "classification": "<OTP_PHISH|VISHING|IMPERSONATION|SOCIAL_ENGINEERING|SAFE>",
          "confidence": <float 0.0-1.0>,
          "explanation": "<one sentence justification>",
          "recommendedAction": "<BLOCK|MONITOR|ALLOW>",
          "detectedLanguage": "<en|pidgin|yo|ha|ig — dominant language of the message>"
        }

        Language codes: en = English, pidgin = Nigerian Pidgin, yo = Yoruba, ha = Hausa, ig = Igbo.
        Detect the language from vocabulary, grammar, and script — not from the topic.

        Classification guide:
        - OTP_PHISH: Message requests an OTP/PIN from the recipient under a false pretence.
        - VISHING: Voice or text scripted to impersonate a bank, EFCC, NIN office, etc.
        - IMPERSONATION: Sender pretends to be a known institution (GTBank, MTN, INEC, EFCC).
        - SOCIAL_ENGINEERING: Family/friend impersonation, fake emergency, urgent money requests.
        - SAFE: Legitimate OTP delivery, bank notifications with no requests, verified senders.

        Few-shot examples:

        Message (SMS): "Your GTBank account has been suspended. Send your OTP to 09012345678 to reactivate now."
        Response: {"riskScore":97,"classification":"OTP_PHISH","confidence":0.98,"explanation":"Requests OTP with urgency under threat of account suspension impersonating GTBank","recommendedAction":"BLOCK","detectedLanguage":"en"}

        Message (SMS): "Hi Mum, I changed my number. This is John. I'm in trouble — please send N50,000 urgent."
        Response: {"riskScore":88,"classification":"SOCIAL_ENGINEERING","confidence":0.93,"explanation":"Classic family-impersonation scam requesting urgent money transfer from a new number","recommendedAction":"BLOCK","detectedLanguage":"en"}

        Message (SMS): "Your GTBank account is restricted. Verify your identity at http://gtb-secure.ng immediately."
        Response: {"riskScore":95,"classification":"IMPERSONATION","confidence":0.96,"explanation":"Bank impersonation with suspicious phishing URL requesting identity verification","recommendedAction":"BLOCK","detectedLanguage":"en"}

        Message (SMS): "Your OTP to access your account is 123456. Valid for 5 minutes. Do not share this code with anyone."
        Response: {"riskScore":8,"classification":"SAFE","confidence":0.97,"explanation":"Standard OTP delivery from a verified source with an explicit do-not-share warning","recommendedAction":"ALLOW","detectedLanguage":"en"}

        Message (Voice transcript): "Oga send me the OTP now now, my boss don say make I collect am from you."
        Response: {"riskScore":91,"classification":"OTP_PHISH","confidence":0.89,"explanation":"Urgent Pidgin-English request for OTP using social pressure and authority reference","recommendedAction":"BLOCK","detectedLanguage":"pidgin"}

        Message (SMS): "EFCC: Your BVN has been flagged for fraud. Call 08099887766 within 1 hour or be arrested."
        Response: {"riskScore":99,"classification":"VISHING","confidence":0.99,"explanation":"EFCC impersonation with arrest threat designed to extract personal information by phone","recommendedAction":"BLOCK","detectedLanguage":"en"}

        Message (SMS): "Transfer of N5,000 to Chijioke Obi was successful. Balance: N42,300. Not you? Call 07001234567."
        Response: {"riskScore":12,"classification":"SAFE","confidence":0.94,"explanation":"Standard bank debit notification with a legitimate customer-care callback number","recommendedAction":"ALLOW","detectedLanguage":"en"}

        Message (SMS): "Ẹ jọ, fi OTP rẹ ranṣẹ sí mi láti mú akọọlẹ rẹ ṣiṣẹ. GTBank ni mo ń sọ."
        Response: {"riskScore":96,"classification":"OTP_PHISH","confidence":0.95,"explanation":"Yoruba-language OTP phishing message impersonating GTBank requesting the recipient send their OTP","recommendedAction":"BLOCK","detectedLanguage":"yo"}

        Message (SMS): "Biko ziga m OTP gị ka m nwee ike ịmeghe akaụntụ gị. Nke a bụ UBA."
        Response: {"riskScore":95,"classification":"OTP_PHISH","confidence":0.94,"explanation":"Igbo-language OTP phishing impersonating UBA bank requesting account OTP","recommendedAction":"BLOCK","detectedLanguage":"ig"}

        Message (SMS): "Aiki na banki ya tsaya. Ka aiko mana OTP ɗinka domin maido da aiki. Zenith Bank."
        Response: {"riskScore":94,"classification":"OTP_PHISH","confidence":0.93,"explanation":"Hausa-language bank impersonation requesting OTP to restore account access","recommendedAction":"BLOCK","detectedLanguage":"ha"}

        Now analyse this message:
        """;

    /// <summary>Initialises the service with the Semantic Kernel and a structured logger.</summary>
    public ThreatScoringService(Kernel kernel, ILogger<ThreatScoringService> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LlmThreatAnalysis> AnalyzeAsync(
        string redactedText,
        string channel = "SMS",
        CancellationToken cancellationToken = default)
    {
        var fullPrompt = $"{SystemPrompt}\nMessage ({channel}): \"{redactedText}\"\nResponse:";

        try
        {
            _logger.LogInformation(
                "[ThreatScoring] Sending {Channel} message to LLM for analysis (length={Len})",
                channel, redactedText.Length);

            var result = await _kernel.InvokePromptAsync(fullPrompt, cancellationToken: cancellationToken);
            var rawResponse = result.ToString().Trim();

            _logger.LogDebug("[ThreatScoring] Raw LLM response: {Response}", rawResponse);

            var json = ExtractJson(rawResponse);
            var analysis = JsonSerializer.Deserialize<LlmThreatAnalysis>(json, JsonOpts);

            if (analysis is null)
            {
                _logger.LogError("[ThreatScoring] LLM returned null after deserialisation. Raw: {Raw}", rawResponse);
                return SafeDefault();
            }

            _logger.LogInformation(
                "[ThreatScoring] Result: Score={Score} Class={Class} Action={Action} Confidence={Conf}",
                analysis.RiskScore, analysis.Classification, analysis.RecommendedAction, analysis.Confidence);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ThreatScoring] LLM call failed for {Channel} message", channel);
            return SafeDefault();
        }
    }

    // Pulls the JSON object out of a raw LLM response, stripping markdown code fences if present.
    private static string ExtractJson(string response)
    {
        var match = JsonBlockRegex.Match(response);
        if (match.Success)
            return match.Groups[1].Value;

        // If there are no code fences, find the first { and last } as a fallback
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start >= 0 && end > start)
            return response[start..(end + 1)];

        return response;
    }

    private static LlmThreatAnalysis SafeDefault() => new()
    {
        RiskScore = 0,
        Classification = "SAFE",
        Confidence = 0,
        Explanation = "LLM analysis unavailable — defaulting to safe for manual review.",
        RecommendedAction = "ALLOW"
    };
}
