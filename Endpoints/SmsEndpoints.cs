using naija_shield_backend.DTOs;
using naija_shield_backend.Services;

namespace naija_shield_backend.Endpoints;

public static class SmsEndpoints
{
    public static void MapSmsEndpoints(this WebApplication app)
    {
        var sms = app.MapGroup("/api/sms");

        // ===================================================
        // POST /api/sms/ingest — Africa's Talking webhook
        // Africa's Talking sends application/x-www-form-urlencoded
        // ===================================================
        sms.MapPost("/ingest", async (HttpContext ctx, SmsService smsService) =>
        {
            var form = await ctx.Request.ReadFormAsync();

            var request = new SmsIngestRequest
            {
                From = form["from"].ToString(),
                To = form["to"].ToString(),
                Text = form["text"].ToString(),
                Id = form["id"].ToString(),
                Date = form["date"].ToString(),
                LinkId = form["linkId"].ToString()
            };

            if (string.IsNullOrWhiteSpace(request.Text))
                return Results.Ok(); // acknowledge empty payloads silently

            return await smsService.ProcessIncomingSmsAsync(request);
        })
        .AllowAnonymous()
        .DisableAntiforgery()
        .WithName("SmsIngest")
        .WithDescription("Africa's Talking incoming SMS webhook");
    }
}
