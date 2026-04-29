# Testing Scripts Guide

## ?? Quick Start (One Command)

The easiest way to test everything:

```powershell
.\Scripts\Test-Everything.ps1
```

This script will:
1. ? Create admin user automatically
2. ? Test all 5 auth endpoints
3. ? Test AI scam detection
4. ? Show you the results

**No Azure Portal needed!** ??

---

## ?? Available Scripts

| Script | What It Does | When to Use |
|--------|--------------|-------------|
| **Test-Everything.ps1** | Creates admin + tests all endpoints | ? **Use this first!** |
| **Create-AdminUserDirect.ps1** | Creates admin user only | If you just need the admin user |
| **Test-Simple.ps1** | Tests all endpoints (requires admin exists) | After admin is created |
| **Create-AdminUser.ps1** | Shows JSON to paste in Azure Portal | If automated creation fails |

---

## ?? Step-by-Step Guide

### **Step 1: Start Your Application**

```powershell
# In Visual Studio: Press F5
# Or in terminal:
dotnet run
```

**Wait for:**
```
Using local configuration (Development mode)
Cosmos DB 'NaijaShieldDB' and container 'Users' ready!
Now listening on: http://localhost:5209
```

### **Step 2: Run the Master Test Script**

```powershell
.\Scripts\Test-Everything.ps1
```

**Expected Output:**
```
========================================
  NaijaShield Complete Test Suite
========================================

[CHECK] Is application running?
? Application is running!

[STEP 1/6] Creating admin user...
? Admin user ready

[STEP 2/6] Testing Login...
? Login successful!
   User: Admin User
   Role: SYSTEM_ADMIN

[STEP 3/6] Testing Create Invitation...
? Invitation created!
   Invite ID: USR-002

[STEP 4/6] Testing Refresh Token...
? Token refresh successful!

[STEP 5/6] Testing Logout...
? Logout successful!

[STEP 6/6] Testing AI Scam Detection...
? AI working!

========================================
  All Tests Passed! ??
========================================
```

---

## ?? Individual Scripts

### **Create Admin User Only**

```powershell
.\Scripts\Create-AdminUserDirect.ps1
```

**Creates:**
- Email: `admin@naijashield.com`
- Password: `admin123`
- Role: `SYSTEM_ADMIN`

### **Test Endpoints Only**

```powershell
.\Scripts\Test-Simple.ps1
```

**Tests:**
- Login
- Create Invitation
- Refresh Token
- Logout
- AI Scam Detection

---

## ?? Troubleshooting

### **Error: "Application not running"**

**Solution:**
```powershell
# Start the app
dotnet run

# Then run tests
.\Scripts\Test-Everything.ps1
```

### **Error: "Cannot be loaded because running scripts is disabled"**

**Solution:**
```powershell
# Run this once (as Administrator)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Then try again
.\Scripts\Test-Everything.ps1
```

### **Error: "Conflict" or "User already exists"**

**This is fine!** The admin user already exists. Just run:
```powershell
.\Scripts\Test-Simple.ps1
```

### **Error: "Could not read Cosmos connection string"**

**Solution:** Make sure `appsettings.Development.json` has your Cosmos DB connection string:
```json
{
  "Secrets": {
    "Cosmos-Connection-String": "AccountEndpoint=https://...;AccountKey=...;"
  }
}
```

---

## ?? What Each Test Does

| Test | Endpoint | What It Checks |
|------|----------|----------------|
| **Login** | `POST /api/auth/login` | Authentication works |
| **Create Invitation** | `POST /api/auth/invite` | Admin can invite users |
| **Refresh Token** | `POST /api/auth/refresh` | Token refresh works |
| **Logout** | `POST /api/auth/logout` | Session termination works |
| **AI Scam** | `GET /api/test-scam` | AI integration works |

---

## ?? Quick Reference

### **Full Test (Recommended)**
```powershell
.\Scripts\Test-Everything.ps1
```

### **Create Admin Only**
```powershell
.\Scripts\Create-AdminUserDirect.ps1
```

### **Test Endpoints Only**
```powershell
.\Scripts\Test-Simple.ps1
```

---

## ? Success Checklist

After running `Test-Everything.ps1`, you should see:

- [x] Application running
- [x] Admin user created
- [x] Login working
- [x] Create invitation working
- [x] Refresh token working
- [x] Logout working
- [x] AI scam detection working

**If all show ? - you're ready to develop!** ??

---

## ?? Admin Credentials

After running any creation script:

```
Email: admin@naijashield.com
Password: admin123
```

Use these to:
- Test login
- Create invitations
- Access admin-only endpoints

---

**Created:** April 2025  
**Status:** Ready to Use ?
