$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$taskFolder = Join-Path $PSScriptRoot ('altg-diagnostic-' + [guid]::NewGuid().ToString('N'))
$taskWorker = $null
$taskPhase = $null
$taskCompleted = $false

function Read-Status {
    for ($taskRetry=0; $taskRetry -lt 5; $taskRetry++) {
        try {
            $taskStream = [IO.File]::Open($taskReport, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $taskReader = [IO.StreamReader]::new($taskStream)
            try { return ($taskReader.ReadToEnd() | ConvertFrom-Json) }
            finally { $taskReader.Dispose() }
        } catch { Start-Sleep -Milliseconds 150 }
    }
    throw 'Brak raportu silnika. Zachowano czesciowy zapis; nie powtarzaj od razu testu.'
}
function Check-Worker {
    if ($taskWorker.HasExited) { throw 'Zapis sie zatrzymal. Zachowano raport i logi.' }
    $taskStatus = Read-Status
    if ($null -ne $taskStatus.error) { throw ('Blad zapisu: ' + $taskStatus.error) }
    if ($taskStatus.stopReason -ne 'running') { throw ('Zapis zatrzymany: ' + $taskStatus.stopReason) }
}
function Mark-Phase([string]$taskName) {
    Check-Worker
    [IO.File]::WriteAllText($taskPhase, $taskName)
    Add-Content -LiteralPath (Join-Path $taskFolder 'actions.txt') -Value ((Get-Date -Format o) + ' ' + $taskName)
    Start-Sleep -Milliseconds 250
}
function Wait-Capture([int]$taskSeconds) {
    for ($taskSecond=0; $taskSecond -lt $taskSeconds; $taskSecond++) {
        Start-Sleep -Seconds 1
        Check-Worker
    }
}

Write-Host 'AION2 Presence - krotki zapis craftingu przez ALT+G'
Write-Host 'Tylko JEDEN przepis i JEDNA seria 3 sztuk. Bez testow przy stole.'
Write-Host 'Uruchom tutaj, na Shadow PC z dzialajacym AION 2 i Npcap. ExitLag moze zostac wlaczony.'
Write-Host 'Zamknij zwykle Presence przez jego menu przy zegarze. Przygotuj materialy na 3 sztuki.'
Write-Host 'Narzedzie pobierze silnik beta.14 z oficjalnego repozytorium i sprawdzi jego SHA-256.'
Write-Host 'Zapis obejmuje przychodzace dane gry, takze mozliwy czat i dane postaci.'
Write-Host 'Nic nie jest automatycznie wysylane. ZIP przekaz tylko w prywatnym czacie, nie na publicznym GitHubie.'
$taskRecipe = Read-Host 'Nazwa przepisu (opcjonalnie)'
[void](Read-Host 'Zamknij okno craftingu w grze. Enter pobierze narzedzie i rozpocznie zapis')

try {
    [void][IO.Directory]::CreateDirectory($taskFolder)
    $taskArchive = Join-Path $taskFolder 'diagnostic-engine.zip'
    $taskRuntime = Join-Path $taskFolder 'runtime'
    $taskUri = 'https://github.com/Kooowal-design/aion2-presence/releases/download/v0.2.0-beta.14/Aion2Presence-Crafting-Diagnostic.zip'
    Invoke-WebRequest -UseBasicParsing -Uri $taskUri -OutFile $taskArchive
    $taskExpectedHash = '2be64af2b7feb0c84b6e99ef16646afb6362254c8f7e93c9358a533dfe96b77b'
    if ((Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash -ne $taskExpectedHash) {
        throw 'Pobrany plik nie przeszedl kontroli SHA-256. Silnik nie zostal uruchomiony.'
    }
    Expand-Archive -LiteralPath $taskArchive -DestinationPath $taskRuntime
    $taskEngines = @(Get-ChildItem -LiteralPath $taskRuntime -Filter 'aion-presence-engine.exe' -Recurse -File)
    if ($taskEngines.Count -ne 1) { throw 'Nie znaleziono jednoznacznie silnika diagnostycznego.' }
    $taskEngine = $taskEngines[0].FullName
    $taskReport = Join-Path $taskFolder 'craft-report.json'
    $taskPhase = Join-Path $taskFolder 'phase.txt'
    # Use existing engine markers for compatibility. Context explicitly maps
    # these historical labels to ALT+G; they do not imply a potion recipe.
    @{ schema=1; scenario='alt-g-remote-crafting'; recipe=$taskRecipe;
       startedAt=(Get-Date -Format o); engineVersion='v0.2.0-beta.14';
       phaseMeaning=@{ idle='closed crafting window baseline'; 'potions-window'='ALT+G recipe menu';
         'potions-finish'='one ALT+G batch of exactly 3 items'; 'potions-finished'='batch completed naturally' };
       markerMeaning='user action windows, not exact server event timestamps';
       scope='incoming game data only; no automatic upload; no client commands' } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskFolder 'test-context.json') -Encoding UTF8
    [IO.File]::WriteAllText($taskPhase, 'idle')
    $taskWorker = Start-Process -FilePath $taskEngine -ArgumentList @('--craft-probe', ('"'+$taskReport+'"'), ('"'+$taskPhase+'"')) -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $taskFolder 'engine-stderr.txt') -RedirectStandardOutput (Join-Path $taskFolder 'engine-stdout.txt')
    Start-Sleep -Seconds 2
    Write-Host 'Sprawdzam odbior gry przez 5 sekund...'
    Wait-Capture 5
    $taskStatus = Read-Status
    if (-not $taskStatus.portDetected -or $taskStatus.frames -lt 5) {
        throw 'Brak odbioru gry. NIE rozpoczynaj craftingu. Zachowano raport; nie powtarzaj serii.'
    }
    [void](Read-Host 'Enter, potem otworz ALT+G i wybierz przepis. JESZCZE NIE CRAFTUJ')
    Mark-Phase 'potions-window'
    [void](Read-Host 'Gdy przepis jest wybrany, wroc tutaj i nacisnij Enter')
    [void](Read-Host 'Enter, potem w grze uruchom JEDNA automatyczna serie dokladnie 3 sztuk przez ALT+G')
    Mark-Phase 'potions-finish'
    [void](Read-Host 'Pozwol serii skonczyc sie samodzielnie. Dopiero potem wroc tutaj i nacisnij Enter')
    Mark-Phase 'potions-finished'
    Write-Host 'Zapisuje jeszcze 8 sekund po zakonczeniu...'
    Wait-Capture 8
    $taskCompleted = $true
} catch {
    Write-Warning $_.Exception.Message
    if (Test-Path -LiteralPath $taskFolder) {
        $_.Exception.Message | Set-Content -LiteralPath (Join-Path $taskFolder 'helper-error.txt')
    }
} finally {
    if ($null -ne $taskPhase) { [IO.File]::WriteAllText($taskPhase, 'done') }
    if ($null -ne $taskWorker) {
        if (-not $taskWorker.WaitForExit(10000)) {
            $taskWorker.Kill()
            $taskWorker.WaitForExit()
            'Engine did not stop within 10s; partial trace retained.' |
                Add-Content -LiteralPath (Join-Path $taskFolder 'helper-error.txt')
            $taskCompleted = $false
        }
        $taskWorker.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $taskFolder)) { exit 1 }
try {
    $taskStatus = Read-Status
    Write-Host ('Zapisano ramek: ' + $taskStatus.frames + '; koniec: ' + $taskStatus.stopReason)
    if (-not $taskStatus.complete -or -not $taskCompleted) {
        Write-Warning 'Zapis czesciowy. Wyslij go bez ponawiania testu.'
    }
} catch { Write-Warning $_.Exception.Message }
# Package only the report/trace/context/logs, never the runtime or downloaded archive.
$taskFiles = @(Get-ChildItem -LiteralPath $taskFolder -File | Where-Object { $_.Name -ne 'diagnostic-engine.zip' })
if ($taskFiles.Count -eq 0) { throw 'Nie powstal raport. Zglos komunikat bledu z tego okna.' }
$taskZip = $taskFolder + '.zip'
Compress-Archive -LiteralPath $taskFiles.FullName -DestinationPath $taskZip
Write-Host ''
Write-Host ('GOTOWE - DOLACZ W PRYWATNYM CZACIE: ' + $taskZip)
Write-Host 'Mozesz ponownie wlaczyc zwykle Presence. Nic nie zostalo automatycznie wyslane.'
[void](Read-Host 'Enter zamyka okno')
