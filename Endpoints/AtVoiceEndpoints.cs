namespace naija_shield_backend.Endpoints;

/// <summary>
/// Africa's Talking Voice API callback endpoint.
/// When NaijaShield makes an outbound warning call via AfricasTalkingService.MakeOutboundCallAsync,
/// AT connects the call then immediately hits this endpoint to ask "what should I say?"
/// We respond with XML containing the TTS warning in the detected language.
///
/// POST /api/at/voice-action?lang={en|pidgin|yo|ha|ig}
/// </summary>
public static class AtVoiceEndpoints
{
    private static readonly Dictionary<string, string> WarningTexts = new()
    {
        ["en"]     = "NaijaShield Alert. Someone is attempting to scam you. " +
                     "Do not share your O T P, PIN, or any bank details with anyone who calls or texts you. " +
                     "If you received a suspicious message, hang up and call your bank directly on their official number.",

        ["pidgin"] = "NaijaShield dey warn you. Somebody wan scam you. " +
                     "Abeg no give anybody your O T P, your PIN, or your bank details. " +
                     "If you receive any suspicious message, cut the call and phone your bank directly.",

        ["yo"]     = "NaijaShield Ìkìlọ̀. Ẹni kan ń gbìyànjú láti jẹ ọ jẹ. " +
                     "Má fún ẹnikẹní ní O T P, PIN, tàbí àwọn aláyé ilé-ifowópamọ́ rẹ. " +
                     "Bí o bá gba ifiranṣẹ afura, pa fóònù náà kí o sì pe ilé-ifowópamọ́ rẹ.",

        ["ha"]     = "NaijaShield Gargadi. Wani yana ƙoƙarin yaudara ka. " +
                     "Kada ka ba kowa O T P, PIN, ko bayanan bankin ka. " +
                     "Idan ka sami sakon da ake zargi, datse kiran kuma ka kira bankin ka.",

        ["ig"]     = "NaijaShield Ọchọcha. Onye na-agbalị igus gị. " +
                     "Emekwala ka onye ọ bụla nweta O T P, PIN, ma ọ bụ ozi banki gị. " +
                     "Ọ bụrụ na i natara ozi ihe egwu, mechie ekwentị wee kpọọ banki gị.",
    };

    public static void MapAtVoiceEndpoints(this WebApplication app)
    {
        // AT sends form-encoded POST when call is answered; we return AT XML
        app.MapPost("/api/at/voice-action", VoiceAction).AllowAnonymous();
    }

    private static IResult VoiceAction(
        HttpContext context,
        ILogger<Program> logger,
        string? lang = "en")
    {
        var language    = lang ?? "en";
        var warningText = WarningTexts.GetValueOrDefault(language, WarningTexts["en"]);

        // Log call event details AT sends in the form body
        var sessionId = context.Request.Form["sessionId"].FirstOrDefault() ?? "(unknown)";
        var caller    = context.Request.Form["callerNumber"].FirstOrDefault()
                     ?? context.Request.Form["destinationNumber"].FirstOrDefault()
                     ?? "(unknown)";

        logger.LogInformation(
            "[AT Voice] Warning call answered sessionId={Session} number={Number} lang={Lang}",
            sessionId, caller, language);

        // Africa's Talking Voice XML — <Say> invokes AT's built-in TTS engine
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Response>
              <Say voice="woman" playBeep="false">{warningText}</Say>
              <Hangup />
            </Response>
            """;

        return Results.Content(xml, "application/xml");
    }
}
