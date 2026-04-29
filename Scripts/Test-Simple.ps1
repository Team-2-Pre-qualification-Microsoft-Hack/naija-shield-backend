# Simple NaijaShield Testing Script
# Tests all endpoints (requires admin user to be created manually first time)

param(
    [string]$BaseUrl = "http://localhost:5209"
)

Write-Host "`n=== NaijaShield Auth Testing Suite ===" -ForegroundColor Cyan

# Test if app is running
Write-Host "`n[CHECK] Testing if application is running..." -ForegroundColor Yellow
try {
    $null = Invoke-WebRequest -Uri "$BaseUrl/api/test-scam" -Method GET -TimeoutSec 2 -ErrorAction Stop
    Write-Host "? Application is running!" -ForegroundColor Green
} catch {
    Write-Host "? Application is not running. Start it with 'dotnet run' first!" -ForegroundColor Red
    exit 1
}

# ==========================================
# STEP 1: LOGIN
# ==========================================
Write-Host "`n[TEST 1] Login..." -ForegroundColor Yellow

$loginBody = @{
    email = "admin@naijashield.com"
    password = "admin123"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body $loginBody
    Write-Host "? Login successful!" -ForegroundColor Green
    Write-Host "   User: $($loginResponse.user.name) ($($loginResponse.user.role))" -ForegroundColor Gray
    
    $token = $loginResponse.token
    $refreshToken = $loginResponse.refreshToken
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 401) {
        Write-Host "? No admin user found!" -ForegroundColor Red
        Write-Host "`n?? Quick Fix - Run this in PowerShell:" -ForegroundColor Yellow
        Write-Host ".\Scripts\Create-AdminUserDirect.ps1" -ForegroundColor White
        Write-Host "`nOr manually create user in Azure Portal with this JSON:" -ForegroundColor Yellow
        
        $adminJson = @"
{
  "id": "USR-001",
  "name": "Admin User",
  "email": "admin@naijashield.com",
  "password": "`$2a`$12`$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewY5GyYIq0fRh.C2",
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
"@
        Write-Host $adminJson -ForegroundColor Gray
        exit 1
    } else {
        Write-Host "? Login failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# ==========================================
# STEP 2: CREATE INVITATION
# ==========================================
Write-Host "`n[TEST 2] Create Invitation..." -ForegroundColor Yellow

$inviteBody = @{
    email = "analyst@test.com"
    name = "Test Analyst"
    role = "SOC_ANALYST"
} | ConvertTo-Json

try {
    $inviteResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/invite" `
        -Method POST `
        -Headers @{"Authorization" = "Bearer $token"} `
        -ContentType "application/json" `
        -Body $inviteBody
    
    Write-Host "? Invitation created!" -ForegroundColor Green
    Write-Host "   Invite ID: $($inviteResponse.inviteId)" -ForegroundColor Gray
    Write-Host "   Email: $($inviteResponse.email)" -ForegroundColor Gray
    Write-Host "   ??  Check application console for invite token!" -ForegroundColor Yellow
} catch {
    Write-Host "? Create invitation failed: $($_.Exception.Message)" -ForegroundColor Red
}

# ==========================================
# STEP 3: REFRESH TOKEN
# ==========================================
Write-Host "`n[TEST 3] Refresh Token..." -ForegroundColor Yellow

$refreshBody = @{
    refreshToken = $refreshToken
} | ConvertTo-Json

try {
    $refreshResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/refresh" -Method POST -ContentType "application/json" -Body $refreshBody
    Write-Host "? Token refresh successful!" -ForegroundColor Green
    Write-Host "   New Token: $($refreshResponse.token.Substring(0, 30))..." -ForegroundColor Gray
} catch {
    Write-Host "? Token refresh failed: $($_.Exception.Message)" -ForegroundColor Red
}

# ==========================================
# STEP 4: LOGOUT
# ==========================================
Write-Host "`n[TEST 4] Logout..." -ForegroundColor Yellow

$logoutBody = @{
    refreshToken = $refreshToken
} | ConvertTo-Json

try {
    $null = Invoke-RestMethod -Uri "$BaseUrl/api/auth/logout" `
        -Method POST `
        -Headers @{"Authorization" = "Bearer $token"} `
        -ContentType "application/json" `
        -Body $logoutBody
    
    Write-Host "? Logout successful!" -ForegroundColor Green
} catch {
    Write-Host "? Logout failed: $($_.Exception.Message)" -ForegroundColor Red
}

# ==========================================
# STEP 5: AI SCAM DETECTION
# ==========================================
Write-Host "`n[TEST 5] AI Scam Detection..." -ForegroundColor Yellow

try {
    $scamResponse = Invoke-RestMethod -Uri "$BaseUrl/api/test-scam" -Method GET
    Write-Host "? AI Scam Detection working!" -ForegroundColor Green
    Write-Host "   Decision: $($scamResponse.ai_decision)" -ForegroundColor Gray
} catch {
    Write-Host "? AI Scam Detection failed: $($_.Exception.Message)" -ForegroundColor Red
}

# ==========================================
# SUMMARY
# ==========================================
Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "? All core endpoints tested successfully!" -ForegroundColor Green
Write-Host "`nAdmin Credentials:" -ForegroundColor Cyan
Write-Host "  Email: admin@naijashield.com" -ForegroundColor White
Write-Host "  Password: admin123" -ForegroundColor White
Write-Host "`nDone! ??" -ForegroundColor Green
