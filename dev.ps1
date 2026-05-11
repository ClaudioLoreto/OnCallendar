# OnCallendar Dev CLI
# Uso: .\dev.ps1  (dalla root C:\Users\Clore\Sviluppo\OnCallendar)

$ErrorActionPreference = "Stop"
$root       = "C:\Users\Clore\Sviluppo\OnCallendar"
$mobile     = "$root\mobile"
$backend    = "$root\backend"
$backendCsp = "$backend\src\OnCallendar.Api\OnCallendar.Api.csproj"
$cf         = "C:\Users\Clore\AppData\Local\Microsoft\WinGet\Packages\Cloudflare.cloudflared_Microsoft.Winget.Source_8wekyb3d8bbwe\cloudflared.exe"
$envFile    = "$mobile\.env"

Set-Location $root

function Show-Menu {
    Clear-Host
    Write-Host ""
    Write-Host "  ================================================" -ForegroundColor Cyan
    Write-Host "           OnCallendar  Dev CLI" -ForegroundColor Cyan
    Write-Host "  ================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  DEV LOCALE" -ForegroundColor Yellow
    Write-Host "   1  Avvia tutto  (tunnel + backend + Expo QR)" -ForegroundColor White
    Write-Host "   2  Solo backend" -ForegroundColor White
    Write-Host "   3  Solo Expo" -ForegroundColor White
    Write-Host "   4  Migrations DB locale" -ForegroundColor White
    Write-Host "   5  Ferma tutto" -ForegroundColor White
    Write-Host ""
    Write-Host "  GIT / DEPLOY" -ForegroundColor Yellow
    Write-Host "   6  Git commit + push" -ForegroundColor White
    Write-Host "   7  Deploy Railway (push su main)" -ForegroundColor White
    Write-Host "   8  Migra DB produzione Railway" -ForegroundColor White
    Write-Host ""
    Write-Host "   0  Esci" -ForegroundColor DarkGray
    Write-Host ""
}

function Wait-Key {
    Write-Host ""
    Write-Host "  [invio per tornare al menu]" -ForegroundColor DarkGray
    $null = Read-Host
}

function Start-All {
    Write-Host ""
    Write-Host "  Fermo processi precedenti..." -ForegroundColor Yellow
    Get-Process cloudflared, node, dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800

    $logBackend = "$env:TEMP\cf-backend.log"
    Remove-Item $logBackend -ErrorAction SilentlyContinue
    Write-Host "  Avvio cloudflared tunnel (porta 5000)..." -ForegroundColor Cyan
    $null = Start-Process -FilePath $cf -ArgumentList "tunnel --url http://localhost:5000" `
        -RedirectStandardError $logBackend -PassThru -WindowStyle Hidden

    Write-Host "  Attendo URL tunnel (max 40s)..." -ForegroundColor Yellow
    $backendUrl = $null
    for ($i = 1; $i -le 40; $i++) {
        Start-Sleep -Seconds 1
        if (Test-Path $logBackend) {
            $txt = Get-Content $logBackend -Raw -ErrorAction SilentlyContinue
            if ($txt -match 'https://[a-z0-9\-]+\.trycloudflare\.com') {
                $backendUrl = $Matches[0]
            }
        }
        if ($backendUrl) { break }
        Write-Host "    ($i/40)..." -ForegroundColor DarkGray
    }

    if (-not $backendUrl) {
        Write-Host "  ERRORE: tunnel non avviato." -ForegroundColor Red
        Wait-Key
        return
    }

    Write-Host "  Tunnel OK: $backendUrl" -ForegroundColor Green

    $content = Get-Content $envFile -Raw
    $content = $content -replace 'EXPO_PUBLIC_API_BASE_URL=https?://[^\r\n]+', "EXPO_PUBLIC_API_BASE_URL=$backendUrl"
    Set-Content $envFile -Value $content -Encoding UTF8 -NoNewline
    Write-Host "  .env aggiornato" -ForegroundColor Green

    Write-Host "  Avvio backend .NET..." -ForegroundColor Cyan
    Start-Process -FilePath "dotnet" -ArgumentList "run --project `"$backendCsp`" --no-build" `
        -WorkingDirectory $backend -WindowStyle Minimized
    Write-Host "  Backend in avvio (attendo 5s)..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 5

    Write-Host ""
    Write-Host "  Avvio Expo tunnel... scansiona il QR con Expo Go" -ForegroundColor Green
    Write-Host ""
    Set-Location $mobile
    npx expo start --tunnel --clear
    Set-Location $root
}

function Start-Backend {
    Write-Host ""
    Write-Host "  Avvio backend in finestra esterna..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList "-NoExit", "-ExecutionPolicy", "Bypass", "-Command", `
        "Set-Location '$backend'; dotnet run --project 'src/OnCallendar.Api/OnCallendar.Api.csproj'"
    Write-Host "  Fatto." -ForegroundColor Green
    Wait-Key
}

function Start-Expo {
    Write-Host ""
    Write-Host "  Avvio Expo tunnel..." -ForegroundColor Cyan
    Set-Location $mobile
    npx expo start --tunnel --clear
    Set-Location $root
}

function Apply-Migrations {
    Write-Host ""
    Write-Host "  Migrations DB locale..." -ForegroundColor Cyan
    $env:DATABASE_URL_DESIGN = "Host=localhost;Port=5433;Database=oncallendar_dev;Username=oncallendar;Password=dev_only_password"
    Set-Location $backend
    dotnet ef database update `
        --project src/OnCallendar.Infrastructure `
        --startup-project src/OnCallendar.Api
    Set-Location $root
    Write-Host "  OK." -ForegroundColor Green
    Wait-Key
}

function Stop-All {
    Write-Host ""
    Write-Host "  Fermo cloudflared, node, dotnet..." -ForegroundColor Yellow
    Get-Process cloudflared, node, dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "  Tutto fermato." -ForegroundColor Green
    Wait-Key
}

function Git-Push {
    Write-Host ""
    $currentBranch = git -C $root branch --show-current 2>$null
    Write-Host "  Branch corrente: $currentBranch" -ForegroundColor Cyan
    Write-Host ""
    git -C $root branch
    Write-Host ""
    $branch = Read-Host "  Branch per il push? [invio = $currentBranch]"
    if ([string]::IsNullOrWhiteSpace($branch)) { $branch = $currentBranch }

    if ($branch -ne $currentBranch) {
        git -C $root checkout $branch
    }

    Write-Host ""
    git -C $root status --short
    Write-Host ""
    $msg = Read-Host "  Messaggio commit (invio = solo push senza commit)"
    if (-not [string]::IsNullOrWhiteSpace($msg)) {
        git -C $root add -A
        git -C $root commit -m $msg
    }

    git -C $root push origin $branch
    Write-Host "  Push completato." -ForegroundColor Green
    Wait-Key
}

function Deploy-Railway {
    Write-Host ""
    Write-Host "  DEPLOY RAILWAY" -ForegroundColor Magenta
    Write-Host "  Railway deploya automaticamente da main." -ForegroundColor Yellow
    Write-Host ""

    $currentBranch = git -C $root branch --show-current 2>$null
    if ($currentBranch -ne "main") {
        Write-Host "  Sei su '$currentBranch'. Merge su main?" -ForegroundColor Yellow
        $resp = Read-Host "  [S/n]"
        if ($resp -notmatch "^[Nn]") {
            $msg = Read-Host "  Messaggio commit (invio = skip)"
            if (-not [string]::IsNullOrWhiteSpace($msg)) {
                git -C $root add -A
                git -C $root commit -m $msg
            }
            git -C $root checkout main
            git -C $root merge $currentBranch --no-edit
        }
    }

    $confirm = Read-Host "  Push su main (deploy Railway)? [S/n]"
    if ($confirm -match "^[Nn]") {
        Write-Host "  Annullato." -ForegroundColor DarkGray
        Wait-Key
        return
    }

    git -C $root push origin main
    Write-Host "  Push eseguito. Controlla https://railway.app" -ForegroundColor Green
    Wait-Key
}

function Migrate-Railway {
    Write-Host ""
    Write-Host "  MIGRAZIONE DB PRODUZIONE RAILWAY" -ForegroundColor Red
    Write-Host "  ATTENZIONE: tocca il DB di produzione!" -ForegroundColor Yellow
    Write-Host ""
    $railwayUrl = Read-Host "  Incolla la DATABASE_URL di Railway"
    if ([string]::IsNullOrWhiteSpace($railwayUrl)) {
        Write-Host "  Annullato." -ForegroundColor DarkGray
        Wait-Key
        return
    }
    $confirm = Read-Host "  Sei sicuro? Scrivi SI per confermare"
    if ($confirm -ne "SI") {
        Write-Host "  Annullato." -ForegroundColor DarkGray
        Wait-Key
        return
    }
    $env:DATABASE_URL_DESIGN = $railwayUrl
    Set-Location $backend
    dotnet ef database update `
        --project src/OnCallendar.Infrastructure `
        --startup-project src/OnCallendar.Api
    Set-Location $root
    Write-Host "  Migrations produzione applicate." -ForegroundColor Green
    Wait-Key
}

# MAIN LOOP
while ($true) {
    Show-Menu
    $choice = (Read-Host "  Scelta").Trim()
    switch ($choice) {
        "1" { Start-All }
        "2" { Start-Backend }
        "3" { Start-Expo }
        "4" { Apply-Migrations }
        "5" { Stop-All }
        "6" { Git-Push }
        "7" { Deploy-Railway }
        "8" { Migrate-Railway }
        "0" { Write-Host "  Ciao!" -ForegroundColor Cyan; exit 0 }
        default {
            Write-Host "  Scelta non valida." -ForegroundColor Red
            Start-Sleep -Seconds 1
        }
    }
}
