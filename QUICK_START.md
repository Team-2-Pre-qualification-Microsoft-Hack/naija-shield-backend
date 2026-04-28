# NaijaShield Authentication - Quick Start Guide

Get the NaijaShield authentication system up and running in 10 minutes.

## Prerequisites

- .NET 9 SDK installed
- Azure Cosmos DB account created
- Azure Key Vault created
- Azure CLI installed and logged in

---

## Step 1: Configure Azure Key Vault (2 minutes)

### Generate JWT Secret
```bash
# Generate a secure 64-character secret
openssl rand -base64 64
```

### Add to Key Vault
```bash
# Replace with your actual values
VAULT_NAME="rg-naijashield-dev-key"
JWT_SECRET="your-generated-secret-from-above"

# Add JWT secret
az keyvault secret set \
  --vault-name "$VAULT_NAME" \
  --name "JWT-Secret" \
  --value "$JWT_SECRET"

# Verify it was added
az keyvault secret show \
  --vault-name "$VAULT_NAME" \
  --name "JWT-Secret" \
  --query "value" -o tsv
```

### Verify Other Secrets
Ensure these secrets exist in Key Vault:
```bash
az keyvault secret list --vault-name "$VAULT_NAME" --query "[].name" -o table
```

Expected secrets:
- `JWT-Secret` ?
- `Cosmos-Connection-String` ?
- `OpenAI-Key` ?
- `Search-Key` ?
- `SignalR-Connection-String` ?

---

## Step 2: Update Configuration (1 minute)

Edit `appsettings.json`:

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
    "BaseUrl": "http://localhost:3000"
  }
}
```

Update the frontend URL to match your frontend development server.

---

## Step 3: Build and Run (1 minute)

```bash
# Restore packages
dotnet restore

# Build project
dotnet build

# Run application
dotnet run
```

The API will start at:
- HTTPS: `https://localhost:7000`
- HTTP: `http://localhost:5000` (redirects to HTTPS)

---

## Step 4: Create Initial Admin User (3 minutes)

### Option A: Using the Helper Script (Recommended)

```bash
# Run the admin creation script
dotnet run --project naija-shield-backend.csproj Scripts/CreateAdminUser.cs
```

Follow the prompts:
1. Enter Cosmos DB connection string
2. Enter admin name
3. Enter admin email
4. Enter password (hidden)
5. Confirm password

### Option B: Manual Creation

1. Generate password hash:
```bash
# Using C# Interactive or a small console app
dotnet fsi
> #r "nuget: BCrypt.Net-Next, 4.0.3"
> BCrypt.Net.BCrypt.HashPassword("YourPassword123", 12)
```

2. Copy the hash (starts with `$2a$12$`)

3. Open Azure Portal ? Cosmos DB ? NaijaShieldDB ? Users ? New Item

4. Paste this JSON (replace password hash and email):
```json
{
  "id": "USR-001",
  "name": "System Administrator",
  "email": "admin@yourdomain.com",
  "password": "$2a$12$YOUR_HASHED_PASSWORD_HERE",
  "role": "SYSTEM_ADMIN",
  "organisation": "NaijaShield",
  "status": "Active",
  "inviteToken": null,
  "inviteExpiry": null,
  "lastActive": "2025-04-27T10:00:00Z",
  "createdAt": "2025-04-27T10:00:00Z",
  "refreshToken": null,
  "refreshTokenExpiry": null,
  "failedLoginAttempts": 0,
  "lockoutUntil": null,
  "type": "user"
}
```

---

## Step 5: Test the API (3 minutes)

### Test 1: Login

```bash
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "email": "admin@yourdomain.com",
    "password": "YourPassword123"
  }'
```

**Expected Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "user": {
    "id": "USR-001",
    "name": "System Administrator",
    "email": "admin@yourdomain.com",
    "role": "SYSTEM_ADMIN",
    "organisation": "NaijaShield"
  }
}
```

? **Success!** Copy the `token` value for next test.

### Test 2: Create Invitation

```bash
# Replace YOUR_TOKEN with the token from Test 1
TOKEN="YOUR_TOKEN_HERE"

curl -X POST https://localhost:7000/api/auth/invite \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -k \
  -d '{
    "email": "analyst@mtn.ng",
    "name": "Test Analyst",
    "role": "SOC_ANALYST"
  }'
```

**Expected Response:**
```json
{
  "inviteId": "USR-002",
  "email": "analyst@mtn.ng",
  "name": "Test Analyst",
  "role": "SOC_ANALYST",
  "expiresAt": "2025-04-29T10:00:00Z",
  "status": "Pending"
}
```

? **Success!** Check the console logs for the invitation email details (since we're using the placeholder email service).

### Test 3: Accept Invitation

```bash
# Copy the inviteToken from console logs
INVITE_TOKEN="token-from-console-logs"

curl -X POST https://localhost:7000/api/auth/invite/accept \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "inviteToken": "'$INVITE_TOKEN'",
    "password": "AnalystPassword123!",
    "confirmPassword": "AnalystPassword123!"
  }'
```

**Expected Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "user": {
    "id": "USR-002",
    "name": "Test Analyst",
    "email": "analyst@mtn.ng",
    "role": "SOC_ANALYST",
    "organisation": "NaijaShield"
  }
}
```

? **Success!** The analyst account is now active.

### Test 4: Verify Role Permissions

```bash
# Login as analyst
curl -X POST https://localhost:7000/api/auth/login \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "email": "analyst@mtn.ng",
    "password": "AnalystPassword123!"
  }'

# Copy the token
ANALYST_TOKEN="analyst-token-here"

# Try to create invitation (should fail - insufficient permissions)
curl -X POST https://localhost:7000/api/auth/invite \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ANALYST_TOKEN" \
  -k \
  -d '{
    "email": "test@test.com",
    "name": "Test",
    "role": "SOC_ANALYST"
  }'
```

**Expected Response:**
```json
{
  "error": "INSUFFICIENT_PERMISSIONS",
  "message": "Only System Admins can invite new users"
}
```

? **Success!** Role-based permissions are working correctly.

---

## ?? You're Done!

Your NaijaShield authentication system is now running with:
- ? Admin user created
- ? JWT authentication working
- ? Invitation system working
- ? Role-based permissions enforced

---

## Next Steps

### 1. Integrate with Frontend
Share these details with the frontend team:
- **API Base URL:** `https://localhost:7000`
- **API Documentation:** See `API_REFERENCE.md`
- **Error Codes:** See `API_REFERENCE.md` for standard error format

### 2. Implement Email Service
Replace the placeholder in `Services/EmailService.cs`:
- Use Azure Communication Services, OR
- Use SendGrid, OR
- Use another email provider

See `DEPLOYMENT_CHECKLIST.md` for details.

### 3. Production Deployment
Follow the checklist in `DEPLOYMENT_CHECKLIST.md` before deploying to production.

### 4. Testing
Run the full test suite using `TESTING_GUIDE.md` to verify all features.

---

## Troubleshooting

### Error: "JWT Secret not configured"
**Solution:** Verify JWT-Secret exists in Key Vault and you're logged in with Azure CLI:
```bash
az login
az keyvault secret show --vault-name "rg-naijashield-dev-key" --name "JWT-Secret"
```

### Error: "Cannot connect to Cosmos DB"
**Solution:** Verify Cosmos-Connection-String in Key Vault:
```bash
az keyvault secret show --vault-name "rg-naijashield-dev-key" --name "Cosmos-Connection-String"
```

### Error: Certificate validation failed
**Solution:** For development, use the `-k` flag with curl to skip certificate validation:
```bash
curl -k https://localhost:7000/api/auth/login ...
```

### Ports already in use
**Solution:** Kill existing process or change ports in `launchSettings.json`.

---

## Quick Reference

### User Roles
- `SOC_ANALYST` - View threats, investigate incidents
- `COMPLIANCE_OFFICER` - View compliance, generate reports
- `SYSTEM_ADMIN` - Full access, manage users

### Token Expiry
- Access token: 1 hour
- Refresh token: 7 days
- Invite token: 48 hours

### Rate Limiting
- 5 failed login attempts = 15-minute account lockout

---

**Version:** 1.0  
**Last Updated:** April 2025  
**Questions?** Check the full documentation or contact the backend team.
