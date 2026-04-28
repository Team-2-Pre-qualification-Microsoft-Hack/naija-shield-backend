# Direct Admin User Creator using Cosmos DB REST API
# This creates an admin user without needing Azure Portal

Write-Host "`n=== Admin User Creator ===" -ForegroundColor Cyan

# Load configuration
try {
    $appSettings = Get-Content "appsettings.Development.json" | ConvertFrom-Json
    $cosmosConnectionString = $appSettings.Secrets.'Cosmos-Connection-String'
    
    if (!$cosmosConnectionString) {
        Write-Host "❌ Could not read Cosmos connection string from appsettings.Development.json" -ForegroundColor Red
        exit 1
    }
    
    # Parse connection string
    if ($cosmosConnectionString -match 'AccountEndpoint=([^;]+);AccountKey=([^;]+)') {
        $endpoint = $matches[1].TrimEnd('/')
        $key = $matches[2].TrimEnd(';')
    } else {
        Write-Host "❌ Invalid Cosmos DB connection string format" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✅ Configuration loaded" -ForegroundColor Green
    Write-Host "   Endpoint: $endpoint" -ForegroundColor Gray
    
} catch {
    Write-Host "❌ Error loading configuration: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Admin user data
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
    lastActive = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    createdAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    refreshToken = $null
    refreshTokenExpiry = $null
    failedLoginAttempts = 0
    lockoutUntil = $null
    type = "user"
}

Write-Host "`n[STEP 1] Preparing Cosmos DB request..." -ForegroundColor Yellow

# Cosmos DB settings
$databaseId = "NaijaShieldDB"
$containerId = "Users"
$resourceType = "docs"
$resourceLink = "dbs/$databaseId/colls/$containerId"

# Create authorization token
Add-Type -AssemblyName System.Web
$dateTime = [DateTime]::UtcNow.ToString("r")
$verb = "POST"

$keyBytes = [Convert]::FromBase64String($key)
$text = @($verb.ToLowerInvariant() + "`n" + $resourceType.ToLowerInvariant() + "`n" + $resourceLink + "`n" + $dateTime.ToLowerInvariant() + "`n" + "" + "`n")
$body = [Text.Encoding]::UTF8.GetBytes($text)

$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = $keyBytes
$signature = [Convert]::ToBase64String($hmac.ComputeHash($body))
$authToken = [Web.HttpUtility]::UrlEncode("type=master&ver=1.0&sig=$signature")

# Prepare request
$uri = "$endpoint/dbs/$databaseId/colls/$containerId/docs"
$headers = @{
    "Authorization" = $authToken
    "x-ms-date" = $dateTime
    "x-ms-version" = "2018-12-31"
    "Content-Type" = "application/json"
    "x-ms-documentdb-partitionkey" = '["USR-001"]'
}

Write-Host "[STEP 2] Creating admin user in Cosmos DB..." -ForegroundColor Yellow

try {
    $response = Invoke-RestMethod -Uri $uri -Method POST -Headers $headers -Body ($adminUser | ConvertTo-Json -Depth 10)
    
    Write-Host "✅ Admin user created successfully!" -ForegroundColor Green
    Write-Host "`n=== Login Credentials ===" -ForegroundColor Cyan
    Write-Host "Email: admin@naijashield.com" -ForegroundColor White
    Write-Host "Password: admin123" -ForegroundColor White
    
    Write-Host "`n=== Test Login ===" -ForegroundColor Cyan
    Write-Host "Run this command:" -ForegroundColor Yellow
    Write-Host '.\Scripts\Test-Simple.ps1' -ForegroundColor White
    
} catch {
    if ($_.Exception.Message -match "Conflict" -or $_.Exception.Message -match "409") {
        Write-Host "⚠️  Admin user already exists!" -ForegroundColor Yellow
        Write-Host "`n=== Login Credentials ===" -ForegroundColor Cyan
        Write-Host "Email: admin@naijashield.com" -ForegroundColor White
        Write-Host "Password: admin123" -ForegroundColor White
        Write-Host "`nYou can now test login with:" -ForegroundColor Yellow
        Write-Host '.\Scripts\Test-Simple.ps1' -ForegroundColor White
    } else {
        Write-Host "❌ Error creating user: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "`n📝 Fallback: Create user manually in Azure Portal" -ForegroundColor Yellow
        Write-Host "Copy this JSON:" -ForegroundColor Yellow
        Write-Host ($adminUser | ConvertTo-Json -Depth 10) -ForegroundColor Gray
    }
}
