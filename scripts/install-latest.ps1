# ZdoArmaVoice - download latest release to current directory
# Usage: powershell -ExecutionPolicy Bypass -File install-latest.ps1 [-ServerOnly]
#
# Downloads and extracts:
#   ./@zdo_arma_voice/  - Arma 3 mod (copy to Arma 3 directory)
#   ./server/      - C# server (exe + server-data + config example)
#
# -ServerOnly: skip mod download, install only the server

param(
    [switch]$ServerOnly
)

$repo = "drzdo/zdo-arma3-voice"
$ErrorActionPreference = "Stop"

Write-Host "Fetching latest release from $repo..."
$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
$tag = $release.tag_name
Write-Host "Latest release: $tag"

# Find asset URLs
$serverAsset = $release.assets | Where-Object { $_.name -eq "zdo_arma_voice_server.zip" }
if (-not $serverAsset) { Write-Error "zdo_arma_voice_server.zip not found in release $tag"; exit 1 }

if (-not $ServerOnly) {
    $modAsset = $release.assets | Where-Object { $_.name -eq "zdo_arma_voice_mod.zip" }
    if (-not $modAsset) { Write-Error "zdo_arma_voice_mod.zip not found in release $tag"; exit 1 }

    # Download and extract mod
    Write-Host "Downloading mod..."
    Invoke-WebRequest -Uri $modAsset.browser_download_url -OutFile "zdo_arma_voice_mod.zip"
    Write-Host "Extracting mod..."
    if (Test-Path "@zdo_arma_voice") { Remove-Item -Recurse -Force "@zdo_arma_voice" }
    Expand-Archive "zdo_arma_voice_mod.zip" -DestinationPath .
    Remove-Item "zdo_arma_voice_mod.zip"
} else {
    Write-Host "Skipping mod (server-only mode)"
}

# Download server
Write-Host "Downloading server..."
Invoke-WebRequest -Uri $serverAsset.browser_download_url -OutFile "zdo_arma_voice_server.zip"

# Extract server (flatten nested folder from artifact)
Write-Host "Extracting server..."
Expand-Archive "zdo_arma_voice_server.zip" -DestinationPath "server_tmp" -Force
$inner = Get-ChildItem "server_tmp" -Directory | Select-Object -First 1
$srcDir = if ($inner) { $inner.FullName } else { "server_tmp" }

if (Test-Path "server") {
    # Remove only known server-data folders, preserve custom ones (e.g. 040_*)
    $knownDataDirs = @("000_reset.sqf", "010_core", "020_functions", "030_commands")
    foreach ($d in $knownDataDirs) {
        $target = Join-Path (Join-Path "server" "server-data") $d
        if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    }
    # Copy new files over existing server dir (overwrite binaries, configs, etc.)
    Copy-Item -Path "$srcDir\*" -Destination "server" -Recurse -Force
} else {
    Move-Item -LiteralPath $srcDir -Destination "server"
}

if (Test-Path "server_tmp") { Remove-Item -Recurse -Force "server_tmp" }
Remove-Item "zdo_arma_voice_server.zip"

Write-Host ""
Write-Host "Done! Installed ZdoArmaVoice $tag"
if (-not $ServerOnly) {
    Write-Host "  @zdo_arma_voice/ - copy to your Arma 3 directory"
}
Write-Host "  server/     - copy config-example.yaml to config.yaml, edit it, then run:"
Write-Host "               .\server\ZdoArmaVoice.Server.exe --config .\server\config.yaml"
