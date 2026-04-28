using Azure.Communication.Email;

namespace naija_shield_backend.Services;

public interface IEmailService
{
    Task SendInvitationEmailAsync(string email, string name, string inviteToken);
}

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly EmailClient? _emailClient;
    private readonly string _senderAddress;
    private readonly string _frontendUrl;
    private readonly bool _isEmailEnabled;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _frontendUrl = _configuration["Frontend:BaseUrl"] ?? "https://naijashield.com";
        _senderAddress = _configuration["Email:SenderAddress"] ?? "DoNotReply@naijashield.com";
        
        // Get email connection string from configuration (loaded from Key Vault)
        var connectionString = _configuration["Email:ConnectionString"];
        
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                _emailClient = new EmailClient(connectionString);
                _isEmailEnabled = true;
                _logger.LogInformation("Azure Communication Services Email client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Azure Communication Services Email client. Email will be logged only.");
                _isEmailEnabled = false;
            }
        }
        else
        {
            _logger.LogWarning("Email connection string not configured. Emails will be logged only.");
            _isEmailEnabled = false;
        }
    }

    public async Task SendInvitationEmailAsync(string email, string name, string inviteToken)
    {
        var inviteLink = $"{_frontendUrl}/accept-invite?token={inviteToken}";
        
        // Always log the invitation details
        _logger.LogInformation("=== INVITATION EMAIL ===");
        _logger.LogInformation($"To: {email}");
        _logger.LogInformation($"Name: {name}");
        _logger.LogInformation($"Invite Link: {inviteLink}");
        _logger.LogInformation($"Token: {inviteToken}");
        _logger.LogInformation("========================");

        // If email is not enabled, just log and return
        if (!_isEmailEnabled || _emailClient == null)
        {
            _logger.LogWarning("Email service not configured. Invitation details logged above.");
            return;
        }

        try
        {
            // Create email content
            var subject = "Welcome to NaijaShield - Complete Your Account Setup";
            var htmlContent = GenerateInvitationEmailHtml(name, inviteLink);
            var plainTextContent = GenerateInvitationEmailPlainText(name, inviteLink);

            // Send email using Azure Communication Services
            var emailContent = new EmailContent(subject)
            {
                PlainText = plainTextContent,
                Html = htmlContent
            };

            var recipients = new EmailRecipients(new List<EmailAddress>
            {
                new EmailAddress(email, name)
            });

            var emailMessage = new EmailMessage(_senderAddress, recipients, emailContent);

            var emailSendOperation = await _emailClient.SendAsync(
                Azure.WaitUntil.Started,
                emailMessage
            );

            _logger.LogInformation($"Email sent successfully to {email}. Message ID: {emailSendOperation.Id}");

            // Optionally wait for the email to be delivered
            // var result = await emailSendOperation.WaitForCompletionAsync();
            // _logger.LogInformation($"Email delivery status: {result.Value.Status}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send invitation email to {email}. Invitation details were logged above.");
            // Don't throw - we don't want to fail user creation if email fails
            // The admin can manually share the invite link from the logs
        }
    }

    private string GenerateInvitationEmailHtml(string name, string inviteLink)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Welcome to NaijaShield</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 10px;
            padding: 40px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .logo {{
            font-size: 32px;
            font-weight: bold;
            color: #0078D4;
            margin-bottom: 10px;
        }}
        .shield-icon {{
            font-size: 48px;
        }}
        h1 {{
            color: #0078D4;
            font-size: 24px;
            margin-bottom: 20px;
        }}
        .welcome-text {{
            font-size: 16px;
            margin-bottom: 20px;
            color: #555;
        }}
        .cta-button {{
            display: inline-block;
            background-color: #0078D4;
            color: #ffffff;
            text-decoration: none;
            padding: 15px 40px;
            border-radius: 5px;
            font-weight: bold;
            font-size: 16px;
            margin: 20px 0;
        }}
        .cta-button:hover {{
            background-color: #005a9e;
        }}
        .info-box {{
            background-color: #f8f8f8;
            border-left: 4px solid #0078D4;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #e0e0e0;
            font-size: 12px;
            color: #888;
            text-align: center;
        }}
        .link-fallback {{
            word-wrap: break-word;
            color: #0078D4;
            font-size: 14px;
            margin-top: 15px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""shield-icon"">🛡️</div>
            <div class=""logo"">NaijaShield</div>
        </div>
        
        <h1>Welcome to NaijaShield!</h1>
        
        <p class=""welcome-text"">Hi {name},</p>
        
        <p class=""welcome-text"">
            You've been invited to join <strong>NaijaShield</strong>, Nigeria's premier fraud intelligence platform. 
            We're excited to have you on board!
        </p>
        
        <div class=""info-box"">
            <strong>⏰ Action Required:</strong> This invitation expires in <strong>48 hours</strong>. 
            Please complete your account setup as soon as possible.
        </div>
        
        <p class=""welcome-text"">
            To get started, click the button below to set your password and activate your account:
        </p>
        
        <div style=""text-align: center;"">
            <a href=""{inviteLink}"" class=""cta-button"">Complete Account Setup</a>
        </div>
        
        <p class=""link-fallback"">
            If the button doesn't work, copy and paste this link into your browser:<br>
            <a href=""{inviteLink}"">{inviteLink}</a>
        </p>
        
        <div class=""info-box"">
            <strong>🔐 Security Tips:</strong>
            <ul style=""margin: 10px 0; padding-left: 20px;"">
                <li>Choose a strong password (minimum 12 characters)</li>
                <li>Don't share this invitation link with anyone</li>
                <li>This link can only be used once</li>
            </ul>
        </div>
        
        <p class=""welcome-text"">
            Once you've completed setup, you'll have access to the NaijaShield platform where you can:
        </p>
        
        <ul style=""color: #555;"">
            <li>Monitor fraud intelligence in real-time</li>
            <li>Investigate suspicious activities</li>
            <li>Generate compliance reports</li>
            <li>Collaborate with your team</li>
        </ul>
        
        <p class=""welcome-text"">
            If you have any questions or need assistance, please contact your system administrator.
        </p>
        
        <div class=""footer"">
            <p>
                This is an automated message from NaijaShield. Please do not reply to this email.<br>
                © 2025 NaijaShield. All rights reserved.
            </p>
            <p style=""margin-top: 10px; color: #0078D4;"">
                🛡️ Protecting Nigeria from Fraud
            </p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateInvitationEmailPlainText(string name, string inviteLink)
    {
        return $@"
Welcome to NaijaShield!

Hi {name},

You've been invited to join NaijaShield, Nigeria's premier fraud intelligence platform. We're excited to have you on board!

ACTION REQUIRED: This invitation expires in 48 hours.

To get started, visit this link to set your password and activate your account:
{inviteLink}

SECURITY TIPS:
- Choose a strong password (minimum 12 characters)
- Don't share this invitation link with anyone
- This link can only be used once

Once you've completed setup, you'll have access to the NaijaShield platform where you can:
- Monitor fraud intelligence in real-time
- Investigate suspicious activities
- Generate compliance reports
- Collaborate with your team

If you have any questions or need assistance, please contact your system administrator.

---
This is an automated message from NaijaShield. Please do not reply to this email.
© 2025 NaijaShield. All rights reserved.

🛡️ Protecting Nigeria from Fraud
";
    }
}

