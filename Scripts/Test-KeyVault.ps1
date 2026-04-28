# NaijaShield Azure Key Vault Diagnostic Script
# Run this to check your Azure Key Vault connection

Write-Host "=================================" -ForegroundColor Cyan
Write-Host "NaijaShield Key Vault Diagnostic" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check Azure CLI Installation
Write-Host "[1/5] Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --query '\"azure-cli\"' -o tsv 2>$null
    if ($azVersion) {
        Write-Host "  ? Azure CLI installed: v$azVersion" -ForegroundColor Green
    } else {
        Write-Host "  ? Azure CLI not found or not working" -ForegroundColor Red
        Write-Host "     Install from: https://aka.ms/installazurecliwindows" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "  ? Azure CLI not found" -ForegroundColor Red
    Write-Host "     Install from: https://aka.ms/installazurecliwindows" -ForegroundColor Gray
    exit 1
}

Write-Host ""

# Test 2: Check Azure Login Status
Write-Host "[2/5] Checking Azure login status..." -ForegroundColor Yellow
try {
    $accountName = az account show --query "name" -o tsv 2>$null
    if ($accountName) {
        Write-Host "  ? Logged in to Azure" -ForegroundColor Green
        Write-Host "     Account: $accountName" -ForegroundColor Gray
        
        $userEmail = az account show --query "user.name" -o tsv 2>$null
        Write-Host "     User: $userEmail" -ForegroundColor Gray
    } else {
        Write-Host "  ? Not logged in to Azure" -ForegroundColor Red
        Write-Host "     Run: az login" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "  ? Not logged in to Azure" -ForegroundColor Red
    Write-Host "     Run: az login" -ForegroundColor Gray
    exit 1
}

Write-Host ""

# Test 3: Check Key Vault Access
Write-Host "[3/5] Checking Key Vault access..." -ForegroundColor Yellow
$keyVaultName = "rg-naijashield-dev-key"
try {
    $secrets = az keyvault secret list --vault-name $keyVaultName --query "[].name" -o tsv 2>$null
    if ($secrets) {
        Write-Host "  ? Can access Key Vault: $keyVaultName" -ForegroundColor Green
        Write-Host "     Available secrets:" -ForegroundColor Gray
        foreach ($secret in $secrets) {
            Write-Host "       - $secret" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ? Cannot access Key Vault: $keyVaultName" -ForegroundColor Red
        Write-Host "     Possible issues:" -ForegroundColor Gray
        Write-Host "       1. You don't have permission to access the Key Vault" -ForegroundColor Gray
        Write-Host "       2. The Key Vault doesn't exist" -ForegroundColor Gray
        Write-Host "       3. Network connectivity issues" -ForegroundColor Gray
        Write-Host "" -ForegroundColor Gray
        Write-Host "     Ask your administrator to grant access:" -ForegroundColor Gray
        Write-Host "     az keyvault set-policy --name $keyVaultName --upn YOUR_EMAIL --secret-permissions get list" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "  ? Error accessing Key Vault" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Test 4: Check Required Secrets
Write-Host "[4/5] Checking required secrets..." -ForegroundColor Yellow
$requiredSecrets = @(
    "OpenAI-Key",
    "Cosmos-Connection-String",
    "Search-Key",
    "SignalR-Connection-String",
    "JWT-Secret"
)

$missingSecrets = @()
foreach ($secretName in $requiredSecrets) {
    try {
        $secretValue = az keyvault secret show --vault-name $keyVaultName --name $secretName --query "value" -o tsv 2>$null
        if ($secretValue) {
            Write-Host "  ? $secretName" -ForegroundColor Green
        } else {
            Write-Host "  ? $secretName (not found)" -ForegroundColor Red
            $missingSecrets += $secretName
        }
    } catch {
        Write-Host "  ? $secretName (error)" -ForegroundColor Red
        $missingSecrets += $secretName
    }
}

Write-Host ""

# Test 5: Test Cosmos DB Connection String Format
Write-Host "[5/5] Validating Cosmos DB connection string..." -ForegroundColor Yellow
try {
    $cosmosConnString = az keyvault secret show --vault-name $keyVaultName --name "Cosmos-Connection-String" --query "value" -o tsv 2>$null
    if ($cosmosConnString) {
        if ($cosmosConnString -match "^AccountEndpoint=https://") {
            Write-Host "  ? Cosmos DB connection string format is valid" -ForegroundColor Green
            
            # Extract account name
            if ($cosmosConnString -match "AccountEndpoint=https://([^.]+)\.") {
                $accountName = $matches[1]
                Write-Host "     Account: $accountName" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ??  Cosmos DB connection string format looks incorrect" -ForegroundColor Yellow
            Write-Host "     Expected format: AccountEndpoint=https://...;AccountKey=..." -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  ??  Could not validate Cosmos DB connection string" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Diagnostic Summary" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

if ($missingSecrets.Count -eq 0) {
    Write-Host "? All checks passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your environment is configured correctly." -ForegroundColor Green
    Write-Host "You can run your application with:" -ForegroundColor Cyan
    Write-Host "  dotnet run" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "??  Some secrets are missing:" -ForegroundColor Yellow
    foreach ($secret in $missingSecrets) {
        Write-Host "  - $secret" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Contact your administrator to add these secrets to the Key Vault." -ForegroundColor Gray
    Write-Host ""
}

Write-Host "Diagnostic complete." -ForegroundColor Cyan
