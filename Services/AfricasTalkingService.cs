namespace naija_shield_backend.Services;

public class AfricasTalkingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly string _username;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<AfricasTalkingService> _logger;

    private readonly string? _senderId;

    public AfricasTalkingService(HttpClient http, IConfiguration config, ILogger<AfricasTalkingService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _username = config["AT-Username"] ?? string.Empty;
        _apiKey = config["AT-API-Key"] ?? string.Empty;
        _senderId = config["AT-SenderId"]; // optional — e.g. "NaijaShield"

        // Sandbox when username is literally "sandbox", production otherwise
        _baseUrl = _username == "sandbox"
            ? "https://api.sandbox.africastalking.com/version1/messaging"
            : "https://api.africastalking.com/version1/messaging";
    }

    public async Task<bool> SendSmsAsync(string to, string message)
    {
        if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(_username))
        {
            _logger.LogWarning("[AT] API key or username not configured — skipping SMS send");
            return false;
        }

        var formData = new Dictionary<string, string>
        {
            ["username"] = _username,
            ["to"] = to,
            ["message"] = message,
        };

        // Registered sender ID (alphanumeric) — omit to let AT pick the shortcode
        if (!string.IsNullOrEmpty(_senderId))
            formData["from"] = _senderId;

        var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("apiKey", _apiKey);
        request.Headers.Add("Accept", "application/json");

        try
        {
            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("[AT] SendSms response {Status}: {Body}", (int)response.StatusCode, body);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AT] SendSms failed for {To}", to);
            return false;
        }
    }

    /// <summary>
    /// Makes a real outbound voice call to <paramref name="to"/> via Africa's Talking Voice API.
    /// When the call is answered, AT hits /api/at/voice-action?lang={language} which returns
    /// XML with the TTS warning. Returns the AT session ID on success, null on failure.
    /// Requires AT-VoiceNumber in configuration (the virtual number to call from).
    /// </summary>
    public async Task<string?> MakeOutboundCallAsync(
        string to,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var callFrom  = _config["AT-VoiceNumber"] ?? string.Empty;
        var serverUrl = _config["ServerUrl"] ?? _config["Jambonz:ServerUrl"] ?? string.Empty;

        if (string.IsNullOrEmpty(callFrom) || string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("[AT] Voice number or API key not configured — skipping outbound call to {To}", to);
            return null;
        }

        var callbackUrl = string.IsNullOrEmpty(serverUrl)
            ? null
            : $"{serverUrl}/api/at/voice-action?lang={language}";

        var formData = new Dictionary<string, string>
        {
            ["username"] = _username,
            ["callFrom"] = callFrom,
            ["callTo"]   = to,
        };
        if (callbackUrl is not null)
            formData["callbackUrl"] = callbackUrl;

        var voiceUrl = _username == "sandbox"
            ? "https://voice.sandbox.africastalking.com/call"
            : "https://voice.africastalking.com/call";

        var request = new HttpRequestMessage(HttpMethod.Post, voiceUrl)
        {
            Content = new FormUrlEncodedContent(formData)
        };
        request.Headers.Add("apiKey", _apiKey);
        request.Headers.Add("Accept", "application/json");

        try
        {
            var response = await _http.SendAsync(request, cancellationToken);
            var body     = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("[AT] MakeOutboundCall response {Status}: {Body}", (int)response.StatusCode, body);

            if (!response.IsSuccessStatusCode) return null;

            using var doc     = System.Text.Json.JsonDocument.Parse(body);
            var       entries = doc.RootElement.GetProperty("entries");
            return entries.GetArrayLength() > 0
                ? entries[0].GetProperty("sessionId").GetString()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AT] MakeOutboundCall failed for {To}", to);
            return null;
        }
    }
}
