param([string]$CargoExe = 'cargo', [string]$DotnetExe = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $json = & $CargoExe metadata --locked --format-version 1 --manifest-path engine/Cargo.toml
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read dependency metadata' }
    $metadata = $json | ConvertFrom-Json
    $text = [Text.StringBuilder]::new()
    [void]$text.AppendLine('AION2 Presence - third-party software notices')
    [void]$text.AppendLine('Npcap is not included. Obtain and license it separately at https://npcap.com/.')
    foreach ($package in ($metadata.packages | Sort-Object name,version)) {
        $license = $package.license
        if ($package.name -eq 'a2tools-dps-meter') { $license = 'GPL-3.0 (see LICENSE.txt and NOTICE.md)' }
        if (-not $license) { throw "Missing license metadata: $($package.name)" }
        [void]$text.AppendLine("`n===== $($package.name) $($package.version) | $license =====")
        $directory = Split-Path $package.manifest_path -Parent
        if ($package.name -eq 'a2tools-dps-meter') { $licenses = @(Get-Item (Join-Path $repo 'vendor/a2tools/LICENSE')) }
        elseif ($package.name -eq 'aion-presence-engine') { $licenses = @(Get-Item (Join-Path $repo 'LICENSE')) }
        elseif ($package.name -eq 'valuable' -and $package.version -eq '0.1.1') { $licenses = @(Get-Item (Join-Path $repo 'licenses/valuable-0.1.1-LICENSE')) }
        else { $licenses = @(Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object {
            $_.Name -match '^(LICENSE|LICENCE|COPYING|COPYRIGHT|NOTICE)([.-].*)?$'
        }) }
        if ($licenses.Count -eq 0) { throw "No license text found: $($package.name)" }
        foreach ($file in $licenses) {
            [void]$text.AppendLine("`n--- $($file.Name) ---")
            [void]$text.AppendLine([IO.File]::ReadAllText($file.FullName))
        }
    }
    $nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    foreach ($name in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')) {
        $package = Get-ChildItem -LiteralPath (Join-Path $nuget $name) -Directory | Where-Object { $_.Name -match '^10\.0\.\d+$' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
        if (-not $package) { throw "Restore the .NET 10 Windows runtime pack first: $name" }
        [void]$text.AppendLine("`n===== $name $($package.Name) =====")
        foreach ($file in Get-ChildItem -LiteralPath $package.FullName -File | Where-Object { $_.Name -match '^(LICENSE|THIRD-PARTY-NOTICES)(\.TXT)?$' }) {
            [void]$text.AppendLine("`n--- $($file.Name) ---")
            [void]$text.AppendLine([IO.File]::ReadAllText($file.FullName))
        }
    }
    $generated = Join-Path $repo 'AionPresence/Generated'
    New-Item -ItemType Directory -Path $generated -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $generated 'THIRD-PARTY-NOTICES.txt'), $text.ToString())
    $revision = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { $result = & git rev-parse --verify HEAD 2>$null; if ($LASTEXITCODE -eq 0) { $result } else { 'working-tree' } }
    $sdk = & $DotnetExe --version
    @{revision=$revision; dotnetSdk=$sdk; license='GPL-3.0'; sourceRepository='https://github.com/Kooowal-design/aion2-presence'} | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $generated 'release-info.json')
    Write-Host "Prepared notices for $($metadata.packages.Count) Rust packages and .NET runtime packs."
} finally { Pop-Location }
