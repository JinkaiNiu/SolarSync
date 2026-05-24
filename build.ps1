$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Building SunriseThemeSwitcher..." -ForegroundColor Cyan

# Check if .NET SDK is available
try {
    dotnet --list-sdks 2>&1 | Out-Null
} catch {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8 SDK from:" -ForegroundColor Red
    Write-Host "  https://dotnet.microsoft.com/en-us/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

# Build in Release mode
Write-Host "Step 1: Restoring packages..." -ForegroundColor Green
dotnet restore "$projectDir\SunriseThemeSwitcher.csproj"
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host "Step 2: Building..." -ForegroundColor Green
dotnet build "$projectDir\SunriseThemeSwitcher.csproj" -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit 1 }

Write-Host ""
Write-Host "Build succeeded!" -ForegroundColor Green
Write-Host "Output: $projectDir\bin\Release\net8.0-windows\SunriseThemeSwitcher.dll"

Write-Host ""
Write-Host "To publish as a single-file EXE, run:" -ForegroundColor Cyan
Write-Host "  dotnet publish `"$projectDir\SunriseThemeSwitcher.csproj`" -c Release -r win-x64 --self-contained true" -ForegroundColor Yellow
Write-Host ""
Write-Host "Or use the publish profile in the .csproj (already configured)."
