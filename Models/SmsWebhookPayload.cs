namespace naija_shield_backend.Models;

/// <summary>
/// Represents the form-encoded webhook payload sent by Africa's Talking
/// when an inbound SMS is received on a registered shortcode or virtual number.
/// Africa's Talking POST body fields are lowercase; ASP.NET model binding
/// matches them case-insensitively to these Pascal-case properties.
/// </summary>
public class SmsWebhookPayload
{
    /// <summary>Sender's MSISDN in international format, e.g. +2348055030371.</summary>
    public string From { get; set; } = default!;

    /// <summary>Destination shortcode or virtual number.</summary>
    public string To { get; set; } = default!;

    /// <summary>Raw body of the SMS message.</summary>
    public string Text { get; set; } = default!;

    /// <summary>Timestamp of the message as provided by Africa's Talking.</summary>
    public string Date { get; set; } = default!;

    /// <summary>Africa's Talking unique message identifier.</summary>
    public string Id { get; set; } = default!;

    /// <summary>Optional link ID for premium SMS flows.</summary>
    public string? LinkId { get; set; }
}
