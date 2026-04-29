namespace naija_shield_backend.Models;

/// <summary>
/// Strongly-typed representation of the JSON object the LLM is instructed
/// to return for every message analysed by the threat-scoring prompt.
/// Property names are camelCase to match the LLM output directly;
/// <see cref="System.Text.Json.JsonSerializerOptions.PropertyNameCaseInsensitive"/>
/// is used when deserialising so casing mismatches are tolerated.
/// </summary>
public class LlmThreatAnalysis
{
    /// <summary>Risk score 0–100; higher means more dangerous.</summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// One of: OTP_PHISH | VISHING | IMPERSONATION | SOCIAL_ENGINEERING | SAFE
    /// </summary>
    public string Classification { get; set; } = "SAFE";

    /// <summary>Model's self-reported confidence in the classification, 0.0–1.0.</summary>
    public double Confidence { get; set; }

    /// <summary>Human-readable justification for the risk score and classification.</summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>One of: BLOCK | MONITOR | ALLOW — the LLM's recommended action.</summary>
    public string RecommendedAction { get; set; } = "ALLOW";
}
