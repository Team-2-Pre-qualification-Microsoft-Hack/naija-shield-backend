namespace naija_shield_backend.Services.Interfaces;

/// <summary>
/// Redacts Personally Identifiable Information from free-text before
/// any content is sent to the LLM or persisted to storage.
/// The current registered implementation is a regex-based placeholder.
/// Swap in <c>PresidioPiiRedactionService</c> once the Microsoft Presidio
/// HTTP API is deployed and its URL is available in configuration.
/// Patterns covered: Nigerian phone numbers, NINs, BVNs, NUBAN bank accounts.
/// </summary>
public interface IPiiRedactionService
{
    /// <summary>
    /// Returns a copy of <paramref name="text"/> with all detected PII
    /// replaced by labelled tokens such as [PHONE_REDACTED].
    /// The original string is never modified.
    /// </summary>
    /// <param name="text">Raw input text that may contain PII.</param>
    /// <param name="cancellationToken">Propagates notification that operation should be cancelled.</param>
    /// <returns>Redacted text safe for LLM processing and storage.</returns>
    Task<string> RedactAsync(string text, CancellationToken cancellationToken = default);
}
