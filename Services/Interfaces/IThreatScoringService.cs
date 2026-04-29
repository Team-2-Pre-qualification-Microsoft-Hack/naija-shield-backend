using naija_shield_backend.Models;

namespace naija_shield_backend.Services.Interfaces;

/// <summary>
/// Sends redacted message text to Azure OpenAI via Semantic Kernel
/// and returns a structured threat analysis result.
/// Uses a few-shot prompt pre-loaded with Nigerian scam patterns
/// (OTP phishing, bank impersonation, social engineering, Pidgin-language vishing).
/// </summary>
public interface IThreatScoringService
{
    /// <summary>
    /// Analyses <paramref name="redactedText"/> for fraud signals and returns
    /// a structured <see cref="LlmThreatAnalysis"/> with risk score, classification,
    /// confidence, explanation and recommended action.
    /// </summary>
    /// <param name="redactedText">PII-scrubbed message body or call transcript.</param>
    /// <param name="channel">Source channel hint passed to the prompt: "SMS" or "Voice".</param>
    /// <param name="cancellationToken">Propagates notification that operation should be cancelled.</param>
    /// <returns>
    /// A populated <see cref="LlmThreatAnalysis"/>.
    /// Returns a safe default (riskScore=0, classification=SAFE) if the LLM call fails,
    /// so the pipeline can continue rather than surface an unhandled exception.
    /// </returns>
    Task<LlmThreatAnalysis> AnalyzeAsync(
        string redactedText,
        string channel = "SMS",
        CancellationToken cancellationToken = default);
}
