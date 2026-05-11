$ErrorActionPreference = "Continue"
$root   = "C:\Users\Clore\Sviluppo\OnCallendar"
$cf     = "C:\Users\Clore\AppData\Local\Microsoft\WinGet\Packages\Cloudflare.cloudflared_Microsoft.Winget.Source_8wekyb3d8bbwe\cloudflared.exe"

Get-Process cloudflared,node,dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

$logBackend = "$env:TEMP\cf-backend.log"
Remove-Item $logBackend -ErrorAction SilentlyContinue

Write-Host "Avvio tunnel cloudflared Backend (5000)..." -ForegroundColor Cyan
$cfBackend = Start-Process -FilePath $cf -ArgumentList "tunnel --url http://localhost:5000" -RedirectStandardError $logBackend -PassThru -WindowStyle Hidden

Write-Host "Attendo URL cloudflared backend (max 40s)..." -ForegroundColor Yellow
$backendUrl = $null
for ($i = 1; $i -le 40; $i++) {
    Start-Sleep -Seconds 1
    if (Test-Path $logBackend) {
        $txt = Get-Content $logBackend -Raw -ErrorAction SilentlyContinue
        if ($txt -match 'https://[a-z0-9\-]+\.trycloudflare\.com') { $backendUrl = $Matches[0] }
    }
    if ($backendUrl) { break }
    Write-Host "  ($i/40)  Backend=..." -ForegroundColor DarkGray
}

if (-not $backendUrl) { Write-Error "Tunnel Backend non avviato"; exit 1 }

Write-Host "=== TUNNEL ATTIVO ===" -ForegroundColor Green
Write-Host "  Backend: $backendUrl" -ForegroundColor Green

$envFile = "$root\mobile\.env"
(Get-Content $envFile -Raw) -replace 'EXPO_PUBLIC_API_BASE_URL=https?://[^\r\n]+', "EXPO_PUBLIC_API_BASE_URL=$backendUrl" | Set-Content $envFile -Encoding UTF8 -NoNewline
Write-Host "mobile/.env aggiornato: EXPO_PUBLIC_API_BASE_URL=$backendUrl" -ForegroundColor Cyan

# appsettings.Local.json: MobileDeepLinkBaseUrl placeholder (verrà aggiornato a runtime da ngrok)
$apiSettingsDir = "$root\backend\src\OnCallendar.Api"
$localSettings  = Join-Path $apiSettingsDir "appsettings.Local.json"
$localJson = @{
    Mail = @{
        MobileDeepLinkBaseUrl = ""
    }
} | ConvertTo-Json -Depth 5
Set-Content -Path $localSettings -Value $localJson -Encoding UTF8

Write-Host "Avvio backend (dotnet run)..." -ForegroundColor Cyan
$backendCsproj = "$root\backend\src\OnCallendar.Api\OnCallendar.Api.csproj"
Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$backendCsproj`" --no-build" -WorkingDirectory "$root\backend" -WindowStyle Minimized
Write-Host "Backend in avvio in finestra separata. Aspetto 5s..." -ForegroundColor DarkGray
Start-Sleep -Seconds 5

Set-Location "$root\mobile"
Write-Host ""
Write-Host "Avvio Metro con tunnel cloudflared (--tunnel)..." -ForegroundColor Cyan
Write-Host "Scansiona il QR che appare con Expo Go" -ForegroundColor Yellow
Write-Host ""
npx expo start --tunnel --clear 2>&1
