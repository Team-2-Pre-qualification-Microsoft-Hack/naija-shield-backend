# Azure Key Vault Connection Troubleshooting

This guide helps you diagnose and fix Azure Key Vault connection issues.

## Current Error

```
ArgumentException: Format of the initialization string does not conform to specification starting at index 0.
```

This error means the Cosmos DB connection string is **empty or invalid**, which happens when Key Vault authentication fails silently.

---

## Step-by-Step Troubleshooting

### Step 1: Check if you're logged into Azure CLI

```bash
# Check current login status
az account show
```

**Expected Output:**
```json
{
  "id": "your-subscription-id",
  "name": "Your Subscription Name",
  "user": {
    "name": "your-email@domain.com",
    "type": "user"
  }
}
```

**If you see an error:**
```bash
# Login to Azure
az login

# If you have multiple subscriptions, set the correct one
az account set --subscription "your-subscription-id"
```

---

### Step 2: Verify Access to Key Vault

```bash
# Try to list secrets in your Key Vault
az keyvault secret list --vault-name rg-naijashield-dev-key --query "[].name" -o table
```

**Expected Output:**
```
Result
-------------------------
OpenAI-Key
Cosmos-Connection-String
Search-Key
SignalR-Connection-String
JWT-Secret
```

**If you see "Forbidden" or access denied:**
1. You don't have permission to access the Key Vault
2. Ask the Key Vault administrator to grant you access:

```bash
# Administrator runs this (replace YOUR_EMAIL)
az keyvault set-policy \
  --name rg-naijashield-dev-key \
  --upn YOUR_EMAIL@domain.com \
  --secret-permissions get list
```

---

### Step 3: Test Getting a Specific Secret

```bash
# Try to get the Cosmos DB connection string
az keyvault secret show \
  --vault-name rg-naijashield-dev-key \
  --name "Cosmos-Connection-String" \
  --query "value" -o tsv
```

**Expected Output:**
```
AccountEndpoint=https://your-cosmos-account.documents.azure.com:443/;AccountKey=...
```

**If this works but the app still fails:**
The issue is with `DefaultAzureCredential` in your code.

---

### Step 4: Check DefaultAzureCredential Authentication Chain

`DefaultAzureCredential` tries multiple authentication methods in this order:

1. **EnvironmentCredential** - Environment variables
2. **ManagedIdentityCredential** - Azure VM/App Service managed identity
3. **SharedTokenCacheCredential** - Cached credentials
4. **VisualStudioCredential** - Visual Studio login
5. **VisualStudioCodeCredential** - VS Code login
6. **AzureCliCredential** - Azure CLI (`az login`) ? Most common for local dev
7. **AzurePowerShellCredential** - Azure PowerShell
8. **InteractiveBrowserCredential** - Browser popup

**Make sure Azure CLI is your active authentication:**
```bash
# Verify Azure CLI is authenticated
az account show

# If not, login
az login
```

---

## Solution Options

### ? Option 1: Fix Azure Key Vault Authentication (Recommended)

**Step 1:** Make sure you're logged in:
```bash
az login
```

**Step 2:** Verify access to Key Vault:
```bash
az keyvault secret list --vault-name rg-naijashield-dev-key
```

**Step 3:** Restart your application:
```bash
dotnet run
```

**You should see:**
```
Fetching secrets from Azure Key Vault: https://rg-naijashield-dev-key.vault.azure.net/
Email connection string retrieved from Key Vault.
All secrets successfully retrieved from Key Vault!
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
```

---

### ? Option 2: Use Local Configuration (If No Azure Access)

If you **don't have access to Azure Key Vault** or are working offline:

**Step 1:** Update `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "UseKeyVault": false,
  "Secrets": {
    "OpenAI-Key": "PASTE_YOUR_OPENAI_KEY_HERE",
    "Cosmos-Connection-String": "AccountEndpoint=https://YOUR-COSMOS.documents.azure.com:443/;AccountKey=YOUR_KEY_HERE==",
    "Search-Key": "PASTE_YOUR_SEARCH_KEY_HERE",
    "SignalR-Connection-String": "Endpoint=https://YOUR-SIGNALR.service.signalr.net;AccessKey=YOUR_KEY_HERE==",
    "JWT-Secret": "this-is-a-very-long-development-jwt-secret-at-least-32-characters-long",
    "Email-Connection-String": ""
  },
  "Frontend": {
    "BaseUrl": "http://localhost:3000"
  },
  "Email": {
    "SenderAddress": "DoNotReply@naijashield.com"
  }
}
```

**Step 2:** Get your connection strings from Azure Portal or CLI:

**Cosmos DB:**
```bash
az cosmosdb keys list \
  --name YOUR-COSMOS-ACCOUNT \
  --resource-group YOUR-RESOURCE-GROUP \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

**SignalR:**
```bash
az signalr key list \
  --name YOUR-SIGNALR-NAME \
  --resource-group YOUR-RESOURCE-GROUP \
  --query "primaryConnectionString" -o tsv
```

**Search:**
```bash
az search admin-key show \
  --resource-group YOUR-RESOURCE-GROUP \
  --service-name YOUR-SEARCH-SERVICE \
  --query "primaryKey" -o tsv
```

**OpenAI:** Get from Azure Portal ? Azure OpenAI resource ? Keys and Endpoint

**Step 3:** Restart the application:
```bash
dotnet run
```

**You should see:**
```
??  Using local configuration (Development mode)
??  DO NOT use this mode in production!
Local configuration loaded successfully.
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
```

---

## Common Issues & Solutions

### Issue: "Please run 'az login' to setup account"

**Solution:**
```bash
az login
```

### Issue: "The user, group or application does not have secrets get permission"

**Solution:** Ask your Key Vault administrator to grant you access:
```bash
az keyvault set-policy \
  --name rg-naijashield-dev-key \
  --upn YOUR_EMAIL@domain.com \
  --secret-permissions get list
```

### Issue: "AADSTS50020: User account from identity provider does not exist"

**Solution:** You're logged in with a different account. Login with the correct account:
```bash
az logout
az login
```

### Issue: Still getting connection string errors with UseKeyVault: false

**Solution:** Make sure you're replacing ALL placeholder values in the Secrets section:
- ? Use **actual connection strings** from Azure
- ? Don't leave `"your-cosmos-connection-string-here"`

---

## Quick Test Script

Create a file `test-keyvault.sh` (Linux/Mac) or `test-keyvault.ps1` (Windows PowerShell):

**PowerShell (Windows):**
```powershell
# test-keyvault.ps1
Write-Host "Testing Azure Key Vault Access..." -ForegroundColor Cyan

# Check Azure CLI login
Write-Host "`n1. Checking Azure CLI login..." -ForegroundColor Yellow
az account show --query "name" -o tsv

# Check Key Vault access
Write-Host "`n2. Checking Key Vault access..." -ForegroundColor Yellow
az keyvault secret list --vault-name rg-naijashield-dev-key --query "[].name" -o table

# Test getting a secret
Write-Host "`n3. Testing secret retrieval..." -ForegroundColor Yellow
$cosmosSecret = az keyvault secret show --vault-name rg-naijashield-dev-key --name "Cosmos-Connection-String" --query "value" -o tsv

if ($cosmosSecret) {
    Write-Host "? Successfully retrieved Cosmos DB connection string!" -ForegroundColor Green
    Write-Host "   First 50 characters: $($cosmosSecret.Substring(0, [Math]::Min(50, $cosmosSecret.Length)))..." -ForegroundColor Gray
} else {
    Write-Host "? Failed to retrieve secret" -ForegroundColor Red
}

Write-Host "`nIf all checks passed, run: dotnet run" -ForegroundColor Cyan
```

**Run it:**
```powershell
.\test-keyvault.ps1
```

---

## Next Steps

1. **Run the troubleshooting steps above**
2. **Choose Option 1 (Azure Key Vault) or Option 2 (Local Config)**
3. **Restart your application**
4. **Check the startup logs for success messages**

---

## Still Having Issues?

If you're still stuck, check these logs when starting the app:

**Success (Key Vault mode):**
```
Fetching secrets from Azure Key Vault: https://rg-naijashield-dev-key.vault.azure.net/
Email connection string retrieved from Key Vault.
All secrets successfully retrieved from Key Vault!
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
```

**Success (Local mode):**
```
??  Using local configuration (Development mode)
??  DO NOT use this mode in production!
Local configuration loaded successfully.
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
```

**Failure:**
```
? CRITICAL ERROR: Failed to retrieve secrets from Azure Key Vault.
Error: DefaultAzureCredential failed to retrieve a token...
```

If you see the failure message, follow the troubleshooting steps in the error output.

---

**Version:** 1.0  
**Last Updated:** April 2025
