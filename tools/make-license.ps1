# Generates a license key for a customer. Keep this file private.
$secret = "YOUR-SECRET-HERE"
$name = Read-Host "Customer name"

function B64U($bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$payload = [Text.Encoding]::UTF8.GetBytes($name)
$hmac = New-Object System.Security.Cryptography.HMACSHA256 (,[Text.Encoding]::UTF8.GetBytes($secret))
$sig = $hmac.ComputeHash($payload)

Write-Host ""
Write-Host "License key:" -ForegroundColor Green
Write-Host "$(B64U $payload).$(B64U $sig)"