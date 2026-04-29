using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

/// <summary>
/// Placeholder alert service that writes alert details to structured logs.
/// Replace or supplement this implementation with:
/// — Africa's Talking outbound SMS for <see cref="SendSmsAlertAsync"/>
/// — Spitch TTS injected via the Python sidecar for <see cref="SendVoiceAlertAsync"/>
/// All call sites depend on <see cref="IAlertService"/> so no controller code
/// changes are required when real delivery is wired up.
/// </summary>
public sealed class LoggingAlertService : IAlertService
{
    private readonly ILogger<LoggingAlertService> _logger;

    /// <summary>Initialises the service with a structured logger.</summary>
    public LoggingAlertService(ILogger<LoggingAlertService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendSmsAlertAsync(
        string to,
        string originalMessage,
        string action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[ALERT][SMS] Action={Action} Recipient={To} RedactedMessage={Message}",
            action, to, originalMessage);

        // TODO: Implement Africa's Talking outbound SMS delivery
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendVoiceAlertAsync(
        string to,
        string action,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[ALERT][VOICE] Action={Action} Recipient={To}",
            action, to);

        // TODO: Inject Spitch TTS warning via Python sidecar POST /inject-warning
        return Task.CompletedTask;
    }
}
