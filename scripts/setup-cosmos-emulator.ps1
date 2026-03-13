# Setup Cosmos DB Emulator Database and Container
# This script creates the required database and container in the local Cosmos DB Emulator

$ErrorActionPreference = "Stop"

# Configuration
$endpoint = "https://localhost:8081"
$key = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
$databaseName = "TournamentDb"
$containerName = "Tournaments"
$partitionKeyPath = "/pk"

Write-Host "`n=== Cosmos DB Emulator Setup ===" -ForegroundColor Cyan
Write-Host "Database: $databaseName" -ForegroundColor White
Write-Host "Container: $containerName" -ForegroundColor White
Write-Host "Partition Key: $partitionKeyPath`n" -ForegroundColor White

# Function to make Cosmos DB REST API calls
function Invoke-CosmosDbRequest {
    param(
        [string]$Method,
        [string]$ResourceType,
        [string]$ResourceLink,
        [string]$Body = ""
    )
    
    $verb = $Method.ToLowerInvariant()
    $resourceType = $ResourceType.ToLowerInvariant()
    $resourceLink = $ResourceLink.ToLowerInvariant()
    $date = [DateTime]::UtcNow.ToString("r")
    
    $keyBytes = [Convert]::FromBase64String($key)
    
    $payload = "$verb`n$resourceType`n$resourceLink`n$($date.ToLowerInvariant())`n`n"
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = $keyBytes
    $hashBytes = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))
    $signature = [Convert]::ToBase64String($hashBytes)
    
    $authHeader = "type=master&ver=1.0&sig=$signature"
    
    $headers = @{
        "authorization" = [System.Web.HttpUtility]::UrlEncode($authHeader)
        "x-ms-date" = $date
        "x-ms-version" = "2018-12-31"
        "Content-Type" = "application/json"
    }
    
    $uri = "$endpoint/$ResourceLink"
    if ($Method -eq "POST" -and $ResourceType -eq "dbs") {
        $uri = "$endpoint/dbs"
    } elseif ($Method -eq "POST" -and $ResourceType -eq "colls") {
        $uri = "$endpoint/$ResourceLink/colls"
    }
    
    try {
        if ($Body) {
            $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -Body $Body -SkipCertificateCheck
        } else {
            $response = Invoke-RestMethod -Uri $uri -Method $Method -Headers $headers -SkipCertificateCheck
        }
        return $response
    } catch {
        if ($_.Exception.Response.StatusCode -eq 409) {
            return $null  # Resource already exists
        }
        throw
    }
}

Add-Type -AssemblyName System.Web

# Step 1: Check if database exists, create if not
Write-Host "Checking database '$databaseName'..." -NoNewline
try {
    $dbBody = @{
        id = $databaseName
    } | ConvertTo-Json
    
    $result = Invoke-CosmosDbRequest -Method "POST" -ResourceType "dbs" -ResourceLink "" -Body $dbBody
    
    if ($null -eq $result) {
        Write-Host " already exists ✓" -ForegroundColor Yellow
    } else {
        Write-Host " created ✓" -ForegroundColor Green
    }
} catch {
    Write-Host " error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Check if container exists, create if not
Write-Host "Checking container '$containerName'..." -NoNewline
try {
    $containerBody = @{
        id = $containerName
        partitionKey = @{
            paths = @($partitionKeyPath)
            kind = "Hash"
        }
    } | ConvertTo-Json -Depth 10
    
    $result = Invoke-CosmosDbRequest -Method "POST" -ResourceType "colls" -ResourceLink "dbs/$databaseName" -Body $containerBody
    
    if ($null -eq $result) {
        Write-Host " already exists ✓" -ForegroundColor Yellow
    } else {
        Write-Host " created ✓" -ForegroundColor Green
    }
} catch {
    Write-Host " error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`n✓ Setup complete!" -ForegroundColor Green
Write-Host "`nYou can view your database at:" -ForegroundColor Cyan
Write-Host "https://localhost:8081/_explorer/index.html" -ForegroundColor White
Write-Host "`nYour Azure Functions can now connect to the emulator.`n" -ForegroundColor Green
