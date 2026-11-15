$ErrorActionPreference = 'Stop'
$taskEngine = Join-Path $PSScriptRoot 'aion-presence-engine.exe'
if (-not (Test-Path -LiteralPath $taskEngine)) { throw 'Brak aion-presence-engine.exe obok skryptu.' }
$taskFolder = Join-Path $PSScriptRoot ('craft-diagnostic-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($taskFolder)
$taskReport = Join-Path $taskFolder 'craft-report.json'
$taskPhase = Join-Path $taskFolder 'phase.txt'
$taskWorker = $null
Write-Host 'Uruchom na ShadowPC z wlaczona gra. Zamknij Presence przez zasobnik.'
Write-Host 'Zapis: tylko typy komunikatow, rozmiary, czasy. Bez tresci pakietow i bez wysylania.'
Write-Host 'Otworz okno alchemii, ale jeszcze nic nie wytwarzaj.'
[void](Read-Host 'Enter rozpoczyna test')
[IO.File]::WriteAllText($taskPhase, 'idle')
try {
    $taskWorker = Start-Process -FilePath $taskEngine -ArgumentList @('--craft-probe', ('"' + $taskReport + '"'), ('"' + $taskPhase + '"')) -WindowStyle Hidden -PassThru
    Write-Host 'Etap 1: nie craftuj przez 20 sekund.'
    Start-Sleep -Seconds 20
    if ($taskWorker.HasExited) { throw 'Silnik zakonczyl prace przed testem.' }
    [void](Read-Host 'Etap 2: po Enter wroc do gry i uruchom serie mikstur na co najmniej 45 sekund')
    if ($taskWorker.HasExited) { throw 'Czas diagnostyki uplynal. Uruchom ponownie.' }
    [IO.File]::WriteAllText($taskPhase, 'crafting')
    Write-Host 'Teraz craftuj. Zbieram dane przez 45 sekund.'
    Start-Sleep -Seconds 45
    [void](Read-Host 'Etap 3: po Enter wroc do gry i anuluj serie')
    if ($taskWorker.HasExited) { throw 'Czas diagnostyki uplynal. Uruchom ponownie.' }
    [IO.File]::WriteAllText($taskPhase, 'stopped')
    Write-Host 'Anuluj crafting i nic nie wytwarzaj przez 20 sekund.'
    Start-Sleep -Seconds 20
} finally {
    [IO.File]::WriteAllText($taskPhase, 'done')
    if ($null -ne $taskWorker) {
        if (-not $taskWorker.WaitForExit(10000)) { $taskWorker.Kill(); $taskWorker.WaitForExit() }
        $taskWorker.Dispose()
    }
}
$taskData = Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
$taskFrames = ($taskData.rows | Measure-Object -Property count -Sum).Sum
if (-not $taskData.portDetected -or $taskFrames -eq 0) {
    Write-Warning 'Brak komunikatow gry. Raport nie wystarczy do analizy.'
}
Write-Host ('Raport do zalaczenia w czacie: ' + $taskReport)
Write-Host 'Nic nie zostalo wyslane. Mozesz ponownie uruchomic normalne Presence.'
[void](Read-Host 'Enter zamyka okno')
