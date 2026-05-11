# apply-icon.ps1
# Copia icona.png (in root del repo) negli asset richiesti da Expo (mobile)
# e dalla web app (favicon). Esegui ogni volta che cambi l'icona.

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$src    = Join-Path $root 'icona.png'

if (-not (Test-Path $src)) {
    Write-Host "icona.png non trovata in $root" -ForegroundColor Red
    Write-Host "Salva il file 'icona.png' nella root del progetto e riesegui." -ForegroundColor Yellow
    exit 1
}

# --- Mobile (Expo) ---
$mobileAssets = Join-Path $root 'mobile\assets'
if (-not (Test-Path $mobileAssets)) { New-Item -ItemType Directory -Path $mobileAssets | Out-Null }

$targets = @(
    'icon.png',            # icona principale app
    'adaptive-icon.png',   # icona adattiva Android
    'favicon.png',         # favicon web (Expo web)
    'splash-icon.png'      # splash (se usata)
)
foreach ($t in $targets) {
    $dst = Join-Path $mobileAssets $t
    Copy-Item -Path $src -Destination $dst -Force
    Write-Host "OK  mobile/assets/$t" -ForegroundColor Green
}

# --- Web app (build statica Expo Web) ---
$webDist = Join-Path $root 'web'
if (Test-Path $webDist) {
    Copy-Item -Path $src -Destination (Join-Path $webDist 'favicon.png') -Force
    Write-Host "OK  web/favicon.png" -ForegroundColor Green
}

Write-Host ""
Write-Host "Fatto. Ricostruisci l'app mobile (npx expo start --clear) per vedere la nuova icona." -ForegroundColor Cyan
