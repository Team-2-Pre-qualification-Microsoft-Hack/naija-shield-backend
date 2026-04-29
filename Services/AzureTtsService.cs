namespace naija_shield_backend.Services;

/// <summary>
/// Calls the Azure Cognitive Services Text-to-Speech REST API and returns MP3 audio bytes.
/// Uses the Nigerian-English neural voice (en-NG-EzinneNeural) which is included in the
/// free tier (500,000 chars/month). No SDK required — plain HTTP.
/// </summary>
public class AzureTtsService
{
    private readonly IHttpClientFactory _factory;
    private readonly string _key;
    private readonly string _region;
    private readonly ILogger<AzureTtsService> _logger;

    public AzureTtsService(IHttpClientFactory factory, IConfiguration config, ILogger<AzureTtsService> logger)
    {
        _factory = factory;
        _key     = config["Azure-Speech-Key"] ?? throw new InvalidOperationException("Azure-Speech-Key not configured");
        _region  = config["Azure-Speech-Region"] ?? "swedencentral";
        _logger  = logger;
    }

    public async Task<byte[]?> SynthesizeAsync(string text, CancellationToken ct = default)
    {
        // Escape XML special chars before embedding in SSML
        var escaped = System.Security.SecurityElement.Escape(text) ?? text;

        var ssml = $"""
            <speak version='1.0' xml:lang='en-NG'>
              <voice name='en-NG-EzinneNeural'>{escaped}</voice>
            </speak>
            """;

        var client  = _factory.CreateClient();
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{_region}.tts.speech.microsoft.com/cognitiveservices/v1")
        {
            Content = new StringContent(ssml, System.Text.Encoding.UTF8, "application/ssml+xml")
        };
        request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
        request.Headers.Add("X-Microsoft-OutputFormat", "audio-24khz-48kbitrate-mono-mp3");
        request.Headers.Add("User-Agent", "NaijaShield/1.0");

        try
        {
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[TTS] Synthesis failed {Status}: {Body}", (int)response.StatusCode, body);
                return null;
            }
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TTS] HTTP request threw");
            return null;
        }
    }
}
