# Local Development Configuration Guide

## Overview

The NaijaShield backend now supports **two modes** for configuration:

1. **Production Mode** (Azure Key Vault) - Default, recommended for deployed environments
2. **Development Mode** (Local Configuration) - For local development without Azure CLI authentication

---

## Quick Start

### Option 1: Use Azure Key Vault (Recommended)

**Requirements:**
- Azure CLI installed
- Authenticated: `az login`
- Access to the Key Vault

**Configuration:**
```json
// appsettings.json or appsettings.Development.json
{
  "UseKeyVault": true  // This is the default
}
```

**Run:**
```bash
dotnet run
```

? **App will fetch secrets from Azure Key Vault**

---

### Option 2: Use Local Configuration (Development Only)

**When to use:**
- Don't have Azure CLI installed
- Don't have access to Azure Key Vault
- Want to quickly test without Azure authentication
- Working offline

**Configuration:**

1. **Set `UseKeyVault` to `false`** in `appsettings.Development.json`:

```json
{
  "UseKeyVault": false
}
```

2. **Add your secrets** to `appsettings.Development.json`:

```json
{
  "UseKeyVault": false,
  "Secrets": {
    "OpenAI-Key": "your-actual-openai-key",
    "Cosmos-Connection-String": "AccountEndpoint=https://...",
    "Search-Key": "your-search-key",
    "SignalR-Connection-String": "Endpoint=https://...",
    "JWT-Secret": "your-minimum-32-character-secret-for-development",
    "Email-Connection-String": "" // Leave empty to use logging mode
  }
}
```

3. **Run:**
```bash
dotnet run
```

?? **App will use local configuration (NOT Key Vault)**

---

## Configuration File Structure

### `appsettings.json` (Production)

```json
{
  "UseKeyVault": true,
  "KeyVault": {
    "Uri": "https://rg-naijashield-dev-key.vault.azure.net/"
  },
  "Jwt": {
    "Issuer": "NaijaShield",
    "Audience": "NaijaShield"
  },
  "Cosmos": {
    "DatabaseName": "NaijaShieldDB",
    "UserContainerName": "Users"
  },
  "Frontend": {
    "BaseUrl": "https://naijashield.com"
  },
  "Email": {
    "SenderAddress": "DoNotReply@naijashield.com"
  }
}
```

### `appsettings.Development.json` (Local Development)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "UseKeyVault": false,  // ?? Set to false for local development
  "Secrets": {
    "OpenAI-Key": "your-openai-key-here",
    "Cosmos-Connection-String": "your-cosmos-connection-string-here",
    "Search-Key": "your-search-key-here",
    "SignalR-Connection-String": "your-signalr-connection-string-here",
    "JWT-Secret": "your-minimum-32-character-jwt-secret-for-development-only",
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

---

## How It Works

### Startup Logic Flow

```
Application Starts
    ?
Check UseKeyVault setting
    ?
???????????????????????????
? UseKeyVault = true?     ?
???????????????????????????
    ?               ?
    YES             NO
    ?               ?
?????????????????  ??????????????????
? Azure KV Mode ?  ? Local Config   ?
?????????????????  ??????????????????
    ?                   ?
    ?                   ?
Try to connect      Load from
to Key Vault        appsettings
    ?                   ?
    ?                   ?
Success?                ?
    ?                   ?
    YES                 ?
    ?                   ?
Load secrets        Secrets loaded
from KV                 ?
    ?                   ?
    ?????????????????????
              ?
        App continues
```

### Code Implementation

**In `Program.cs`:**

```csharp
var useKeyVault = builder.Configuration.GetValue<bool>("UseKeyVault", true);

if (useKeyVault)
{
    // Try Azure Key Vault
    try
    {
        var credential = new DefaultAzureCredential();
        var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
        
        // Fetch secrets from Key Vault
        openAiKey = (await secretClient.GetSecretAsync("OpenAI-Key")).Value.Value;
        // ... other secrets
    }
    catch (Exception ex)
    {
        // Detailed error message + troubleshooting steps
        throw new InvalidOperationException("Key Vault error", ex);
    }
}
else
{
    // Load from local configuration
    openAiKey = builder.Configuration["Secrets:OpenAI-Key"] 
        ?? throw new InvalidOperationException("Secret not configured");
    // ... other secrets
}
```

---

## Security Best Practices

### ? DO

1. **Use Key Vault in Production**
   ```json
   {
     "UseKeyVault": true  // Always in production
   }
   ```

2. **Keep `appsettings.Development.json` in .gitignore**
   ```gitignore
   appsettings.Development.json
   ```

3. **Use strong secrets even in development**
   - JWT Secret: Minimum 32 characters
   - Use real Azure resources for testing

4. **Document which mode you're using**
   - Add comments in configuration files
   - Update README for team members

### ? DON'T

1. **Never commit real secrets to Git**
   ```json
   // ? BAD - Real secret in Git
   {
     "Secrets": {
       "JWT-Secret": "actual-production-secret-12345"
     }
   }
   ```

2. **Never use local config in production**
   ```json
   // ? BAD in appsettings.Production.json
   {
     "UseKeyVault": false  // NEVER do this
   }
   ```

3. **Never disable Key Vault in deployed environments**
   - Azure App Service: Always use Key Vault
   - Azure Container Apps: Always use Key Vault
   - Production VMs: Always use Key Vault

---

## Troubleshooting

### Issue: "Failed to retrieve secrets from Azure Key Vault"

**Symptoms:**
```
? CRITICAL ERROR: Failed to retrieve secrets from Azure Key Vault.
Error: DefaultAzureCredential failed to retrieve a token...
```

**Solutions:**

1. **Check if you're logged in to Azure CLI:**
   ```bash
   az login
   az account show
   ```

2. **Verify you have access to the Key Vault:**
   ```bash
   az keyvault secret list --vault-name rg-naijashield-dev-key
   ```

3. **For local development, switch to local config:**
   ```json
   // appsettings.Development.json
   {
     "UseKeyVault": false
   }
   ```

---

### Issue: "Secret not configured" when UseKeyVault = false

**Symptoms:**
```
InvalidOperationException: Secrets:OpenAI-Key not configured
```

**Solution:**

Add the missing secret to `appsettings.Development.json`:

```json
{
  "UseKeyVault": false,
  "Secrets": {
    "OpenAI-Key": "your-key-here",  // ?? Add this
    "Cosmos-Connection-String": "...",
    // ... all other secrets
  }
}
```

---

### Issue: "Email service not working"

**Expected Behavior:**

- With `Email-Connection-String`: Sends actual emails
- Without `Email-Connection-String`: Logs to console

**To use logging mode (no emails):**

```json
{
  "Secrets": {
    "Email-Connection-String": ""  // ?? Leave empty
  }
}
```

---

## Environment-Specific Configuration

### Development (Local Machine)

```json
// appsettings.Development.json
{
  "UseKeyVault": false,
  "Secrets": { /* your local secrets */ },
  "Frontend": {
    "BaseUrl": "http://localhost:3000"
  }
}
```

### Staging (Azure App Service)

```json
// appsettings.Staging.json
{
  "UseKeyVault": true,
  "KeyVault": {
    "Uri": "https://rg-naijashield-staging-key.vault.azure.net/"
  },
  "Frontend": {
    "BaseUrl": "https://staging.naijashield.com"
  }
}
```

### Production (Azure App Service)

```json
// appsettings.Production.json
{
  "UseKeyVault": true,
  "KeyVault": {
    "Uri": "https://rg-naijashield-prod-key.vault.azure.net/"
  },
  "Frontend": {
    "BaseUrl": "https://naijashield.com"
  }
}
```

---

## Migration Guide

### Migrating from Key Vault-Only to Dual Mode

If you're updating an existing installation:

1. **Update `Program.cs`** (already done)

2. **Update `appsettings.json`** - add `UseKeyVault`:
   ```json
   {
     "UseKeyVault": true
   }
   ```

3. **Update `appsettings.Development.json`** - add local secrets:
   ```json
   {
     "UseKeyVault": false,
     "Secrets": { /* add your secrets */ }
   }
   ```

4. **Test both modes:**
   ```bash
   # Test Key Vault mode
   dotnet run --environment Production
   
   # Test local mode
   dotnet run --environment Development
   ```

---

## FAQ

**Q: Which mode should I use for local development?**
A: If you have Azure CLI set up, use Key Vault mode. Otherwise, use local config mode.

**Q: Can I use Key Vault mode without Azure CLI?**
A: Yes, but you need to configure authentication differently (e.g., using a service principal).

**Q: Is local config mode secure?**
A: It's fine for development, but **never use it in production**. Always use Key Vault for deployed environments.

**Q: What happens if I forget to set UseKeyVault?**
A: Default is `true`, so the app will try to use Key Vault.

**Q: Can I mix Key Vault and local config?**
A: No, it's one or the other. Choose based on `UseKeyVault` setting.

**Q: Where should I store my `appsettings.Development.json`?**
A: Add it to `.gitignore` so secrets aren't committed to Git.

---

## Summary

### ? Recommended Setup

| Environment | UseKeyVault | Secrets Source | Secure? |
|-------------|-------------|----------------|---------|
| **Local Dev** | `false` | appsettings.Development.json | ?? For dev only |
| **Staging** | `true` | Azure Key Vault | ? Secure |
| **Production** | `true` | Azure Key Vault | ? Secure |

### ?? Security Checklist

- [ ] `UseKeyVault: true` in production
- [ ] `appsettings.Development.json` in `.gitignore`
- [ ] No real secrets committed to Git
- [ ] Strong JWT secret (32+ characters) even in development
- [ ] Email connection string optional (graceful fallback)
- [ ] Detailed error messages for troubleshooting

---

**Last Updated:** April 2025  
**Version:** 2.0  
**Status:** Production Ready with Local Development Support ?
