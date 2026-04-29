using Azure;
using Azure.Communication.Email;

namespace naija_shield_backend.Services;

/// <summary>
/// Sends transactional emails via Azure Communication Services.
/// </summary>
public class EmailService
{
    private readonly EmailClient _emailClient;
    private readonly string _senderAddress;
    private readonly string _frontendBaseUrl;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _logger = logger;

        var connectionString = config["ACS-Email-Connection-String"]
            ?? throw new InvalidOperationException("ACS-Email-Connection-String not found in configuration");

        _senderAddress = config["ACS-Email-Sender"]
            ?? throw new InvalidOperationException("ACS-Email-Sender not found in configuration");

        _frontendBaseUrl = config["Frontend-Base-Url"]?.TrimEnd('/')
            ?? "http://localhost:3000";

        _emailClient = new EmailClient(connectionString);
    }

    /// <summary>
    /// Sends an invitation email with a link for the user to accept and set their password.
    /// </summary>
    public async Task<bool> SendInviteEmailAsync(string recipientEmail, string recipientName, string role, string inviteToken)
    {
        var inviteLink = $"{_frontendBaseUrl}/invite/accept?token={inviteToken}";
        var roleName = FormatRoleName(role);

        var subject = "You've been invited to NaijaShield";

        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin:0; padding:0; background-color:#f4f6f9; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f6f9; padding:40px 0;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 4px 24px rgba(0,0,0,0.08);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #1a5632 0%, #2d8a4e 100%); padding:32px 40px; text-align:center;"">
                            <h1 style=""margin:0; color:#ffffff; font-size:28px; font-weight:700; letter-spacing:-0.5px;"">🛡️ NaijaShield</h1>
                            <p style=""margin:8px 0 0; color:rgba(255,255,255,0.85); font-size:14px;"">Protecting Nigeria's Digital Future</p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:40px;"">
                            <h2 style=""margin:0 0 16px; color:#1a1a2e; font-size:22px; font-weight:600;"">Welcome aboard, {recipientName}!</h2>
                            <p style=""margin:0 0 24px; color:#4a4a68; font-size:15px; line-height:1.7;"">
                                You've been invited to join <strong>NaijaShield</strong> as a <strong>{roleName}</strong>.
                                Click the button below to set your password and activate your account.
                            </p>
                            <!-- CTA Button -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"" style=""padding:8px 0 32px;"">
                                        <a href=""{inviteLink}"" 
                                           style=""display:inline-block; background:linear-gradient(135deg, #1a5632 0%, #2d8a4e 100%); 
                                                  color:#ffffff; text-decoration:none; padding:14px 40px; border-radius:8px; 
                                                  font-size:16px; font-weight:600; letter-spacing:0.3px;"">
                                            Accept Invitation
                                        </a>
                                    </td>
                                </tr>
                            </table>
                            <!-- Info Box -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f8faf9; border-radius:8px; border-left:4px solid #2d8a4e;"">
                                <tr>
                                    <td style=""padding:16px 20px;"">
                                        <p style=""margin:0; color:#4a4a68; font-size:13px; line-height:1.6;"">
                                            ⏰ This invitation expires in <strong>48 hours</strong>.<br>
                                            🔒 If you didn't expect this email, you can safely ignore it.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                            <!-- Fallback Link -->
                            <p style=""margin:24px 0 0; color:#8a8aa3; font-size:12px; line-height:1.6;"">
                                If the button doesn't work, copy and paste this link into your browser:<br>
                                <a href=""{inviteLink}"" style=""color:#2d8a4e; word-break:break-all;"">{inviteLink}</a>
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#f8faf9; padding:24px 40px; text-align:center; border-top:1px solid #e8ebe9;"">
                            <p style=""margin:0; color:#8a8aa3; font-size:12px;"">
                                © {DateTime.UtcNow.Year} NaijaShield · Cybersecurity Operations Platform
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        var plainTextContent = $@"Welcome to NaijaShield, {recipientName}!

You've been invited to join NaijaShield as a {roleName}.

Accept your invitation by visiting: {inviteLink}

This invitation expires in 48 hours.

If you didn't expect this email, you can safely ignore it.

© {DateTime.UtcNow.Year} NaijaShield";

        try
        {
            var emailMessage = new EmailMessage(
                senderAddress: _senderAddress,
                recipientAddress: recipientEmail,
                content: new EmailContent(subject)
                {
                    Html = htmlContent,
                    PlainText = plainTextContent
                });

            EmailSendOperation operation = await _emailClient.SendAsync(
                WaitUntil.Completed, emailMessage);

            _logger.LogInformation(
                "Invite email sent to {Email} (Operation ID: {OperationId})",
                recipientEmail, operation.Id);

            return true;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Failed to send invite email to {Email}: {Message}",
                recipientEmail, ex.Message);
            return false;
        }
    }

    private static string FormatRoleName(string role) => role switch
    {
        "SOC_ANALYST" => "SOC Analyst",
        "COMPLIANCE_OFFICER" => "Compliance Officer",
        "SYSTEM_ADMIN" => "System Admin",
        _ => role
    };
}
