namespace naija_shield_backend.Services.Interfaces;

/// <summary>
/// Sends outbound alerts to the original communication parties
/// when a threat is detected and the action is BLOCKED or MONITORING.
/// The current registered implementation logs the alert details only.
/// Replace with Africa's Talking outbound SMS for <see cref="SendSmsAlertAsync"/>
/// and Spitch TTS via the Python sidecar for <see cref="SendVoiceAlertAsync"/>.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Notifies the recipient of a flagged SMS threat.
    /// </summary>
    /// <param name="to">MSISDN of the original SMS recipient to be warned.</param>
    /// <param name="originalMessage">Redacted body of the suspicious message.</param>
    /// <param name="action">The action taken: BLOCK or MONITOR.</param>
    /// <param name="cancellationToken">Propagates notification that operation should be cancelled.</param>
    Task SendSmsAlertAsync(
        string to,
        string originalMessage,
        string action,
        string language = "en",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies the recipient of a flagged voice call threat.
    /// Future implementation should inject a localised Spitch TTS warning
    /// into the call via the Python sidecar.
    /// </summary>
    /// <param name="to">MSISDN of the original call recipient to be warned.</param>
    /// <param name="action">The action taken: BLOCK or MONITOR.</param>
    /// <param name="language">Detected language code (en|pidgin|yo|ha|ig). Used to localise the warning.</param>
    /// <param name="cancellationToken">Propagates notification that operation should be cancelled.</param>
    Task SendVoiceAlertAsync(
        string to,
        string action,
        string language = "en",
        CancellationToken cancellationToken = default);
}
