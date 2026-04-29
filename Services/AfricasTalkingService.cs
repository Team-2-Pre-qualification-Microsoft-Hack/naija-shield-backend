namespace naija_shield_backend.Services;

public class AfricasTalkingService
{
    private readonly HttpClient _http;
    private readonly string _username;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<AfricasTalkingService> _logger;

    public AfricasTalkingService(HttpClient http, IConfiguration config, ILogger<AfricasTalkingService> logger)
    {
        _http = http;
        _logger = logger;
        _username = config["AT-Username"] ?? string.Empty;
        _apiKey = config["AT-API-Key"] ?? string.Empty;

        // Use sandbox in dev, production in prod
        var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Development";
        _baseUrl = env == "Production"
            ? "https://api.africastalking.com/version1/messaging"
            : "https://api.sandbox.africastalking.com/version1/messaging";
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
}
