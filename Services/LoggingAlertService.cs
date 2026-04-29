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

    private const string SmsWarning =
        "NaijaShield Alert: A suspicious message was just sent to your number. " +
        "Do NOT share your OTP, PIN, or bank details with anyone. " +
        "If unsure, call your bank on their official number.";

    private const string VoiceWarning =
        "NaijaShield Alert: A suspicious call was just made to your number. " +
        "Do NOT share your OTP, PIN, or bank details. " +
        "Hang up and call your bank directly on their official number.";

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
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[ALERT][SMS] Action={Action} Recipient={To} RedactedMessage={Message}",
            action, to, originalMessage);

        var sent = await _at.SendSmsAsync(to, SmsWarning);

        if (!sent)
            _logger.LogError("[ALERT][SMS] Africa's Talking delivery failed for {To}", to);
    }

    /// <inheritdoc />
    public async Task SendVoiceAlertAsync(
        string to,
        string action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[ALERT][VOICE] Action={Action} Recipient={To}",
            action, to);

        // SMS fallback — Spitch TTS call injection via Python sidecar is a future step
        var sent = await _at.SendSmsAsync(to, VoiceWarning);

        if (!sent)
            _logger.LogError("[ALERT][VOICE] Africa's Talking SMS fallback failed for {To}", to);
    }
}
