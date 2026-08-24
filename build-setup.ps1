# Builds a self-contained single-file publish and wraps it into a Windows installer.
# Usage:  .\build-setup.ps1
$ErrorActionPreference = "Stop"

$root    = $PSScriptRoot
$projDir = Join-Path $root "MyHobbyWarehouse"
$csproj  = Join-Path $projDir "MyHobbyWarehouse.csproj"
$iss     = Join-Path $root "installer.iss"
$iscc    = "C:\Program Files\Inno Setup 6\ISCC.exe"

# ── Read version from csproj ────────────────────────────────────────────────
$csprojText = Get-Content $csproj -Raw
$match = [regex]::Match($csprojText, '<Version>(.*?)</Version>')
$version = if ($match.Success) { $match.Groups[1].Value } else { "1.0" }
Write-Host "Version: $version"

# ── Self-contained, single-file publish (no .NET install required) ───────────
Write-Host "Publishing self-contained single-file for win-x64..."
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if (-not (Test-Path $iscc)) { Write-Error "Inno Setup (ISCC.exe) not found at $iscc"; exit 1 }

# ── Build installer ─────────────────────────────────────────────────────────
Write-Host "Building installer..."
& $iscc /dMyAppVersion=$version $iss
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC failed"; exit 1 }

Write-Host "Done. Installer is in the 'installer' folder."
