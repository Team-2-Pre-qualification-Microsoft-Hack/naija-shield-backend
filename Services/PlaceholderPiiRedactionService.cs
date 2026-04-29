using System.Text.RegularExpressions;
using naija_shield_backend.Services.Interfaces;

namespace naija_shield_backend.Services;

/// <summary>
/// Regex-based PII redaction covering the most common Nigerian identifiers.
/// This is a placeholder until Microsoft Presidio is deployed. Replace by
/// registering <c>PresidioPiiRedactionService</c> (which calls the Presidio
/// HTTP API) without changing any call sites — all code depends on
/// <see cref="IPiiRedactionService"/>.
///
/// Patterns covered (in order of application):
///   1. Nigerian mobile numbers — +234 or 0 prefix, 10 remaining digits (MTN/Airtel/Glo/9mobile)
///   2. NUBAN bank account numbers — exactly 10 consecutive digits
///   3. NIN / BVN — exactly 11 consecutive digits
///
/// Note: NIN and BVN share the 11-digit format; both are masked with the same
/// token. Presidio uses named-entity models to distinguish them contextually.
/// </summary>
public sealed class PlaceholderPiiRedactionService : IPiiRedactionService
{
    // +2348055030371 or 08055030371 — Nigerian mobile numbers
    private static readonly Regex PhoneRegex = new(
        @"\b(\+234|0)[789]\d{9}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // NUBAN: exactly 10 digits not surrounded by other digits
    private static readonly Regex NubanRegex = new(
        @"(?<!\d)\d{10}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // NIN / BVN: exactly 11 digits not surrounded by other digits
    private static readonly Regex NinBvnRegex = new(
        @"(?<!\d)\d{11}(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public Task<string> RedactAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            return Task.FromResult(text);

        var redacted = PhoneRegex.Replace(text, "[PHONE_REDACTED]");
        redacted = NubanRegex.Replace(redacted, "[ACCOUNT_REDACTED]");
        redacted = NinBvnRegex.Replace(redacted, "[NIN_BVN_REDACTED]");

        return Task.FromResult(redacted);
    }
}
