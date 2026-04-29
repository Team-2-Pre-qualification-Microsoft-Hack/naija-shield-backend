namespace naija_shield_backend.DTOs;

/// <summary>
/// Matches the form-encoded payload Africa's Talking POSTs to the ingest webhook.
/// </summary>
public class SmsIngestRequest
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string LinkId { get; set; } = string.Empty;
}
