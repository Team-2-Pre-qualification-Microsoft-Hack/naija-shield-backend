using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

/// <summary>
/// Sends outbound alerts via Africa's Talking SMS and logs every action.
/// Voice alerts fall back to SMS until the Spitch TTS sidecar injection is wired up.
/// </summary>
public sealed class LoggingAlertService : IAlertService
{
    private readonly AfricasTalkingService _at;
    private readonly ILogger<LoggingAlertService> _logger;

    private static readonly Dictionary<string, string> SmsWarnings = new()
    {
        ["en"]     = "NaijaShield Alert: A suspicious message was just sent to your number. Do NOT share your OTP, PIN, or bank details with anyone. If unsure, call your bank on their official number.",
        ["pidgin"] = "NaijaShield Alert: Dem just send suspicious message come your number. Abeg no give anybody your OTP, PIN, or bank details. If you no sure, call your bank for their correct number.",
        ["yo"]     = "NaijaShield Ìkìlọ̀: A ṣẹ̀ṣẹ̀ rán ifiranṣẹ afura sí nọ́mbà rẹ. Má fún ẹnikẹní ní OTP, PIN, tàbí àwọn aláyé ilé-ifowópamọ́ rẹ. Pè ilé-ifowópamọ́ rẹ bí o bá ṣiyèméjì.",
        ["ha"]     = "NaijaShield Gargadi: An aiko da sakon da ake zargi zuwa lambar ka yanzu. Kada ka ba kowa OTP, PIN, ko bayanan bankin ka. Kira bankin ka a lambar hukuma idan ba ka tabbata ba.",
        ["ig"]     = "NaijaShield Ọchọcha: Ezitela ozi ihe egwu na nọmba gị ugbu a. Emekwala ka onye ọ bụla nweta OTP, PIN, ma ọ bụ ozi banki gị. Kpọọ banki gị n'ọnụ ọnụ ha ma ọ bụrụ na ị na-adịghị ntụkwasị obi."
    };

    private static readonly Dictionary<string, string> VoiceWarnings = new()
    {
        ["en"]     = "NaijaShield Alert: A suspicious call was just made to your number. Do NOT share your OTP, PIN, or bank details. Hang up and call your bank directly on their official number.",
        ["pidgin"] = "NaijaShield Alert: Dem just call your number with suspicious intent. Abeg no give anybody your OTP, PIN, or bank details. Cut the call and call your bank for their correct number.",
        ["yo"]     = "NaijaShield Ìkìlọ̀: Ẹni kan ṣẹ̀ṣẹ̀ pe nọ́mbà rẹ pẹ̀lú ète buburu. Má fún ẹnikẹní ní OTP, PIN, tàbí àwọn aláyé ilé-ifowópamọ́ rẹ. Pa fóònù náà kí o sì pe ilé-ifowópamọ́ rẹ.",
        ["ha"]     = "NaijaShield Gargadi: Wani ya kira lambar ka tare da niyar zamba. Kada ka ba kowa OTP, PIN, ko bayanan banki. Datse kiran kuma ka kira bankin ka a lambar hukuma.",
        ["ig"]     = "NaijaShield Ọchọcha: Onye akpọọla nọmba gị n'ụzọ ihe egwu. Emekwala ka onye ọ bụla nweta OTP, PIN, ma ọ bụ ozi banki gị. Mechie ekwentị wee kpọọ banki gị n'ọnụ ọnụ ha."
    };

    public LoggingAlertService(AfricasTalkingService at, ILogger<LoggingAlertService> logger)
    {
        _at = at;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendSmsAlertAsync(
        string to,
        string originalMessage,
        string action,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[ALERT][SMS] Action={Action} Lang={Lang} Recipient={To} RedactedMessage={Message}",
            action, language, to, originalMessage);

        var warning = SmsWarnings.GetValueOrDefault(language, SmsWarnings["en"]);
        var sent = await _at.SendSmsAsync(to, warning);

        if (!sent)
            _logger.LogError("[ALERT][SMS] Africa's Talking delivery failed for {To}", to);
    }

    /// <inheritdoc />
    public async Task SendVoiceAlertAsync(
        string to,
        string action,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[ALERT][VOICE] Action={Action} Lang={Lang} Recipient={To}",
            action, language, to);

        // SMS fallback — Spitch TTS call injection via Python sidecar is a future step
        var warning = VoiceWarnings.GetValueOrDefault(language, VoiceWarnings["en"]);
        var sent = await _at.SendSmsAsync(to, warning);

        if (!sent)
            _logger.LogError("[ALERT][VOICE] Africa's Talking SMS fallback failed for {To}", to);
    }
}
