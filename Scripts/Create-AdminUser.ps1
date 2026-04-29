# Quick Admin User Creation Script
# This script creates an admin user directly in Cosmos DB

Write-Host "Creating Admin User in Cosmos DB..." -ForegroundColor Cyan

$adminUser = @"
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

Write-Host "`n=== Admin User JSON ===" -ForegroundColor Yellow
Write-Host $adminUser -ForegroundColor Gray

Write-Host "`n=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Go to Azure Portal: https://portal.azure.com" -ForegroundColor White
Write-Host "2. Navigate to: Cosmos DB ? db-naijashield-dev ? Data Explorer ? Users" -ForegroundColor White
Write-Host "3. Click 'New Item'" -ForegroundColor White
Write-Host "4. Paste the JSON above" -ForegroundColor White
Write-Host "5. Click 'Save'" -ForegroundColor White
Write-Host "`n=== Login Credentials ===" -ForegroundColor Cyan
Write-Host "Email: admin@naijashield.com" -ForegroundColor Green
Write-Host "Password: admin123" -ForegroundColor Green

Write-Host "`n=== Test Login Command ===" -ForegroundColor Cyan
Write-Host '$response = Invoke-WebRequest -Uri "http://localhost:5209/api/auth/login" -Method POST -ContentType "application/json" -Body ''{"email":"admin@naijashield.com","password":"admin123"}''' -ForegroundColor Yellow
Write-Host '$response.Content | ConvertFrom-Json' -ForegroundColor Yellow
