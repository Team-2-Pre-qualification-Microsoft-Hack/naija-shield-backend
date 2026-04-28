# NaijaShield Authentication Setup Checklist

This checklist ensures your NaijaShield backend authentication system is properly configured and ready for production.

## ? Pre-Deployment Checklist

### 1. Azure Key Vault Configuration

- [ ] Azure Key Vault created: `rg-naijashield-dev-key`
- [ ] JWT-Secret added to Key Vault
  ```bash
  # Generate a secure random secret (minimum 32 characters)
  openssl rand -base64 64
  
  # Add to Key Vault
  az keyvault secret set \
    --vault-name "rg-naijashield-dev-key" \
    --name "JWT-Secret" \
    --value "YOUR_GENERATED_SECRET_HERE"
  ```
- [ ] Verify secret is accessible:
  ```bash
  az keyvault secret show \
    --vault-name "rg-naijashield-dev-key" \
    --name "JWT-Secret"
  ```

### 2. Cosmos DB Setup

- [ ] Cosmos DB account created
- [ ] Connection string added to Key Vault as `Cosmos-Connection-String`
- [ ] Database `NaijaShieldDB` created (auto-created by app on first run)
- [ ] Container `Users` created with partition key `/type` (auto-created by app on first run)
- [ ] Verify container settings:
  - Partition key: `/type`
  - Indexing policy: Default (all properties indexed)

### 3. Initial Admin User

- [ ] Generated password hash using PasswordHasher utility
- [ ] Created initial SYSTEM_ADMIN user in Cosmos DB (see `Scripts/CreateInitialAdmin.md`)
- [ ] Verified admin login works
- [ ] Stored admin credentials securely

### 4. Configuration Files

- [ ] `appsettings.json` configured:
  ```json
  {
    "Jwt": {
      "Issuer": "NaijaShield",
      "Audience": "NaijaShield"
    },
    "Cosmos": {
      "DatabaseName": "NaijaShieldDB",
      "UserContainerName": "Users"
    },
    "Frontend": {
      "BaseUrl": "https://your-production-domain.com"
    }
  }
  ```
- [ ] Production secrets NOT in `appsettings.json` (use Key Vault)
- [ ] `appsettings.Production.json` created (if needed)

### 5. CORS Configuration

- [ ] Frontend URL added to CORS policy in `Program.cs`
- [ ] Localhost URLs removed for production deployment
- [ ] Verify CORS policy in `Program.cs`:
  ```csharp
  policy.WithOrigins(
      "https://your-production-domain.com"
      // Remove localhost URLs in production
  )
  ```

### 6. HTTPS Configuration

- [ ] SSL certificate configured for production
- [ ] `RequireHttpsMetadata` set to `true` (already configured)
- [ ] All endpoints accessible via HTTPS only
- [ ] HTTP to HTTPS redirect configured (if needed)

### 7. Email Service (Azure Communication Services)

- [ ] Azure Communication Services resource created
- [ ] Email Communication Services resource created
- [ ] Domain provisioned (Azure Managed or Custom Domain)
- [ ] Domain verified and connected to Communication Services
- [ ] Connection string added to Key Vault as `Email-Connection-String`
  ```bash
  az keyvault secret set \
    --vault-name "rg-naijashield-dev-key" \
    --name "Email-Connection-String" \
    --value "YOUR_ACS_CONNECTION_STRING"
  ```
- [ ] Sender address configured in `appsettings.json`
- [ ] Test invitation email sent successfully
- [ ] Email received in inbox (not spam folder)
- [ ] HTML email renders correctly in major email clients

> **?? For detailed setup instructions, see [EMAIL_SERVICE_SETUP.md](EMAIL_SERVICE_SETUP.md)**

### 8. Azure Identity & Permissions

- [ ] Managed Identity enabled for App Service (if deploying to Azure)
- [ ] Managed Identity granted access to Key Vault
  ```bash
  az keyvault set-policy \
    --name "rg-naijashield-dev-key" \
    --object-id "YOUR_MANAGED_IDENTITY_OBJECT_ID" \
    --secret-permissions get list
  ```
- [ ] DefaultAzureCredential authentication working

### 9. Security Testing

- [ ] Tested login with valid credentials ? Success ?
- [ ] Tested login with invalid credentials ? 401 error ?
- [ ] Tested 5 failed login attempts ? Account locked ?
- [ ] Tested login after lockout ? 429 error ?
- [ ] Tested invite creation by SYSTEM_ADMIN ? Success ?
- [ ] Tested invite creation by non-admin ? 403 error ?
- [ ] Tested invite acceptance with valid token ? Success ?
- [ ] Tested invite acceptance with expired token ? 400 error ?
- [ ] Tested token refresh with valid token ? Success ?
- [ ] Tested token refresh with expired token ? 401 error ?
- [ ] Tested logout ? Refresh token invalidated ?
- [ ] Tested accessing protected endpoint without token ? 401 error ?
- [ ] Tested accessing admin-only endpoint as non-admin ? 403 error ?

### 10. Role-Based Access Testing

- [ ] **SOC_ANALYST** can access:
  - `/api/overview` ?
  - `/api/threat-feed` ?
- [ ] **SOC_ANALYST** cannot access:
  - `/api/compliance` ?
  - `/api/user-management` ?
  - `/api/settings` ?
- [ ] **COMPLIANCE_OFFICER** can access:
  - `/api/overview` ?
  - `/api/compliance` ?
- [ ] **COMPLIANCE_OFFICER** cannot access:
  - `/api/threat-feed` ?
  - `/api/user-management` ?
  - `/api/settings` ?
- [ ] **SYSTEM_ADMIN** can access:
  - `/api/overview` ?
  - `/api/threat-feed` ?
  - `/api/compliance` ?
  - `/api/user-management` ?
  - `/api/settings` ?

### 11. Monitoring & Logging

- [ ] Application Insights configured (optional but recommended)
- [ ] Logging configured for:
  - Failed login attempts
  - Account lockouts
  - Invitation sends
  - Token refresh events
- [ ] Sensitive data (passwords, PII) NOT logged
- [ ] Error tracking configured

### 12. Documentation

- [ ] API documentation updated (Swagger/OpenAPI)
- [ ] Frontend team informed of:
  - API endpoint URLs
  - Token format and claims
  - Error response formats
  - CORS requirements
- [ ] Deployment guide created
- [ ] Runbook created for common issues

## ? Post-Deployment Verification

### After deploying to production:

1. [ ] Health check endpoint responds
2. [ ] Login works with admin credentials
3. [ ] Can create new invitation
4. [ ] Invitation email received (check spam folder)
5. [ ] Can accept invitation and set password
6. [ ] Token refresh works
7. [ ] Logout works
8. [ ] Role-based permissions enforced
9. [ ] No secrets in logs or error messages
10. [ ] HTTPS enforced (HTTP redirects to HTTPS)

## ?? Environment Variables / Key Vault Secrets

Ensure these are configured in Azure Key Vault:

| Secret Name | Purpose | Example |
|-------------|---------|---------|
| `JWT-Secret` | JWT token signing key | `your-64-char-random-string` |
| `Cosmos-Connection-String` | Cosmos DB connection | `AccountEndpoint=https://...` |
| `Email-Connection-String` | Azure Communication Services connection | `endpoint=https://...;accesskey=...` |
| `OpenAI-Key` | Azure OpenAI API key | `abc123...` |
| `Search-Key` | Azure Search key | `xyz789...` |
| `SignalR-Connection-String` | SignalR connection | `Endpoint=https://...` |

## ?? Notes

- All timestamps are in UTC
- Password cost factor is 12 (BCrypt)
- Access tokens expire in 1 hour
- Refresh tokens expire in 7 days
- Invite tokens expire in 48 hours
- Account lockout duration is 15 minutes after 5 failed attempts

## ?? Common Issues & Solutions

### Issue: "JWT Secret not configured"
**Solution**: Ensure JWT-Secret exists in Key Vault and DefaultAzureCredential has access

### Issue: "Cosmos DB connection failed"
**Solution**: Verify Cosmos-Connection-String in Key Vault and connection string format

### Issue: "Token expired" on every request
**Solution**: Check server time is synchronized (NTP), verify token expiry logic

### Issue: Invitation email not received
**Solution**: Check email service configuration, verify email provider credentials

### Issue: CORS errors from frontend
**Solution**: Verify frontend URL in CORS policy, check HTTPS/HTTP mismatch

---

**Last Updated**: April 2025  
**Version**: 1.0  
**Owner**: Backend Team
