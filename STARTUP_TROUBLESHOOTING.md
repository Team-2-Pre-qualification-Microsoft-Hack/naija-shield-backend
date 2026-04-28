# ?? Application Startup Troubleshooting

## Issue: HTTP ERROR 404 on https://localhost:7273/

This means the application isn't running or failed to start. Let's diagnose and fix it.

---

## ? **Step 1: Check if Application is Running**

### In Visual Studio Terminal (or PowerShell):

```powershell
# Check if any .NET process is listening on port 7273
netstat -ano | findstr "7273"
```

**If you see output:** Application is running but endpoints aren't configured
**If you see nothing:** Application isn't running

---

## ? **Step 2: Start the Application Properly**

### Option A: Using Visual Studio

1. **Press F5** or click the green "?? https" button at the top
2. **Wait for console output** showing:
   ```
   Using local configuration (Development mode)
   Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
   Now listening on: https://localhost:7273
   Now listening on: http://localhost:5209
   ```
3. **Check the Output window** for any errors

### Option B: Using Terminal

```powershell
# In the project directory
dotnet run
```

**Expected Output:**
```
Using local configuration (Development mode)
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7273
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5209
```

---

## ? **Step 3: Test the Correct Endpoint**

Your application doesn't have a root endpoint (`/`), so `https://localhost:7273/` will return 404.

### Test These Instead:

#### **1. Test the AI Scam Detection Endpoint** (Original)
```
https://localhost:7273/api/test-scam
```

**Or using PowerShell:**
```powershell
curl https://localhost:7273/api/test-scam -k
```

**Expected Response:**
```json
{
  "status": "Success",
  "test_database_status": "Cosmos DB Client Initialized",
  "ai_decision": "..."
}
```

#### **2. Test Authentication Login** (New)
```powershell
curl -X POST https://localhost:7273/api/auth/login `
  -H "Content-Type: application/json" `
  -k `
  -d '{\"email\":\"admin@test.com\",\"password\":\"Test123!\"}'
```

**Expected Response (if admin user exists):**
```json
{
  "token": "eyJhbGci...",
  "refreshToken": "...",
  "user": { ... }
}
```

**Or (if no admin user yet):**
```json
{
  "error": "INVALID_CREDENTIALS",
  "message": "Invalid email or password"
}
```

---

## ? **Common Errors & Solutions**

### **Error 1: Application Fails to Start**

**Console shows:**
```
InvalidOperationException: Secrets:Cosmos-Connection-String not configured
```

**Solution:** Your `appsettings.Development.json` is missing secrets.

**Fix:**
```json
{
  "UseKeyVault": false,
  "Secrets": {
    "OpenAI-Key": "DH1E30O8ZN5yLEqRh7Um4SvzEJica7OgPv2ZDaIlAPg15bRgPfTWJQQJ99CDACfhMk5XJ3w3AAAAACOGI0vL",
    "Cosmos-Connection-String": "AccountEndpoint=https://db-naijashield-dev.documents.azure.com:443/;AccountKey=TS4JPI9qaBOWCrudfjvPrk94E29obueVzCkgGzkFtqBwWEryXnQ78hYCrrm8K7Q9CqPNoztvGCIVACDbaO6yRw==;",
    "Search-Key": "lxSEAkUrlz9DZ0oyLFFlDSwzm9HG760kBSfL6F3V6CAzSeDKJ0Mx",
    "SignalR-Connection-String": "Endpoint=https://sig-naijashield-dev.service.signalr.net;AccessKey=4dlJHKA0z6mFVgj0KgYFkFx2XbKIdzVhZLFVgq2nF8R0juGjaNHqJQQJ99CDACfhMk5XJ3w3AAAAASRSJosf;Version=1.0;",
    "JWT-Secret": "hackathon-dev-jwt-secret-at-least-32-characters-long-for-local-development",
    "Email-Connection-String": ""
  }
}
```

---

### **Error 2: Port Already in Use**

**Console shows:**
```
Failed to bind to address https://127.0.0.1:7273: address already in use
```

**Solution:** Another process is using port 7273.

**Fix:**
```powershell
# Find the process using port 7273
netstat -ano | findstr "7273"

# Kill the process (replace PID with the actual process ID)
taskkill /F /PID <PID>

# Or restart Visual Studio
```

---

### **Error 3: SSL Certificate Error**

**Browser shows:** "Your connection is not private"

**Solution:** Accept the self-signed certificate or use HTTP.

**Fix:**
- **Browser:** Click "Advanced" ? "Proceed to localhost (unsafe)"
- **Or use HTTP:** `http://localhost:5209/api/test-scam`
- **Or use curl:** Add `-k` flag to skip SSL verification

---

### **Error 4: 404 on Root URL**

**Browser shows:** HTTP ERROR 404 on `https://localhost:7273/`

**Solution:** Your app doesn't have a root endpoint.

**Fix:** Use one of the API endpoints:
- ? `https://localhost:7273/api/test-scam`
- ? `https://localhost:7273/api/auth/login` (POST)
- ? `https://localhost:7273/api/auth/invite` (POST)

---

## ? **Quick Test Script**

Save this as `test-app.ps1` and run it:

```powershell
# test-app.ps1
Write-Host "Testing NaijaShield Backend..." -ForegroundColor Cyan

# Test if app is running
Write-Host "`n1. Checking if app is running on port 7273..." -ForegroundColor Yellow
$port = netstat -ano | findstr "7273"
if ($port) {
    Write-Host "? App is running!" -ForegroundColor Green
} else {
    Write-Host "? App is NOT running. Start it with 'dotnet run' or F5 in Visual Studio" -ForegroundColor Red
    exit
}

# Test /api/test-scam endpoint
Write-Host "`n2. Testing /api/test-scam endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://localhost:7273/api/test-scam" -SkipCertificateCheck -ErrorAction Stop
    Write-Host "? /api/test-scam works!" -ForegroundColor Green
    Write-Host "   Response: $($response.Content.Substring(0, [Math]::Min(100, $response.Content.Length)))..." -ForegroundColor Gray
} catch {
    Write-Host "? /api/test-scam failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test /api/auth/login endpoint
Write-Host "`n3. Testing /api/auth/login endpoint..." -ForegroundColor Yellow
try {
    $body = @{
        email = "test@test.com"
        password = "test123"
    } | ConvertTo-Json

    $response = Invoke-WebRequest -Uri "https://localhost:7273/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $body `
        -SkipCertificateCheck `
        -ErrorAction Stop
    
    Write-Host "? /api/auth/login works!" -ForegroundColor Green
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "? /api/auth/login works! (401 expected - no user yet)" -ForegroundColor Green
    } else {
        Write-Host "? /api/auth/login failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nTesting complete!" -ForegroundColor Cyan
```

**Run it:**
```powershell
.\test-app.ps1
```

---

## ?? **Complete Startup Checklist**

### **Before Starting:**

- [x] `appsettings.Development.json` has all secrets
- [x] Cosmos DB connection string is valid
- [x] JWT Secret is at least 32 characters

### **Start Application:**

```powershell
# Option 1: Visual Studio
# Press F5

# Option 2: Terminal
dotnet run
```

### **Verify Startup:**

```
? Should see: "Using local configuration (Development mode)"
? Should see: "Cosmos DB 'NaijaShieldDB' and container 'Users' ready!"
? Should see: "Now listening on: https://localhost:7273"
```

### **Test Endpoints:**

```powershell
# Test 1: Original endpoint
curl https://localhost:7273/api/test-scam -k

# Test 2: Auth login
curl -X POST https://localhost:7273/api/auth/login -H "Content-Type: application/json" -k -d '{\"email\":\"test@test.com\",\"password\":\"test\"}'
```

---

## ?? **Your Available Endpoints**

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/test-scam` | GET | AI scam detection test |
| `/api/auth/login` | POST | User login |
| `/api/auth/invite` | POST | Create invitation (admin only) |
| `/api/auth/invite/accept` | POST | Accept invitation |
| `/api/auth/refresh` | POST | Refresh token |
| `/api/auth/logout` | POST | Logout |

**Note:** Root URL `/` returns 404 - this is expected!

---

## ?? **Next Steps**

1. **Start the app**: `dotnet run` or F5
2. **Wait for "Now listening on"** message
3. **Test an endpoint**: `https://localhost:7273/api/test-scam`
4. **Don't use root URL** (`/`) - it's not configured

---

**Created:** April 2025  
**Status:** Ready to Use
