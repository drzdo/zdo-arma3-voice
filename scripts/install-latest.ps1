# ArmaVoice — download latest release to current directory
# Usage: powershell -ExecutionPolicy Bypass -File install-latest.ps1
#
# Downloads and extracts:
#   ./@arma3_mic/          — Arma 3 mod (copy to Arma 3 directory)
#   ./ArmaVoice.Server.exe — C# server

$repo = "drzdo/arma3-mic"
$ErrorActionPreference = "Stop"

Write-Host "Fetching latest release from $repo..."
$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
$tag = $release.tag_name
Write-Host "Latest release: $tag"

# Find asset URLs
$modAsset = $release.assets | Where-Object { $_.name -eq "arma3_mic_mod.zip" }
$serverAsset = $release.assets | Where-Object { $_.name -eq "ArmaVoice.Server.exe" }

if (-not $modAsset) { Write-Error "arma3_mic_mod.zip not found in release $tag"; exit 1 }
if (-not $serverAsset) { Write-Error "ArmaVoice.Server.exe not found in release $tag"; exit 1 }

# Download mod
Write-Host "Downloading mod..."
Invoke-WebRequest -Uri $modAsset.browser_download_url -OutFile "arma3_mic_mod.zip"

# Download server
Write-Host "Downloading server..."
Invoke-WebRequest -Uri $serverAsset.browser_download_url -OutFile "ArmaVoice.Server.exe"

# Extract mod (contains @arma3_mic/ folder)
Write-Host "Extracting mod..."
if (Test-Path "@arma3_mic") { Remove-Item -Recurse -Force "@arma3_mic" }
Expand-Archive "arma3_mic_mod.zip" -DestinationPath .
Remove-Item "arma3_mic_mod.zip"

Write-Host ""
Write-Host "Done! Installed ArmaVoice $tag"
Write-Host "  @arma3_mic/          — copy this folder to your Arma 3 directory"
Write-Host "  ArmaVoice.Server.exe — run with: .\ArmaVoice.Server.exe --config config.yaml"
