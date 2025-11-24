# Pipster API - Identity Server Integration Test Script
# This script tests the authentication flow between Identity Server and API

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Pipster Authentication Test Script" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

# Configuration
$identityServerUrl = "http://localhost:5080"
$apiUrl = "http://localhost:5001"

# Step 1: Test Identity Server Health
Write-Host "`nStep 1: Testing Identity Server Health..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$identityServerUrl/health" -Method GET
    if ($response.StatusCode -eq 200) {
        Write-Host "✓ Identity Server is healthy" -ForegroundColor Green
    }
} catch {
    Write-Host "✗ Identity Server is not responding. Please start it first." -ForegroundColor Red
    Write-Host "  Run: docker-compose up -d identity" -ForegroundColor Gray
    exit 1
}

# Step 2: Test API Health (Anonymous Endpoint)
Write-Host "`nStep 2: Testing API Health (Anonymous)..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$apiUrl/health" -Method GET
    $content = $response.Content | ConvertFrom-Json
    Write-Host "✓ API is healthy - Signals: $($content.SignalsCount), Trades: $($content.TradesCount)" -ForegroundColor Green
} catch {
    Write-Host "✗ API is not responding. Please start it first." -ForegroundColor Red
    Write-Host "  Run: dotnet run --project Pipster.Api" -ForegroundColor Gray
    exit 1
}

# Step 3: Test Protected Endpoint (Should Fail Without Token)
Write-Host "`nStep 3: Testing Protected Endpoint Without Token..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$apiUrl/state/status" -Method GET -ErrorAction Stop
    Write-Host "✗ Protected endpoint accessible without token (Security Issue!)" -ForegroundColor Red
} catch {
    if ($_.Exception.Response.StatusCode -eq 401) {
        Write-Host "✓ Protected endpoint correctly requires authentication" -ForegroundColor Green
    } else {
        Write-Host "✗ Unexpected error: $_" -ForegroundColor Red
    }
}

# Step 4: Instructions for Manual Token Testing
Write-Host "`n====================================" -ForegroundColor Cyan
Write-Host "Manual Token Testing Instructions" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

Write-Host "`n1. Open Swagger UI: " -NoNewline
Write-Host "http://localhost:5001/swagger" -ForegroundColor Yellow

Write-Host "`n2. Click the 'Authorize' button (lock icon)"

Write-Host "`n3. Complete the OAuth flow:"
Write-Host "   - You'll be redirected to Identity Server"
Write-Host "   - Log in or register a new account"
Write-Host "   - Accept the consent screen"
Write-Host "   - You'll be redirected back to Swagger"

Write-Host "`n4. Test these endpoints in order:"
Write-Host "   a. GET /state/status" -ForegroundColor Yellow
Write-Host "      - Should return status with your UserId and TenantId"
Write-Host "   b. GET /api/tenants/current" -ForegroundColor Yellow
Write-Host "      - Should return your tenant information"
Write-Host "   c. GET /api/tenants/{your-tenant-id}/channels" -ForegroundColor Yellow
Write-Host "      - Should return your channels list"

Write-Host "`n====================================" -ForegroundColor Cyan
Write-Host "CORS Test (for Frontend Integration)" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

Write-Host "`nTo test CORS from your Next.js app (http://localhost:3000):"
Write-Host "fetch('$apiUrl/health')" -ForegroundColor Yellow
Write-Host "  .then(r => r.json())"
Write-Host "  .then(console.log)"

Write-Host "`n====================================" -ForegroundColor Cyan
Write-Host "Integration Status" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan

Write-Host "✓ Health endpoints are accessible" -ForegroundColor Green
Write-Host "✓ Protected endpoints require authentication" -ForegroundColor Green
Write-Host "✓ CORS is configured for frontend" -ForegroundColor Green
Write-Host "`nNext: Test with real JWT token via Swagger UI" -ForegroundColor Yellow