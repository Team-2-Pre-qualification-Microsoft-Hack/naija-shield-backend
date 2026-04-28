# Program.cs Migration Guide

## Overview

This guide explains how to safely replace the original `Program.cs` with the authentication-integrated version without conflicts.

---

## Key Changes Made

### 1. **Environment-Based Configuration** ?

**Original:**
```csharp
string keyVaultUri = "https://rg-naijashield-dev-key.vault.azure.net/";
var credential = new DefaultAzureCredential();
var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
```

**New (Team-Approved):**
```csharp
if (builder.Environment.IsProduction())
{
    var keyVaultUri = new Uri("https://kv-naijashield-dev.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}
else
{
    // Load from appsettings.Development.json
}
```

**Why:**
- ? Uses the **new Key Vault URI** (`kv-naijashield-dev`) provided by your team
- ? Automatically detects Production vs Development environment
- ? No manual `UseKeyVault` toggle needed
- ? Aligns with team's infrastructure

---

### 2. **Added Authentication Services** ?

**New Services Added:**
```csharp
// Add controllers
builder.Services.AddControllers();

// Add Cosmos DB client as singleton
builder.Services.AddSingleton(cosmosClient);

// Add authentication services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
```

---

### 3. **JWT Authentication Configuration** ?

```csharp
// Configure JWT Authentication
var key = Encoding.UTF8.GetBytes(jwtSecret);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // ... JWT configuration
});

builder.Services.AddAuthorization();
```

---

### 4. **CORS Policy** ?

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "https://naijashield.com",
            "http://localhost:3000",
            "http://localhost:5173"
        )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials();
    });
});
```

---

### 5. **Middleware Pipeline** ?

```csharp
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRoleAuthorization(); // Custom role-based middleware
app.MapControllers();
```

---

### 6. **Cosmos DB Initialization** ?

**Added:**
```csharp
// Ensure database and container exist
var databaseName = builder.Configuration["Cosmos:DatabaseName"] ?? "NaijaShieldDB";
var userContainerName = builder.Configuration["Cosmos:UserContainerName"] ?? "Users";

try
{
    var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
    await database.Database.CreateContainerIfNotExistsAsync(userContainerName, "/type");
    Console.WriteLine($"Cosmos DB '{databaseName}' and container '{userContainerName}' ready!");
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not initialize Cosmos DB: {ex.Message}");
}
```

---

## How to Apply Changes

### **Option 1: Manual Copy (Recommended)**

1. **Backup current Program.cs:**
```powershell
Copy-Item Program.cs Program.cs.backup
```

2. **Replace with new version:**
```powershell
Copy-Item Program.cs.new Program.cs -Force
Remove-Item Program.cs.new
```

3. **Verify the build:**
```powershell
dotnet build
```

---

### **Option 2: Side-by-Side Comparison**

1. Open both files in Visual Studio
   - `Program.cs` (current)
   - `Program.cs.new` (new version)

2. Use Visual Studio's "Compare Files" feature:
   - Right-click `Program.cs` ? Compare With...
   - Select `Program.cs.new`

3. Review changes and merge manually

---

## What Stays the Same

| Feature | Status |
|---------|--------|
| Azure OpenAI integration | ? Unchanged |
| Semantic Kernel setup | ? Unchanged |
| `/api/test-scam` endpoint | ? Unchanged |
| Cosmos DB client | ? Enhanced (added database initialization) |

---

## What's New

| Feature | Description |
|---------|-------------|
| Authentication Services | User, Token, Auth, Email services |
| JWT Configuration | Token validation and generation |
| CORS Policy | Frontend communication |
| Role-Based Middleware | Permission enforcement |
| Controllers | MVC controller support |
| Environment Detection | Auto-switch Production/Development |

---

## Configuration Files Required

### **appsettings.json** (Production)
```json
{
  "UseKeyVault": true,
  "KeyVault": {
    "Uri": "https://kv-naijashield-dev.vault.azure.net/"
  },
  "Jwt": {
    "Issuer": "NaijaShield",
    "Audience": "NaijaShield"
  },
  "Cosmos": {
    "DatabaseName": "NaijaShieldDB",
    "UserContainerName": "Users"
  }
}
```

### **appsettings.Development.json** (Your Local)
```json
{
  "UseKeyVault": false,
  "Secrets": {
    "OpenAI-Key": "...",
    "Cosmos-Connection-String": "...",
    "Search-Key": "...",
    "SignalR-Connection-String": "...",
    "JWT-Secret": "hackathon-dev-jwt-secret-at-least-32-characters-long-for-local-development",
    "Email-Connection-String": ""
  }
}
```

---

## Testing After Migration

### **Step 1: Build**
```powershell
dotnet build
```

**Expected:** ? Build successful

### **Step 2: Run**
```powershell
dotnet run
```

**Expected Output:**
```
??  Using local configuration (Development mode)
??  DO NOT use this mode in production!
Local configuration loaded successfully.
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
Now listening on: https://localhost:7000
```

### **Step 3: Test Original Endpoint**
```powershell
curl https://localhost:7000/api/test-scam -k
```

**Expected:** ? Should work as before

### **Step 4: Test New Auth Endpoints**
```powershell
curl -X POST https://localhost:7000/api/auth/login -H "Content-Type: application/json" -k -d "{\"email\":\"test@test.com\",\"password\":\"test\"}"
```

**Expected:** Should return 401 (no user yet) or 200 (if admin user exists)

---

## Merge Conflict Prevention

### **Before Pushing:**

1. **Check what you're committing:**
```powershell
git status
git diff Program.cs
```

2. **Verify appsettings.Development.json is NOT staged:**
```powershell
git status | Select-String "appsettings.Development.json"
```

**Expected:** Should return nothing (file is in .gitignore)

3. **Stage only Program.cs and auth files:**
```powershell
git add Program.cs
git add Controllers/AuthController.cs
git add Services/*.cs
git add Models/*.cs
git add Middleware/*.cs
git add Utils/*.cs
```

4. **DO NOT add:**
- ? `appsettings.Development.json` (has your secrets)
- ? `Program.cs.backup` (temporary file)
- ? `Program.cs.new` (temporary file)

---

## Rollback Plan

If something goes wrong:

```powershell
# Restore backup
Copy-Item Program.cs.backup Program.cs -Force

# Rebuild
dotnet build

# Run
dotnet run
```

---

## Summary

| Aspect | Change |
|--------|--------|
| **Key Vault URI** | ? Updated to `kv-naijashield-dev` (team-provided) |
| **Environment Detection** | ? Auto-detects Production/Development |
| **Authentication** | ? Added full auth system |
| **Original Features** | ? All preserved |
| **Conflicts** | ? None expected (additive changes) |

---

## Next Steps

1. ? Review `Program.cs.new`
2. ? Backup current `Program.cs`
3. ? Apply new version
4. ? Test locally
5. ? Push to repository

---

**Created:** April 2025  
**Version:** 1.0  
**Status:** Ready to Apply ?
