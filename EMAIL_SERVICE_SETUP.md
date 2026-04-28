# Azure Communication Services Email Setup Guide

This guide walks you through setting up Azure Communication Services Email for the NaijaShield authentication system.

## Overview

Azure Communication Services Email allows you to send transactional emails (like invitation emails) from your application. The email service is now fully implemented and ready to use once you configure the Azure resources.

---

## Prerequisites

- Azure subscription
- Azure CLI installed and authenticated (`az login`)
- Owner or Contributor access to create Azure resources

---

## Step 1: Create Azure Communication Services Resource

### Option A: Using Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **Create a resource**
3. Search for **Communication Services**
4. Click **Create**
5. Fill in the details:
   - **Subscription:** Your subscription
   - **Resource Group:** `rg-naijashield-dev` (or your existing resource group)
   - **Resource Name:** `naijashield-communication-services`
   - **Data Location:** Choose the same region as your other resources
6. Click **Review + Create**, then **Create**

### Option B: Using Azure CLI

```bash
# Set variables
RESOURCE_GROUP="rg-naijashield-dev"
LOCATION="eastus"
ACS_NAME="naijashield-communication-services"

# Create Communication Services resource
az communication create \
  --name $ACS_NAME \
  --location $LOCATION \
  --data-location "United States" \
  --resource-group $RESOURCE_GROUP
```

---

## Step 2: Create Email Communication Services Resource

Azure Communication Services Email is a separate resource that must be created.

### Using Azure Portal

1. In Azure Portal, search for **Email Communication Services**
2. Click **Create**
3. Fill in the details:
   - **Subscription:** Your subscription
   - **Resource Group:** `rg-naijashield-dev`
   - **Resource Name:** `naijashield-email`
   - **Region:** Same as your Communication Services
   - **Data Location:** Same as your Communication Services
4. Click **Review + Create**, then **Create**

### Using Azure CLI

```bash
# Set variables
EMAIL_SERVICE_NAME="naijashield-email"

# Create Email Communication Services
az communication email create \
  --name $EMAIL_SERVICE_NAME \
  --resource-group $RESOURCE_GROUP \
  --data-location "United States"
```

---

## Step 3: Set Up Custom Domain (Recommended) or Use Azure Managed Domain

### Option A: Azure Managed Domain (Quick Setup - Good for Testing)

1. Go to your Email Communication Services resource
2. Click on **Provision domains** ? **Add domain**
3. Select **Azure Managed Domain**
4. This provides a domain like `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.azurecomm.net`
5. Click **Add**
6. Wait for provisioning (2-5 minutes)

**Note:** Azure Managed Domains have a free quota of 100 emails per day. For production, use a custom domain.

### Option B: Custom Domain (Production)

1. Go to your Email Communication Services resource
2. Click on **Provision domains** ? **Add domain**
3. Select **Custom domain**
4. Enter your domain (e.g., `naijashield.com`)
5. Follow the DNS verification steps:
   - Add TXT records to your domain's DNS
   - Add CNAME records for email authentication
6. Click **Verify**
7. Once verified, the domain will be ready to use

**DNS Records Required:**

For domain `naijashield.com`:

```
Type: TXT
Name: @
Value: [Provided by Azure]

Type: TXT
Name: _dmarc
Value: v=DMARC1; p=none

Type: TXT
Name: [selector]._domainkey
Value: [DKIM key provided by Azure]

Type: CNAME
Name: [selector]._domainkey
Value: [DKIM CNAME provided by Azure]
```

---

## Step 4: Connect Email Service to Communication Services

1. Go to your **Communication Services** resource (not Email Communication Services)
2. Click **Domains** in the left menu
3. Click **Connect domain**
4. Select your Email Communication Services resource
5. Select the domain you provisioned
6. Click **Connect**

---

## Step 5: Get the Connection String

### Using Azure Portal

1. Go to your **Communication Services** resource
2. Click **Keys** in the left menu
3. Copy the **Connection string** (Primary or Secondary)

### Using Azure CLI

```bash
# Get connection string
az communication list-key \
  --name $ACS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query primaryConnectionString -o tsv
```

---

## Step 6: Add Connection String to Azure Key Vault

```bash
# Set variables
KEY_VAULT_NAME="rg-naijashield-dev-key"
CONNECTION_STRING="your-connection-string-from-step-5"

# Add to Key Vault
az keyvault secret set \
  --vault-name $KEY_VAULT_NAME \
  --name "Email-Connection-String" \
  --value "$CONNECTION_STRING"

# Verify it was added
az keyvault secret show \
  --vault-name $KEY_VAULT_NAME \
  --name "Email-Connection-String" \
  --query "value" -o tsv
```

---

## Step 7: Configure Sender Address

### Option 1: Update appsettings.json (for all environments)

```json
{
  "Email": {
    "SenderAddress": "DoNotReply@naijashield.com"
  }
}
```

### Option 2: Use Azure Managed Domain Address

If using Azure Managed Domain, use the provided sender address:

```json
{
  "Email": {
    "SenderAddress": "DoNotReply@xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx.azurecomm.net"
  }
}
```

**Important:** The sender address must match an allowed sender from your provisioned domain.

---

## Step 8: Test the Email Service

### Start the Application

```bash
dotnet run
```

### Test Invitation Email

```bash
# Login as admin
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourPassword123"
  }'

# Copy the token from response
TOKEN="paste-your-token-here"

# Create invitation
curl -X POST https://localhost:7000/api/auth/invite \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -k \
  -d '{
    "email": "test@example.com",
    "name": "Test User",
    "role": "SOC_ANALYST"
  }'
```

### Check Email Delivery

1. Check the application logs for "Email sent successfully" message
2. Check the recipient's inbox (including spam folder)
3. The email should arrive within 1-2 minutes

---

## Troubleshooting

### Issue: "Email-Connection-String not found in Key Vault"

**Solution:** The application will fall back to logging-only mode. This is expected if you haven't set up Azure Communication Services yet.

### Issue: "Failed to send email - 401 Unauthorized"

**Solution:** 
- Verify the connection string is correct
- Check that the connection string hasn't expired
- Ensure you copied the full connection string including `endpoint=` and `accesskey=`

### Issue: "Failed to send email - 400 Bad Request - InvalidSender"

**Solution:**
- Verify the sender address matches an allowed sender from your domain
- Check that the domain is properly verified and connected
- For Azure Managed Domains, use the provided `@azurecomm.net` address

### Issue: Email not received

**Solution:**
- Check spam/junk folder
- Verify recipient email address is valid
- Check Azure Communication Services logs in Azure Portal
- For Azure Managed Domain, check you haven't exceeded the 100 emails/day quota

### Issue: "Domain verification failed"

**Solution:**
- Wait 24-48 hours for DNS propagation
- Verify DNS records are correctly added
- Use `nslookup` or `dig` to verify DNS records are visible
- Check with your DNS provider's support if needed

---

## Email Service Features

### What's Implemented ?

- ? Beautiful HTML email template with NaijaShield branding
- ? Plain text fallback for email clients that don't support HTML
- ? Invitation link with 48-hour expiry notice
- ? Security tips and best practices
- ? Professional email styling
- ? Graceful fallback to logging if email service is unavailable
- ? Detailed logging for troubleshooting
- ? Async email sending (doesn't block API response)

### Email Template Preview

The invitation email includes:
- ??? NaijaShield logo and branding
- Personalized greeting
- Clear call-to-action button
- Expiry warning (48 hours)
- Security tips
- Plain text link fallback
- Professional footer

---

## Pricing

### Azure Communication Services Email Pricing (as of 2025)

**Azure Managed Domain:**
- First 100 emails/month: **Free**
- Additional emails: $0.0025 per email

**Custom Domain:**
- First 1,000 emails/month: **Free**
- 1,001 - 50,000: $0.0012 per email
- 50,001+: Volume pricing available

**Storage:**
- Email logs: Standard Azure Storage pricing

### Cost Estimation for NaijaShield

**Scenario 1: Small Team (10 invitations/month)**
- Using Azure Managed Domain: **Free** (under 100 emails/month)

**Scenario 2: Medium Team (500 invitations/month)**
- Using Custom Domain: **Free** (under 1,000 emails/month)

**Scenario 3: Large Organization (5,000 invitations/month)**
- Using Custom Domain:
  - First 1,000: Free
  - Next 4,000: 4,000 × $0.0012 = **$4.80/month**

---

## Alternative: Development/Testing Without Azure Communication Services

If you want to test the authentication system without setting up Azure Communication Services:

1. **Use Logging Mode (Current Default)**
   - Don't add `Email-Connection-String` to Key Vault
   - Invitation details will be logged to console
   - Manually share the invite link with users

2. **Use a Different Email Service**
   - Modify `Services/EmailService.cs`
   - Replace Azure Communication Services client with:
     - SendGrid
     - Mailgun
     - AWS SES
     - Or any SMTP provider

---

## Production Checklist

Before going to production:

- [ ] Azure Communication Services resource created
- [ ] Email Communication Services resource created
- [ ] Custom domain provisioned and verified (recommended)
- [ ] Domain connected to Communication Services
- [ ] Connection string added to Key Vault
- [ ] Sender address configured in appsettings.json
- [ ] Test email sent and received successfully
- [ ] Email appears in inbox (not spam)
- [ ] HTML rendering looks good in major email clients (Gmail, Outlook, Apple Mail)
- [ ] Plain text version readable
- [ ] Links work correctly
- [ ] Monitoring/alerts set up for email failures

---

## Monitoring Email Delivery

### Azure Portal

1. Go to Communication Services resource
2. Click **Metrics** in the left menu
3. Add metrics:
   - Email - Requests
   - Email - Delivery Status
   - Email - Error Rate

### Application Insights (Optional)

Add Application Insights to track:
- Email send success/failure rates
- Email delivery times
- Error patterns

---

## Security Best Practices

1. ? **Never commit connection strings to code**
   - Always use Azure Key Vault
   - Connection string is fetched at runtime

2. ? **Use custom domain for production**
   - Builds trust with recipients
   - Better deliverability
   - No email limits

3. ? **Configure DMARC, SPF, and DKIM**
   - Prevents email spoofing
   - Improves deliverability
   - Azure Communication Services handles this automatically

4. ? **Monitor for abuse**
   - Set up alerts for unusual sending patterns
   - Rate limit invitation creation (already implemented)

5. ? **Keep sender address professional**
   - Use `DoNotReply@naijashield.com` or `noreply@naijashield.com`
   - Don't use personal email addresses

---

## FAQ

**Q: Can I use Gmail/Outlook to send emails instead?**
A: For production applications, use Azure Communication Services or similar service. Personal email accounts have rate limits and may mark emails as spam.

**Q: How long does domain verification take?**
A: DNS propagation can take 24-48 hours, but often completes within a few hours.

**Q: Can I test without a custom domain?**
A: Yes, use Azure Managed Domain for testing. It provides 100 free emails/day.

**Q: What if I exceed the free tier?**
A: You'll be charged per email according to Azure pricing. Set up billing alerts to monitor costs.

**Q: Can I customize the email template?**
A: Yes, modify the `GenerateInvitationEmailHtml()` and `GenerateInvitationEmailPlainText()` methods in `Services/EmailService.cs`.

**Q: Will emails go to spam?**
A: If properly configured with DMARC/SPF/DKIM (Azure handles this), emails should land in inbox. Custom domains have better deliverability than Azure Managed Domains.

---

## Next Steps

1. ? **Set up Azure Communication Services** (follow this guide)
2. ? **Test email delivery** (send test invitation)
3. ? **Customize email template** (optional - update branding/copy)
4. ? **Monitor email metrics** (set up alerts)
5. ? **Go to production** (follow DEPLOYMENT_CHECKLIST.md)

---

**Version:** 1.0  
**Last Updated:** April 2025  
**For Questions:** Contact Backend Team or Azure Support
