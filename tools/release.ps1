# Builds, signs, and writes version.json for one release. Run from the project folder.
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Notes = "Improvements and fixes."
)

$ErrorActionPreference = "Stop"
$repo = "andrei-bytEE/nexus-optimizer"

# 1. Build the single-file exe
dotnet publish NexusOptimizer.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version -o publish | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
$exe = Join-Path (Get-Location) "publish\NEXUS-OPTIMIZER.exe"

# 2. Sign it with the private key
$rsa = [System.Security.Cryptography.RSA]::Create()
$rsa.FromXmlString([IO.File]::ReadAllText((Join-Path (Get-Location) "keys\private.xml")))
$bytes = [IO.File]::ReadAllBytes($exe)
$sig = $rsa.SignData($bytes, [System.Security.Cryptography.HashAlgorithmName]::SHA256,
                     [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

# 3. Write version.json
$info = [ordered]@{
    version   = $Version
    url       = "https://github.com/$repo/releases/download/v$Version/NEXUS-OPTIMIZER.exe"
    signature = [Convert]::ToBase64String($sig)
    notes     = $Notes
}
[IO.File]::WriteAllText((Join-Path (Get-Location) "publish\version.json"),
                        ($info | ConvertTo-Json))

Write-Host ""
Write-Host "Built v$Version. Upload these two files to a GitHub release tagged v$Version :" -ForegroundColor Green
Write-Host "  publish\NEXUS-OPTIMIZER.exe"
Write-Host "  publish\version.json"