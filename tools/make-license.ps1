# Generates a license key tied to one PC. Keep this file private.
$secret = "YOUR-SECRET-HERE"
$name = Read-Host "Customer name (no | characters)"
$machineId = Read-Host "Customer machine ID (from their License page)"

function B64U($bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$payload = [Text.Encoding]::UTF8.GetBytes("$name|$machineId")
$hmac = New-Object System.Security.Cryptography.HMACSHA256 (,[Text.Encoding]::UTF8.GetBytes($secret))
$sig = $hmac.ComputeHash($payload)

Write-Host ""
Write-Host "License key:" -ForegroundColor Green
Write-Host "$(B64U $payload).$(B64U $sig)"