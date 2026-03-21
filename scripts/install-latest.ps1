# ArmaVoice — download latest release to current directory
# Usage: powershell -ExecutionPolicy Bypass -File install-latest.ps1
#
# Downloads and extracts:
#   ./@arma3_mic/  — Arma 3 mod (copy to Arma 3 directory)
#   ./server/      — C# server (exe + commands + functions + config example)

$repo = "drzdo/arma3-mic"
$ErrorActionPreference = "Stop"

Write-Host "Fetching latest release from $repo..."
$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
$tag = $release.tag_name
Write-Host "Latest release: $tag"

# Find asset URLs
$modAsset = $release.assets | Where-Object { $_.name -eq "arma3_mic_mod.zip" }
$serverAsset = $release.assets | Where-Object { $_.name -eq "arma3_mic_server.zip" }

if (-not $modAsset) { Write-Error "arma3_mic_mod.zip not found in release $tag"; exit 1 }
if (-not $serverAsset) { Write-Error "arma3_mic_server.zip not found in release $tag"; exit 1 }

# Download mod
Write-Host "Downloading mod..."
Invoke-WebRequest -Uri $modAsset.browser_download_url -OutFile "arma3_mic_mod.zip"

# Download server
Write-Host "Downloading server..."
Invoke-WebRequest -Uri $serverAsset.browser_download_url -OutFile "arma3_mic_server.zip"

# Extract mod
Write-Host "Extracting mod..."
if (Test-Path "@arma3_mic") { Remove-Item -Recurse -Force "@arma3_mic" }
Expand-Archive "arma3_mic_mod.zip" -DestinationPath .
Remove-Item "arma3_mic_mod.zip"

# Extract server (flatten nested folder from artifact)
Write-Host "Extracting server..."
if (Test-Path "server") { Remove-Item -Recurse -Force "server" }
Expand-Archive "arma3_mic_server.zip" -DestinationPath "server_tmp"
# Move contents of the inner folder up
$inner = Get-ChildItem "server_tmp" -Directory | Select-Object -First 1
if ($inner) {
    Move-Item -LiteralPath $inner.FullName -Destination "server"
} else {
    Rename-Item "server_tmp" "server"
}
if (Test-Path "server_tmp") { Remove-Item -Recurse -Force "server_tmp" }
Remove-Item "arma3_mic_server.zip"

Write-Host ""
Write-Host "Done! Installed ArmaVoice $tag"
Write-Host "  @arma3_mic/ — copy to your Arma 3 directory"
Write-Host "  server/     — copy config.yaml.example to config.yaml, edit it, then run:"
Write-Host "               .\server\ArmaVoice.Server.exe --config .\server\config.yaml"
