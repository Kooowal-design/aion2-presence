$ErrorActionPreference = 'Stop'
$taskEngine = Join-Path $PSScriptRoot 'aion-presence-engine.exe'
if (-not (Test-Path -LiteralPath $taskEngine)) { throw 'Brak aion-presence-engine.exe obok skryptu.' }
$taskFolder = Join-Path $PSScriptRoot ('craft-diagnostic-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($taskFolder)
$taskReport = Join-Path $taskFolder 'craft-report.json'
$taskPhase = Join-Path $taskFolder 'phase.txt'
$taskWorker = $null
$taskFailure = $null
function Read-Status {
    for ($taskRetry=0; $taskRetry -lt 5; $taskRetry++) {
        try { return (Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json) }
        catch { Start-Sleep -Milliseconds 150 }
    }
    throw 'Brak poprawnego raportu silnika. Zapis diagnostyczny zostanie zachowany.'
}
function Check-Worker {
    if ($taskWorker.HasExited) { throw 'Silnik zakonczyl diagnostyke. Szczegoly sa w raporcie i engine-stderr.txt.' }
    $taskStatus = Read-Status
    if ($null -ne $taskStatus.error) { throw ('Blad zapisu: ' + $taskStatus.error) }
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
        $taskStatus = Read-Status
        Write-Progress -Activity 'Zapis diagnostyki' -Status ("Pozostalo {0}s | ramki {1} | {2}" -f ($taskSeconds-$taskSecond),$taskStatus.frames,$taskStatus.phase) -PercentComplete (100*$taskSecond/$taskSeconds)
    }
    Write-Progress -Activity 'Zapis diagnostyki' -Completed
}
function Test-Profession([string]$taskPrefix,[string]$taskDisplay) {
    [void](Read-Host ("{0}: Enter, potem otworz okno profesji i wybierz przepis, ale NIE uruchamiaj craftu" -f $taskDisplay))
    Mark-Phase ($taskPrefix+'-window')
    [void](Read-Host 'Gdy przepis jest wybrany i nic nie craftujesz, wroc tutaj i nacisnij Enter')
    Wait-Capture 5
    [void](Read-Host 'Enter, potem uruchom JEDNA automatyczna serie co najmniej 20 sztuk. NIE uruchamiaj kazdej sztuki osobno')
    Mark-Phase ($taskPrefix+'-batch')
    Write-Host 'Wroc do gry i uruchom serie. Przez 60 sekund niczego nie przerywaj.'
    Wait-Capture 60
    [void](Read-Host 'Enter, potem wroc do gry i ANULUJ te serie')
    Mark-Phase ($taskPrefix+'-cancel')
    [void](Read-Host 'Kiedy seria jest juz ANULOWANA, wroc tutaj i nacisnij Enter')
    Wait-Capture 8
    [void](Read-Host 'Enter, potem uruchom nowa automatyczna serie dokladnie 3 sztuk i pozwol jej skonczyc sie SAMODZIELNIE')
    Mark-Phase ($taskPrefix+'-finish')
    [void](Read-Host 'Dopiero gdy wszystkie 3 sztuki sie skoncza, wroc tutaj i nacisnij Enter')
    Mark-Phase ($taskPrefix+'-finished')
    Wait-Capture 8
}
Write-Host 'Diagnostyka v3: mikstury + jedzenie. Uruchom na ShadowPC z wlaczona gra i Npcap.'
Write-Host 'Zamknij normalne Presence przez zasobnik. Przygotuj materialy na min. 20+3 potki i 20+3 food.'
Write-Host 'Serie po 20 sztuk anulujemy po minucie; nie musisz zuzywac wszystkich materialow.'
Write-Host 'Zapisuje PELNE komunikaty serwer->gra, przed i po dekodowaniu; moga zawierac czat/dane postaci.'
Write-Host 'NIE wysyla nic automatycznie. Nie publikuj raportu na GitHub. Limit: 20 minut / 128 MiB.'
Write-Host 'Nie wykonuj w trakcie innych aktywnosci. Nie trzeba testowac kazdego przepisu.'
$taskPotion = Read-Host 'Nazwa testowanych mikstur (mozesz pozostawic puste)'
$taskFood = Read-Host 'Nazwa testowanego jedzenia (mozesz pozostawic puste)'
@{ schema=3; potion=$taskPotion; food=$taskFood; startedAt=(Get-Date -Format o); expected='one automatic batch per step'; markerMeaning='user action windows; not inferred exact game start/end'; scope='full incoming game stream and decoded frames; client commands are not captured' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskFolder 'test-context.json') -Encoding UTF8
[void](Read-Host 'Zamknij okno craftingu w grze. Enter rozpoczyna zapis')
[IO.File]::WriteAllText($taskPhase, 'idle')
try {
    $taskWorker = Start-Process -FilePath $taskEngine -ArgumentList @('--craft-probe', ('"' + $taskReport + '"'), ('"' + $taskPhase + '"')) -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $taskFolder 'engine-stderr.txt') -RedirectStandardOutput (Join-Path $taskFolder 'engine-stdout.txt')
    Start-Sleep -Seconds 2
    Write-Host 'Kontrola odbioru: pozostan w grze, mozesz poruszyc postacia. Zapis tla przez 15 sekund.'
    Wait-Capture 15
    $taskStatus = Read-Status
    if (-not $taskStatus.portDetected -or $taskStatus.frames -lt 5) {
        throw 'Nie wykryto strumienia gry. NIE zaczynaj testow craftingu. Sprawdz Npcap/gre; zachowano diagnostyke.'
    }
    Write-Host ("Odbior dziala: {0} ramek. Teraz testy." -f $taskStatus.frames)
    Test-Profession 'potions' 'MIKSTURY'
    Test-Profession 'food' 'JEDZENIE'
    Write-Host 'Oba testy zakonczone.'
} catch {
    $taskFailure = $_.Exception.Message
    Write-Warning $taskFailure
    $taskFailure | Set-Content -LiteralPath (Join-Path $taskFolder 'helper-error.txt')
} finally {
    [IO.File]::WriteAllText($taskPhase, 'done')
    if ($null -ne $taskWorker) {
        if (-not $taskWorker.WaitForExit(10000)) {
            $taskWorker.Kill()
            $taskWorker.WaitForExit()
            'Helper had to terminate the engine after 10s.' | Add-Content -LiteralPath (Join-Path $taskFolder 'helper-error.txt')
        }
        $taskWorker.Dispose()
    }
}
try {
    $taskStatus = Read-Status
    Write-Host ("Zapis: {0} ramek, {1} fragmentow strumienia, {2} bajtow; koniec: {3}" -f $taskStatus.frames,$taskStatus.streamChunks,$taskStatus.traceBytes,$taskStatus.stopReason)
    if (-not $taskStatus.complete) { Write-Warning 'Zapis nie zostal zakonczony normalnie. Nie powtarzaj od razu testu; dolacz to, co sie zapisalo.' }
} catch { Write-Warning $_.Exception.Message }
$taskZip = $taskFolder + '.zip'
Compress-Archive -Path (Join-Path $taskFolder '*') -DestinationPath $taskZip
Write-Host ''
Write-Host ('ZALACZ W CZACIE TEN ZIP: ' + $taskZip)
Write-Host 'ZIP zawiera prywatny zapis komunikatow gry. Nic nie zostalo wyslane.'
Write-Host 'Mozesz ponownie wlaczyc normalne Presence. Nie musisz instalowac nowej wersji aplikacji.'
[void](Read-Host 'Enter zamyka okno')

