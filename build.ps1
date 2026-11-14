param([switch]$UseLocalToolchain, [string]$DotnetExe = 'dotnet', [switch]$PackageRelease, [switch]$PublicRelease)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
Set-Location -LiteralPath $taskRoot
$identity = Get-Content discord-app.json -Raw | ConvertFrom-Json
if ($PublicRelease -and (-not $identity.projectOwned -or $identity.applicationId -eq '1556021373401432214')) {
    throw 'Public release requires a project-owned Discord application configured in discord-app.json.'
}
if ($UseLocalToolchain) {
    $env:RUSTUP_HOME = Join-Path $taskRoot '.tools/rustup'
    $env:CARGO_HOME = Join-Path $taskRoot '.tools/cargo'
    $env:PATH = (Join-Path $taskRoot '.tools/w64devkit/bin') + ';' + $env:PATH
    $env:CARGO_TARGET_X86_64_PC_WINDOWS_GNU_LINKER = Join-Path $taskRoot '.tools/rustup/toolchains/stable-x86_64-pc-windows-gnu/lib/rustlib/x86_64-pc-windows-gnu/bin/rust-lld.exe'
    $env:RUSTFLAGS = '-C linker-flavor=ld.lld -C link-self-contained=yes'
    $cargo = Join-Path $taskRoot '.tools/cargo/bin/cargo.exe'
} else { $cargo = 'cargo' }
& $cargo build --release --locked --manifest-path engine/Cargo.toml
if ($LASTEXITCODE -ne 0) { throw 'Engine build failed' }
& $cargo test --release --locked --manifest-path engine/Cargo.toml --bin aion-presence-engine
if ($LASTEXITCODE -ne 0) { throw 'Engine tests failed' }
& $DotnetExe restore AionPresence/AionPresence.csproj -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Windows runtime restore failed' }
& ./scripts/Prepare-Notices.ps1 -CargoExe $cargo -DotnetExe $DotnetExe
& $DotnetExe publish AionPresence/AionPresence.csproj -c Release -r win-x64 --self-contained true -o dist/app --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Windows app build failed' }
$test = Start-Process -FilePath (Join-Path $taskRoot 'dist/app/Aion2Presence.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if ($test.ExitCode -ne 0) { throw 'Discord IPC tests failed' }
Get-Content -LiteralPath 'dist/app/self-test-result.txt'
$notices = Start-Process -FilePath (Join-Path $taskRoot 'dist/app/Aion2Presence.exe') -ArgumentList '--write-notices', ('"' + (Join-Path $taskRoot 'dist/app') + '"') -WindowStyle Hidden -Wait -PassThru
if ($notices.ExitCode -ne 0) { throw 'Notice extraction failed' }
Copy-Item -LiteralPath 'dist/app/Aion2Presence.exe' -Destination 'dist/Aion2Presence-Setup.exe' -Force
Copy-Item -LiteralPath 'dist/app/Aion2Presence.exe' -Destination 'dist/Aion2Presence.exe' -Force
if ($PackageRelease) { & ./scripts/Package-Release.ps1 -CargoExe $cargo }
$artifacts = @('dist/Aion2Presence-Setup.exe','dist/Aion2Presence.exe')
if ($PackageRelease) { $artifacts += @('dist/Aion2Presence-Source.zip','dist/Aion2Presence-Windows.zip') }
Get-FileHash -Algorithm SHA256 -LiteralPath $artifacts | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -LiteralPath 'dist/SHA256SUMS.txt'
