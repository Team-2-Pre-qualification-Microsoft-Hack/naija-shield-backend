# ? Configuration Complete - Ready to Test!

## ?? What's Been Configured

Your NaijaShield backend is now fully configured with all the connection strings from your team!

---

## ? Configuration Summary

### **appsettings.Development.json** (Local Development)
```json
{
  "UseKeyVault": false,  // ? Using local configuration mode
  "Secrets": {
    "OpenAI-Key": "? Configured",
    "Cosmos-Connection-String": "? Configured (db-naijashield-dev)",
    "Search-Key": "? Configured",
    "SignalR-Connection-String": "? Configured (sig-naijashield-dev)",
    "JWT-Secret": "? Configured (local dev secret)",
    "Email-Connection-String": "?? Empty (will use logging mode)"
  }
}
```

### **appsettings.json** (Production)
```json
{
  "UseKeyVault": true,  // ? Production will use Key Vault
  "KeyVault": {
    "Uri": "https://kv-naijashield-dev.vault.azure.net/"  // ? Updated!
  }
}
```

---

## ?? Next Steps

### **Step 1: Start the Application**

```powershell
dotnet run
```

**Expected Output:**
```
??  Using local configuration (Development mode)
??  DO NOT use this mode in production!
Local configuration loaded successfully.
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

? If you see this, **you're ready to go!**

---

### **Step 2: Create Initial Admin User**

Before you can test the auth endpoints, you need an admin user.

**Option 1: Using the Script** (Recommended)
```powershell
# The script will prompt you for details
dotnet run --project Scripts/CreateAdminUser.cs
```

**Option 2: Manually in Azure Portal**
1. Go to Azure Portal ? Cosmos DB ? db-naijashield-dev ? Users container
2. Create a new item with this JSON (generate password hash first):

```json
{
  "id": "USR-001",
  "name": "Admin User",
  "email": "admin@test.com",
  "password": "$2a$12$YOUR_BCRYPT_HASH_HERE",
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

**Generate BCrypt Hash:**
```powershell
# In PowerShell or create a simple C# program
# Password: Test123!
# Hash: Use BCrypt.Net.BCrypt.HashPassword("Test123!", 12)
```

---

### **Step 3: Test Authentication Endpoints**

#### **Test 1: Login**
```powershell
curl -X POST https://localhost:7000/api/auth/login `
  -H "Content-Type: application/json" `
  -k `
  -d '{\"email\":\"admin@test.com\",\"password\":\"Test123!\"}'
```

**Expected Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "user": {
    "id": "USR-001",
    "name": "Admin User",
    "email": "admin@test.com",
    "role": "SYSTEM_ADMIN",
    "organisation": "NaijaShield"
  }
}
```

#### **Test 2: Create Invitation**
```powershell
# Replace TOKEN with your actual token from Test 1
$TOKEN = "your-token-here"

curl -X POST https://localhost:7000/api/auth/invite `
  -H "Authorization: Bearer $TOKEN" `
  -H "Content-Type: application/json" `
  -k `
  -d '{\"email\":\"analyst@test.com\",\"name\":\"Test Analyst\",\"role\":\"SOC_ANALYST\"}'
```

**Expected Response:**
```json
{
  "inviteId": "USR-002",
  "email": "analyst@test.com",
  "name": "Test Analyst",
  "role": "SOC_ANALYST",
  "expiresAt": "2025-04-29T10:00:00Z",
  "status": "Pending"
}
```

**Console Output:**
```
=== INVITATION EMAIL ===
To: analyst@test.com
Name: Test Analyst
Invite Link: http://localhost:3000/accept-invite?token=abc123xyz
Token: abc123xyz
========================
Email service not configured. Invitation details logged above.
```

#### **Test 3: Accept Invitation**
```powershell
# Use the token from console logs above
curl -X POST https://localhost:7000/api/auth/invite/accept `
  -H "Content-Type: application/json" `
  -k `
  -d '{\"inviteToken\":\"abc123xyz\",\"password\":\"Analyst123!\",\"confirmPassword\":\"Analyst123!\"}'
```

**Expected Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBh...",
  "user": {
    "id": "USR-002",
    "name": "Test Analyst",
    "email": "analyst@test.com",
    "role": "SOC_ANALYST",
    "organisation": "NaijaShield"
  }
}
```

---

## ?? Available Endpoints

| Endpoint | Method | Auth Required | Description |
|----------|--------|---------------|-------------|
| `/api/auth/login` | POST | ? No | User login |
| `/api/auth/invite` | POST | ? Yes (Admin) | Create invitation |
| `/api/auth/invite/accept` | POST | ? No | Accept invitation |
| `/api/auth/refresh` | POST | ? No | Refresh token |
| `/api/auth/logout` | POST | ? Yes | Logout user |
| `/api/test-scam` | GET | ? No | Test AI scam detection |

---

## ?? Email Service Note

Your email connection string is **empty**, which means:

- ? **Invitations will be created** successfully
- ? **Invite tokens will be generated**
- ?? **Invitation details will be logged to console** instead of emailed
- ? **You can copy the token from logs** to test acceptance

**This is perfect for testing!** You don't need to check email - just copy the token from your console.

---

## ?? Security Reminders

1. ? **appsettings.Development.json is in .gitignore**
2. ? **DO NOT commit this file** to Git
3. ?? **These are development secrets** - not for production
4. ?? **Production uses Key Vault** (already configured in appsettings.json)

**Verify:**
```powershell
git status
```

You should **NOT** see `appsettings.Development.json` in the list.

---

## ?? Troubleshooting

### Issue: "Format of the initialization string does not conform..."

**Solution:** Make sure the connection string is exactly as received (no extra spaces or line breaks)

### Issue: "Email service not configured"

**Solution:** This is expected! Email-Connection-String is empty. Invitation details will be in console logs.

### Issue: App won't start

**Solution:** Check console output for specific errors. Most likely:
- Cosmos DB connection string format
- JWT Secret too short (needs 32+ characters)

---

## ? You're Ready!

Your configuration is complete. You can now:

1. ? Start the application: `dotnet run`
2. ? Create admin user
3. ? Test all authentication endpoints
4. ? Develop authentication features
5. ? Test with your frontend team

---

**Happy Coding! ??**

*Last Updated: April 2025*
*Status: Configuration Complete ?*
