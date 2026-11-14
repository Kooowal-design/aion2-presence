param([string]$CargoExe = 'cargo', [string]$SourceRef = 'HEAD', [string]$BinaryDirectory = 'dist/app')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    $stage = Join-Path $repo ('dist/p-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    $source = Join-Path $stage 'source'
    $binary = Join-Path $stage 'windows'
    New-Item -ItemType Directory -Path $source,$binary -Force | Out-Null
    $archive = Join-Path $stage 'repository.zip'
    & git archive --format=zip "--output=$archive" $SourceRef
    if ($LASTEXITCODE -ne 0) { throw 'Source archive failed' }
    [IO.Compression.ZipFile]::ExtractToDirectory($archive, $source)
    $upstreamArchive = Join-Path $stage 'upstream.zip'
    & git -C vendor/a2tools archive --format=zip "--output=$upstreamArchive" HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Pinned parser source archive failed' }
    $upstreamSource = Join-Path $source 'vendor/a2tools'
    New-Item -ItemType Directory -Path $upstreamSource -Force | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($upstreamArchive, $upstreamSource, $true)
    $metadata = (& $CargoExe metadata --locked --format-version 1 --manifest-path engine/Cargo.toml) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Cannot collect dependency sources' }
    $cargoHome = if ($env:CARGO_HOME) { $env:CARGO_HOME } else { Join-Path $env:USERPROFILE '.cargo' }
    $crates = Join-Path $source 'dependency-sources'
    New-Item -ItemType Directory -Path $crates -Force | Out-Null
    $expectedChecksums = @{}
    foreach ($block in [regex]::Split([IO.File]::ReadAllText((Join-Path $source 'engine/Cargo.lock')), '(?m)^\[\[package\]\]\s*$')) {
        $nameMatch = [regex]::Match($block, '(?m)^name = "([^"]+)"')
        $versionMatch = [regex]::Match($block, '(?m)^version = "([^"]+)"')
        $checksumMatch = [regex]::Match($block, '(?m)^checksum = "([a-f0-9]{64})"')
        if ($nameMatch.Success -and $versionMatch.Success -and $checksumMatch.Success) {
            $expectedChecksums[$nameMatch.Groups[1].Value + '/' + $versionMatch.Groups[1].Value] = $checksumMatch.Groups[1].Value
        }
    }
    foreach ($package in $metadata.packages | Where-Object { $_.source }) {
        if ($package.source -notlike 'registry+*') { throw "Unsupported dependency source: $($package.source)" }
        $index = Split-Path (Split-Path (Split-Path $package.manifest_path -Parent) -Parent) -Leaf
        $name = "$($package.name)-$($package.version).crate"
        $cached = Join-Path $cargoHome "registry/cache/$index/$name"
        $expected = $expectedChecksums[$package.name + '/' + $package.version]
        if (-not $expected -or (Get-FileHash -LiteralPath $cached -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
            throw "Dependency source does not match Cargo.lock: $name"
        }
        Copy-Item -LiteralPath $cached -Destination (Join-Path $crates $name)
    }
    Get-ChildItem -LiteralPath $crates -Filter '*.crate' | Get-FileHash -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content -Encoding utf8 (Join-Path $crates 'SHA256SUMS.txt')
    @('These are the original Rust crate source archives (tar.gz), not executables.', 'Their versions and registry checksums are pinned in engine/Cargo.lock.', 'The normal build restores dependencies with Cargo; network access may be needed.', 'The complete dependency source is supplied here for inspection and reuse independently of registry availability.') | Set-Content -Encoding utf8 (Join-Path $crates 'README.txt')
    $upstreamRevision = & git -C vendor/a2tools rev-parse HEAD
    $sourceRevision = & git rev-parse $SourceRef
    @("Root source: $sourceRevision", "Parser source: $upstreamRevision", 'Rust dependency source archives: dependency-sources; versions pinned by engine/Cargo.lock.', 'Build: .NET SDK 10, Rust stable, Windows linker; run ./build.ps1. Dependency restore may require network access.', 'The .NET SDK/runtime packs are obtained separately from Microsoft; their notices are included in the binary package.') | Set-Content -Encoding utf8 (Join-Path $source 'SOURCE-MANIFEST.txt')
    Copy-Item -LiteralPath (Join-Path $BinaryDirectory 'Aion2Presence.exe') -Destination (Join-Path $binary 'Aion2Presence.exe')
    foreach ($file in @('LICENSE.txt','NOTICE.md','THIRD-PARTY-NOTICES.txt','SOURCE.txt','release-info.json')) {
        Copy-Item -LiteralPath (Join-Path $BinaryDirectory $file) -Destination (Join-Path $binary $file)
    }
    $sourceZip = Join-Path $stage 'Aion2Presence-Source.zip'
    $windowsZip = Join-Path $stage 'Aion2Presence-Windows.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($source, $sourceZip)
    [IO.Compression.ZipFile]::CreateFromDirectory($binary, $windowsZip)
    Move-Item -LiteralPath $sourceZip -Destination (Join-Path $repo 'dist/Aion2Presence-Source.zip') -Force
    Move-Item -LiteralPath $windowsZip -Destination (Join-Path $repo 'dist/Aion2Presence-Windows.zip') -Force
    Write-Host 'Packaged Windows app and complete project/parser/Rust dependency source.'
} finally { Pop-Location }
