# Deploy the NaijaShield reputation-seeder Logic App to Azure.
# Prerequisites: az CLI installed + logged in (az login)
#
# Usage: .\azure\deploy-reputation-seeder.ps1

$ErrorActionPreference = "Stop"

$ResourceGroup  = "rg-naijashield-dev"
$Location       = "swedencentral"
$ApiBaseUrl     = "https://api-naijashield-dev-a5ggd0exe2dmccf2.eastus-01.azurewebsites.net"
$ScammerNumber  = "+2348099000000"
$TemplatePath   = Join-Path $PSScriptRoot "logicapp-reputation-seeder.json"

Write-Host "[1/4] Ensuring resource group '$ResourceGroup' exists..." -ForegroundColor Cyan
az group create --name $ResourceGroup --location $Location --output none

Write-Host "[2/4] Deploying Logic App ARM template..." -ForegroundColor Cyan
$DeployOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $TemplatePath `
    --parameters apiBaseUrl=$ApiBaseUrl scammerNumber=$ScammerNumber `
    --output json | ConvertFrom-Json

$LogicAppUrl = $DeployOutput.properties.outputs.logicAppUrl.value
Write-Host "Logic App deployed OK" -ForegroundColor Green
Write-Host "Trigger URL: $LogicAppUrl"

Write-Host "[3/4] Triggering Logic App (seeds 8 incidents + queries reputation)..." -ForegroundColor Cyan
try {
    $Response = Invoke-WebRequest -Method POST -Uri $LogicAppUrl -UseBasicParsing
    Write-Host "Triggered OK (HTTP $($Response.StatusCode))" -ForegroundColor Green
} catch {
    $Code = $_.Exception.Response.StatusCode.value__
    if ($Code -eq 202) {
        Write-Host "Triggered OK (HTTP 202 Accepted)" -ForegroundColor Green
    } else {
        Write-Host "Trigger returned HTTP $Code - check Logic App run history in Portal" -ForegroundColor Red
        exit 1
    }
}

Write-Host "[4/4] Waiting 20 s for seed calls to complete..." -ForegroundColor Cyan
Start-Sleep -Seconds 20

$ReputationUrl = "$ApiBaseUrl/api/numbers/%2B2348099000000/reputation"
Write-Host "Querying reputation for $ScammerNumber..." -ForegroundColor Cyan
try {
    $Rep = Invoke-RestMethod -Uri $ReputationUrl -Method GET
    Write-Host ""
    Write-Host "Reputation result:" -ForegroundColor Green
    Write-Host "  Score:           $($Rep.reputationScore)"
    Write-Host "  Verdict:         $($Rep.verdict)"
    Write-Host "  Total incidents: $($Rep.totalIncidents)"
    Write-Host "  Blocked:         $($Rep.breakdown.blocked)"
    Write-Host "  Monitoring:      $($Rep.breakdown.monitoring)"
} catch {
    Write-Host "Could not fetch reputation yet - the run may still be in progress." -ForegroundColor Yellow
    Write-Host "Retry: Invoke-RestMethod `"$ReputationUrl`" | ConvertTo-Json -Depth 5"
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
