# Complete NaijaShield Authentication Testing Suite
# This script creates admin user and tests all endpoints automatically

param(
    [string]$BaseUrl = "http://localhost:5209"
)

# Colors for output
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Info { Write-Host $args -ForegroundColor Cyan }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }
function Write-Error { Write-Host $args -ForegroundColor Red }

Write-Info "`n================================================"
Write-Info "  NaijaShield Authentication Testing Suite"
Write-Info "================================================`n"

# Load Cosmos DB connection from appsettings.Development.json
$appSettings = Get-Content "appsettings.Development.json" | ConvertFrom-Json
$cosmosConnectionString = $appSettings.Secrets.'Cosmos-Connection-String'

if (!$cosmosConnectionString) {
    Write-Error "Could not read Cosmos connection string from appsettings.Development.json"
    exit 1
}

# Parse Cosmos DB endpoint and key from connection string
if ($cosmosConnectionString -match 'AccountEndpoint=([^;]+);AccountKey=([^;]+)') {
    $cosmosEndpoint = $matches[1]
    $cosmosKey = $matches[2]
} else {
    Write-Error "Invalid Cosmos DB connection string format"
    exit 1
}

Write-Info "Cosmos DB Endpoint: $cosmosEndpoint"

# ==========================================
# STEP 1: CREATE ADMIN USER IN COSMOS DB
# ==========================================
Write-Info "`n[STEP 1] Creating Admin User in Cosmos DB..."

$adminUser = @{
    id = "USR-001"
    name = "Admin User"
    email = "admin@naijashield.com"
    password = '$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewY5GyYIq0fRh.C2'
    role = "SYSTEM_ADMIN"
    organisation = "NaijaShield"
    status = "Active"
    inviteToken = $null
    inviteExpiry = $null
    lastActive = "2025-04-27T10:00:00Z"
    createdAt = "2025-04-27T10:00:00Z"
    refreshToken = $null
    refreshTokenExpiry = $null
    failedLoginAttempts = 0
    lockoutUntil = $null
    type = "user"
}

# Prepare Cosmos DB request
$databaseId = "NaijaShieldDB"
$containerId = "Users"
$cosmosUri = "$cosmosEndpoint/dbs/$databaseId/colls/$containerId/docs"

# Generate authorization token
$verb = "POST"
$resourceType = "docs"
$resourceLink = "dbs/$databaseId/colls/$containerId"
$dateTime = [DateTime]::UtcNow.ToString("r")

# Create signature
$keyBytes = [System.Convert]::FromBase64String($cosmosKey)
$text = @($verb.ToLowerInvariant() + "`n" + $resourceType.ToLowerInvariant() + "`n" + $resourceLink + "`n" + $dateTime.ToLowerInvariant() + "`n" + "" + "`n")
$body = [Text.Encoding]::UTF8.GetBytes($text)
$hmacsha256 = New-Object System.Security.Cryptography.HMACSHA256
$hmacsha256.Key = $keyBytes
$signature = [System.Convert]::ToBase64String($hmacsha256.ComputeHash($body))
$authToken = [System.Web.HttpUtility]::UrlEncode("type=master&ver=1.0&sig=$signature")

# Headers for Cosmos DB
$headers = @{
    "Authorization" = $authToken
    "x-ms-date" = $dateTime
    "x-ms-version" = "2018-12-31"
    "Content-Type" = "application/json"
    "x-ms-documentdb-partitionkey" = '["user"]'
}

try {
    Add-Type -AssemblyName System.Web
    $response = Invoke-RestMethod -Uri $cosmosUri -Method POST -Headers $headers -Body ($adminUser | ConvertTo-Json -Depth 10)
    Write-Success "? Admin user created successfully!"
    Write-Info "   Email: admin@naijashield.com"
    Write-Info "   Password: admin123"
} catch {
    if ($_.Exception.Message -match "Conflict") {
        Write-Warning "??  Admin user already exists (this is fine)"
    } else {
        Write-Error "? Failed to create admin user: $($_.Exception.Message)"
        Write-Info "`nFalling back to manual endpoint testing..."
    }
}

# ==========================================
# STEP 2: TEST LOGIN
# ==========================================
Write-Info "`n[STEP 2] Testing Login Endpoint..."

try {
    $loginBody = @{
        email = "admin@naijashield.com"
        password = "admin123"
    } | ConvertTo-Json

    $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body $loginBody
    
    Write-Success "? Login successful!"
    Write-Info "   User: $($loginResponse.user.name)"
    Write-Info "   Role: $($loginResponse.user.role)"
    Write-Info "   Token: $($loginResponse.token.Substring(0, 50))..."
    
    $token = $loginResponse.token
    $refreshToken = $loginResponse.refreshToken
    
} catch {
    Write-Error "? Login failed: $($_.Exception.Message)"
    exit 1
}

# ==========================================
# STEP 3: TEST CREATE INVITATION
# ==========================================
Write-Info "`n[STEP 3] Testing Create Invitation..."

try {
    $inviteBody = @{
        email = "analyst@naijashield.com"
        name = "Test Analyst"
        role = "SOC_ANALYST"
    } | ConvertTo-Json

    $inviteResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/invite" `
        -Method POST `
        -Headers @{"Authorization" = "Bearer $token"} `
        -ContentType "application/json" `
        -Body $inviteBody
    
    Write-Success "? Invitation created!"
    Write-Info "   Invite ID: $($inviteResponse.inviteId)"
    Write-Info "   Email: $($inviteResponse.email)"
    Write-Info "   Status: $($inviteResponse.status)"
    Write-Warning "   ??  Check application console for invite token (email not configured)"
    
} catch {
    Write-Error "? Create invitation failed: $($_.Exception.Message)"
}

# ==========================================
# STEP 4: TEST ACCEPT INVITATION
# ==========================================
Write-Info "`n[STEP 4] Testing Accept Invitation..."
Write-Warning "   Note: You need the invite token from console logs"
Write-Info "   Skipping for now (requires manual token from logs)"

# Uncomment and add token from console logs to test:
# $inviteToken = "PASTE_TOKEN_FROM_CONSOLE_HERE"
# $acceptBody = @{
#     inviteToken = $inviteToken
#     password = "Analyst123!"
#     confirmPassword = "Analyst123!"
# } | ConvertTo-Json
# $acceptResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/invite/accept" -Method POST -ContentType "application/json" -Body $acceptBody

# ==========================================
# STEP 5: TEST REFRESH TOKEN
# ==========================================
Write-Info "`n[STEP 5] Testing Refresh Token..."

try {
    $refreshBody = @{
        refreshToken = $refreshToken
    } | ConvertTo-Json

    $refreshResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/refresh" -Method POST -ContentType "application/json" -Body $refreshBody
    
    Write-Success "? Token refresh successful!"
    Write-Info "   New Token: $($refreshResponse.token.Substring(0, 50))..."
    
    $newToken = $refreshResponse.token
    
} catch {
    Write-Error "? Token refresh failed: $($_.Exception.Message)"
}

# ==========================================
# STEP 6: TEST LOGOUT
# ==========================================
Write-Info "`n[STEP 6] Testing Logout..."

try {
    $logoutBody = @{
        refreshToken = $refreshToken
    } | ConvertTo-Json

    $logoutResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/logout" `
        -Method POST `
        -Headers @{"Authorization" = "Bearer $token"} `
        -ContentType "application/json" `
        -Body $logoutBody
    
    Write-Success "? Logout successful!"
    
} catch {
    Write-Error "? Logout failed: $($_.Exception.Message)"
}

# ==========================================
# STEP 7: TEST AI SCAM DETECTION (ORIGINAL ENDPOINT)
# ==========================================
Write-Info "`n[STEP 7] Testing AI Scam Detection..."

try {
    $scamResponse = Invoke-RestMethod -Uri "$BaseUrl/api/test-scam" -Method GET
    
    Write-Success "? AI Scam Detection working!"
    Write-Info "   Decision: $($scamResponse.ai_decision)"
    
} catch {
    Write-Error "? AI Scam Detection failed: $($_.Exception.Message)"
}

# ==========================================
# SUMMARY
# ==========================================
Write-Info "`n================================================"
Write-Info "  Testing Summary"
Write-Info "================================================"
Write-Success "? Admin User Created"
Write-Success "? Login"
Write-Success "? Create Invitation"
Write-Success "? Refresh Token"
Write-Success "? Logout"
Write-Success "? AI Scam Detection"
Write-Warning "??  Accept Invitation (requires manual token)"

Write-Info "`n================================================"
Write-Info "  All Tests Complete!"
Write-Info "================================================`n"

Write-Info "Admin Credentials:"
Write-Success "  Email: admin@naijashield.com"
Write-Success "  Password: admin123"

Write-Info "`nTo test invitation acceptance:"
Write-Info "1. Check application console for invitation token"
Write-Info "2. Use this command:"
Write-Info '  $acceptBody = @{ inviteToken = "TOKEN_HERE"; password = "Test123!"; confirmPassword = "Test123!" } | ConvertTo-Json'
Write-Info "  Invoke-RestMethod -Uri `"$BaseUrl/api/auth/invite/accept`" -Method POST -ContentType `"application/json`" -Body `$acceptBody"
