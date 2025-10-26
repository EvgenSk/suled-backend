# Deployment Script for Suled Backend
# Usage: .\scripts\deploy.ps1 -Environment dev|prod

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('dev', 'prod')]
    [string]$Environment
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Deploying Suled Backend to $Environment environment..." -ForegroundColor Cyan

# Configuration
$ResourceGroupName = if ($Environment -eq 'dev') { "suled-rg-dev" } else { "suled-rg-prod" }
$FunctionAppName = if ($Environment -eq 'dev') { "suled-functions-dev" } else { "suled-functions" }
$Location = "eastus"

# Step 1: Build and test
Write-Host "`n📦 Building solution..." -ForegroundColor Yellow
dotnet restore
dotnet build --configuration Release

Write-Host "`n🧪 Running tests..." -ForegroundColor Yellow
dotnet test --configuration Release --no-build

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Tests failed. Aborting deployment." -ForegroundColor Red
    exit 1
}

# Step 2: Deploy infrastructure
Write-Host "`n🏗️  Deploying infrastructure..." -ForegroundColor Yellow
az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file ./infra/main.bicep `
    --parameters ./infra/main.bicepparam `
    --parameters environment=$Environment

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Infrastructure deployment failed." -ForegroundColor Red
    exit 1
}

# Step 3: Publish Functions
Write-Host "`n📤 Publishing Azure Functions..." -ForegroundColor Yellow
Push-Location src/SuledFunctions
func azure functionapp publish $FunctionAppName
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Function deployment failed." -ForegroundColor Red
    exit 1
}

# Step 4: Verify deployment
Write-Host "`n✅ Verifying deployment..." -ForegroundColor Yellow
$FunctionUrl = az functionapp show --name $FunctionAppName --resource-group $ResourceGroupName --query "defaultHostName" -o tsv
Write-Host "Function App URL: https://$FunctionUrl" -ForegroundColor Green

Write-Host "`n🎉 Deployment to $Environment completed successfully!" -ForegroundColor Green
Write-Host "   Function App: $FunctionAppName"
Write-Host "   Resource Group: $ResourceGroupName"
Write-Host "   URL: https://$FunctionUrl"
