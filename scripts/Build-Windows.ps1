param([switch]$SkipChecks)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not $SkipChecks) { dotnet run --project tests/HfcSetupAssistant.Checks/HfcSetupAssistant.Checks.csproj -c Release -p:NuGetAudit=false; if ($LASTEXITCODE -ne 0) { throw 'Core validation checks failed.' } }
    dotnet build src/HfcSetupAssistant.Desktop/HfcSetupAssistant.Desktop.csproj -c Release -p:UseSharedCompilation=false -p:NuGetAudit=false; if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
    dotnet publish src/HfcSetupAssistant.Desktop/HfcSetupAssistant.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:NuGetAudit=false -o artifacts/win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
    Write-Host "Build complete: $root\artifacts\win-x64\HFC Setup Assistant.exe"
} finally { Pop-Location }
