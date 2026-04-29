using System.Text;
using System.Text.Json;

namespace naija_shield_backend.Services;

/// <summary>
/// Wraps the Jambonz Live Call Control (LCC) REST API.
/// Called when the audio scanner detects a scam keyword — redirects the
/// live call to the /api/jambonz/warning webhook so Jambonz can say the
/// alert message and hang up.
/// </summary>
public sealed class JambonzService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<JambonzService> _logger;

    public JambonzService(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<JambonzService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    // ── Task 3: PUT /v1/Accounts/{accountId}/Calls/{callSid} ──────────────────
    // language: detected language code (en|pidgin|yo|ha|ig) forwarded to the
    // warning webhook so it can respond in the correct language.
    public async Task TriggerWarningAsync(
        string callSid,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        var accountId = _config["Jambonz:AccountId"];
        var serverUrl = _config["Jambonz:ServerUrl"];

        if (string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(serverUrl))
        {
            _logger.LogError("[Jambonz] LCC skipped — Jambonz:AccountId or Jambonz:ServerUrl not configured");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            call_hook = new { url = $"{serverUrl}/api/jambonz/warning?lang={language}" }
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "[Jambonz] Injecting warning via LCC for callSid={CallSid} lang={Lang}", callSid, language);

        try
        {
            var client = _httpFactory.CreateClient("Jambonz");
            var response = await client.PutAsync(
                $"/v1/Accounts/{accountId}/Calls/{callSid}", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[Jambonz] LCC success for callSid={CallSid}", callSid);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("[Jambonz] LCC failed status={Status} body={Body}",
                    (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Jambonz] LCC request threw for callSid={CallSid}", callSid);
        }
    }
}
