# Publishes self-contained, single-file Windows builds to ./dist (no .NET install needed
# to run them). Excludes the web app, which is run from source with Node.
#
#   ./publish.ps1                 # win-x64, Release
#   ./publish.ps1 -Runtime win-arm64
param(
    [string]$Runtime = "win-x64",
    [string]$Config  = "Release"
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$dist = Join-Path $root "dist"
Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue

$common = @(
    "-c", $Config,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true"
)

Write-Host "Publishing overlay…" -ForegroundColor Cyan
dotnet publish (Join-Path $root "src/Fh6.Telemetry.Overlay/Fh6.Telemetry.Overlay.csproj") @common -o (Join-Path $dist "overlay")

Write-Host "Publishing CLI…" -ForegroundColor Cyan
dotnet publish (Join-Path $root "src/Fh6.Telemetry.Cli/Fh6.Telemetry.Cli.csproj") @common -o (Join-Path $dist "cli")

Write-Host "`nDone. Artifacts in $dist" -ForegroundColor Green
Get-ChildItem -Recurse $dist -Include *.exe | ForEach-Object { "  {0,-10} {1:N1} MB" -f $_.Name, ($_.Length/1MB) }
