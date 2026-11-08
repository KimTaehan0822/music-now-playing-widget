# Builds dist/ObsNowPlaying-v<version>.zip: a single self-contained exe plus docs and licenses.
param([string]$Version = "0.1.0")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root "dist\ObsNowPlaying"
$zip = Join-Path $root "dist\ObsNowPlaying-v$Version.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\ObsNowPlaying") -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
  -p:DebugType=none -p:Version=$Version -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Copy-Item (Join-Path $root "README.md"), (Join-Path $root "README.ko.md"), (Join-Path $root "README.ja.md"), (Join-Path $root "LICENSE") $out
Copy-Item (Join-Path $root "licenses") $out -Recurse
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $out "*") -DestinationPath $zip
Get-ChildItem $out | Format-Table Name, Length
Write-Host "Created $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"
