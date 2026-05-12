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

    # Ferma il backend se in esecuzione (blocca i file DLL)
    $dotnetProc = Get-Process dotnet -ErrorAction SilentlyContinue
    if ($dotnetProc) {
        Write-Host "  Fermo il backend per sbloccare i file..." -ForegroundColor Yellow
        Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    }

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

function Suggest-CommitMessage {
    # Costruisce un messaggio commit precompilato a partire dai file modificati,
    # raggruppati per area logica + un tentativo di descrizione automatica.
    $statusLines = git -C $root status --porcelain
    if (-not $statusLines) { return "" }

    $files = $statusLines | ForEach-Object { ($_ -replace '^...','').Trim() }
    $by = @{
        backend  = @($files | Where-Object { $_ -like 'backend/*' })
        mobile   = @($files | Where-Object { $_ -like 'mobile/*' })
        web      = @($files | Where-Object { $_ -like 'web/*' })
        infra    = @($files | Where-Object { $_ -in @('docker-compose.yml','Dockerfile','railway.toml','.dockerignore','.gitignore') -or $_ -like '.github/*' -or $_ -like '*.ps1' })
        other    = @()
    }
    $known = $by.backend + $by.mobile + $by.web + $by.infra
    $by.other = @($files | Where-Object { $known -notcontains $_ })

    $parts = @()
    if ($by.backend) { $parts += "backend ($($by.backend.Count))" }
    if ($by.mobile)  { $parts += "mobile ($($by.mobile.Count))" }
    if ($by.web)     { $parts += "web ($($by.web.Count))" }
    if ($by.infra)   { $parts += "infra ($($by.infra.Count))" }
    if ($by.other)   { $parts += "other ($($by.other.Count))" }

    # Heuristica: prova a indovinare COSA cambia guardando i nomi file.
    # E` solo un suggerimento; l'utente potra` riscriverlo.
    $hints = @()
    $allLower = ($files -join ' ').ToLower()
    if ($allLower -match 'controller')                  { $hints += 'endpoint API' }
    if ($allLower -match 'screen|component')            { $hints += 'UI mobile' }
    if ($allLower -match 'mail|email|template')         { $hints += 'template email' }
    if ($allLower -match 'auth|login|password|token')   { $hints += 'autenticazione' }
    if ($allLower -match 'notification|push')           { $hints += 'notifiche' }
    if ($allLower -match 'migration')                   { $hints += 'migration DB' }
    if ($allLower -match 'context|provider')            { $hints += 'state app' }
    if ($allLower -match 'theme|style|color|palette')   { $hints += 'styling' }
    if ($allLower -match 'dev\.ps1|start-expo|script')  { $hints += 'tooling dev' }
    if ($allLower -match 'permission|avatar|image')     { $hints += 'permessi/media' }
    $hintLine = if ($hints) { "Aree toccate: " + ($hints -join ', ') + "." } else { "" }

    $title = "chore: update " + ($parts -join ", ")

    $bodyLines = @()
    if ($hintLine) {
        $bodyLines += ""
        $bodyLines += $hintLine
        $bodyLines += "(scrivi qui sotto una breve nota su cosa/perche` se utile)"
    }
    foreach ($area in 'backend','mobile','web','infra','other') {
        $list = $by[$area]
        if ($list -and $list.Count -gt 0) {
            $bodyLines += ""
            $bodyLines += "[$area]"
            foreach ($f in ($list | Select-Object -First 12)) { $bodyLines += "  - $f" }
            if ($list.Count -gt 12) { $bodyLines += "  - ... (+$($list.Count - 12) altri)" }
        }
    }

    return ($title + "`n" + ($bodyLines -join "`n"))
}

function Read-CommitMessage([string]$suggested) {
    # Mostra il suggerimento e permette:
    #   invio                          = usa cosi com'e (titolo + descrizione + file)
    #   d                              = aggiungi una nota tua in cima e tieni il resto
    #   n                              = scrivi tutto da zero (single-line)
    #   qualsiasi altra stringa        = usa quella riga come commit completo
    Write-Host ""
    Write-Host "  Messaggio commit suggerito:" -ForegroundColor Cyan
    Write-Host "  ----------------------------------------------------------" -ForegroundColor DarkGray
    foreach ($l in ($suggested -split "`n")) { Write-Host "  $l" -ForegroundColor Gray }
    Write-Host "  ----------------------------------------------------------" -ForegroundColor DarkGray
    Write-Host "  [invio] usa il suggerito"
    Write-Host "  [d]     aggiungi una breve descrizione in cima e tieni il resto"
    Write-Host "  [n]     scrivi tutto da zero"
    Write-Host "  oppure scrivi direttamente un messaggio inline"
    $resp = Read-Host "  > "
    if ([string]::IsNullOrWhiteSpace($resp)) { return $suggested }
    if ($resp -match '^[Dd]$') {
        $note = Read-Host "  Breve descrizione (cosa/perche)"
        if ([string]::IsNullOrWhiteSpace($note)) { return $suggested }
        # Sostituisce il titolo con quello dell'utente e aggiunge il body originale.
        $body = ($suggested -split "`n", 2)[1]
        return "$note`n$body"
    }
    if ($resp -match '^[Nn]$') {
        $custom = Read-Host "  Messaggio commit (lascia vuoto per annullare)"
        if ([string]::IsNullOrWhiteSpace($custom)) { return $null }
        return $custom
    }
    return $resp
}

function Read-TargetBranch([string]$default) {
    Write-Host ""
    Write-Host "  Branch disponibili:" -ForegroundColor Cyan
    $branches = git -C $root branch --format='%(refname:short)'
    foreach ($b in $branches) { Write-Host "   - $b" -ForegroundColor Gray }
    $branch = Read-Host "  Branch destinazione? [invio = $default]"
    if ([string]::IsNullOrWhiteSpace($branch)) { return $default }
    return $branch.Trim()
}

function Git-Push {
    Write-Host ""
    $currentBranch = git -C $root branch --show-current 2>$null
    Write-Host "  Branch corrente: $currentBranch" -ForegroundColor Cyan
    Write-Host ""
    $branch = Read-TargetBranch $currentBranch

    if ($branch -ne $currentBranch) {
        git -C $root checkout $branch
    }

    Write-Host ""
    git -C $root status --short
    Write-Host ""
    $suggested = Suggest-CommitMessage
    if ($suggested) {
        $msg = Read-CommitMessage $suggested
        if ($null -eq $msg) {
            Write-Host "  Nessun commit, annullato." -ForegroundColor DarkGray
            Wait-Key
            return
        }
        git -C $root add -A
        git -C $root commit -m $msg
    } else {
        Write-Host "  Niente di modificato, faccio solo push." -ForegroundColor DarkGray
    }

    git -C $root push origin $branch
    Write-Host "  Push completato." -ForegroundColor Green
    Wait-Key
}

function Deploy-Railway {
    Write-Host ""
    Write-Host "  DEPLOY RAILWAY" -ForegroundColor Magenta
    Write-Host "  Railway deploya automaticamente dal branch di produzione (default: main)." -ForegroundColor Yellow
    Write-Host ""

    $currentBranch = git -C $root branch --show-current 2>$null
    Write-Host "  Branch corrente: $currentBranch" -ForegroundColor Cyan
    Write-Host ""

    # Branch di produzione su Railway (di default main, ma potresti averlo cambiato)
    $prodBranch = Read-TargetBranch "main"

    Write-Host ""
    git -C $root status --short
    Write-Host ""

    # Se ci sono modifiche non committate sul branch corrente, proponi commit prima del merge.
    $statusLines = git -C $root status --porcelain
    if ($statusLines) {
        $suggested = Suggest-CommitMessage
        $msg = Read-CommitMessage $suggested
        if ($null -ne $msg) {
            git -C $root add -A
            git -C $root commit -m $msg
        }
    }

    if ($currentBranch -ne $prodBranch) {
        Write-Host "  Sei su '$currentBranch'. Merge su '$prodBranch'?" -ForegroundColor Yellow
        $resp = Read-Host "  [S/n]"
        if ($resp -notmatch "^[Nn]") {
            git -C $root checkout $prodBranch
            git -C $root merge $currentBranch --no-edit
        } else {
            Write-Host "  Annullato." -ForegroundColor DarkGray
            Wait-Key
            return
        }
    }

    $confirm = Read-Host "  Push su '$prodBranch' (deploy Railway)? [S/n]"
    if ($confirm -match "^[Nn]") {
        Write-Host "  Annullato." -ForegroundColor DarkGray
        Wait-Key
        return
    }

    git -C $root push origin $prodBranch
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
    if ($confirm -notmatch '^[Ss][Ii]$') {
        Write-Host "  Annullato." -ForegroundColor DarkGray
        Wait-Key
        return
    }

    # Converti URL Railway (postgresql://user:pass@host:port/db) in ADO.NET
    $connString = $railwayUrl
    if ($railwayUrl -match '^postgres(?:ql)?://([^:]+):([^@]+)@([^:/]+):(\d+)/(.+)$') {
        $user = $Matches[1]
        $pass = $Matches[2]
        $pgHost = $Matches[3]
        $port = $Matches[4]
        $db   = $Matches[5]
        $connString = "Host=$pgHost;Port=$port;Database=$db;Username=$user;Password=$pass;SSL Mode=Require;Trust Server Certificate=true"
        Write-Host "  Connection string convertita:" -ForegroundColor Green
        Write-Host "    Host=$pgHost Port=$port DB=$db User=$user" -ForegroundColor DarkGray
    } elseif ($railwayUrl -match '^postgres') {
        Write-Host "  ERRORE: formato URL non riconosciuto." -ForegroundColor Red
        Write-Host "  Formato atteso: postgresql://user:pass@host:port/db" -ForegroundColor Yellow
        Wait-Key
        return
    }

    # Ferma il backend se in esecuzione (blocca i file DLL)
    $dotnetProc = Get-Process dotnet -ErrorAction SilentlyContinue
    $wasRunning = $false
    if ($dotnetProc) {
        Write-Host "  Fermo il backend per sbloccare i file..." -ForegroundColor Yellow
        Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $wasRunning = $true
    }

    $env:DATABASE_URL_DESIGN = $connString
    Write-Host "  Build..." -ForegroundColor Cyan
    Set-Location $backend
    dotnet build src/OnCallendar.Api/OnCallendar.Api.csproj
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Build failed. Correggi gli errori prima di migrare." -ForegroundColor Red
        Set-Location $root
        Wait-Key
        return
    }
    Write-Host "  Build OK. Applico migrations..." -ForegroundColor Cyan
    dotnet ef database update `
        --project src/OnCallendar.Infrastructure `
        --startup-project src/OnCallendar.Api `
        --no-build
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Migrations produzione applicate." -ForegroundColor Green
    } else {
        Write-Host "  ERRORE durante la migrazione." -ForegroundColor Red
    }
    Set-Location $root
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
