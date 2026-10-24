param([string]$AssetDirectory = 'artifacts/release', [ValidateSet('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')][string[]]$RuntimeIdentifier = @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64'))
. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$directory = if ([IO.Path]::IsPathRooted($AssetDirectory)) { $AssetDirectory } else { Join-Path $ProjectRoot $AssetDirectory }
foreach ($rid in $RuntimeIdentifier) {
    $extension = if ($rid.StartsWith('linux-')) { '.tar.gz' } else { '.zip' }
    $archive = Join-Path $directory "RVZConverter-$rid$extension"
    if (-not (Test-Path -LiteralPath $archive)) { throw "Missing $rid application archive." }
    $source = Join-Path $directory "EngineSources-$rid.tar.gz"
    if (-not (Test-Path -LiteralPath $source)) { throw "Corresponding source missing: $rid" }
    if ($extension -eq '.zip') {
        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            $names = @($zip.Entries | ForEach-Object FullName)
            $roots = @($names | ForEach-Object { ($_ -split '/')[0] } | Sort-Object -Unique)
            $expected = if ($rid.StartsWith('osx-')) { @('RVZ Converter.app','README.txt') } else { @('RVZConverter.exe','README.txt','app','tools','docs','licenses') }
            if (Compare-Object ($expected | Sort-Object) $roots) { throw "Unexpected archive root: $rid" }
            $prefix = if ($rid.StartsWith('osx-')) { 'RVZ Converter.app/Contents/Resources/' } else { '' }
            if ($names -notcontains ($prefix + 'tools/engines.json')) { throw "Engine manifest missing: $rid" }
            foreach ($required in @('tools/dolphin/','licenses/engines/','docs/')) { if (-not ($names | Where-Object { $_.StartsWith($prefix + $required) })) { throw "$required missing: $rid" } }
            foreach ($required in @('GPL-3.0.txt','ENGINE-MODIFICATIONS.txt','GCC-Runtime-Exception-3.1.txt')) {
                if ($names -notcontains ($prefix + 'licenses/engines/' + $required)) { throw "Required distribution notice missing: $rid/$required" }
            }
            if ($rid.StartsWith('win-') -and ($names | Where-Object { $_ -match '^[^/]+\.dll$' })) { throw 'Loose DLL found at archive root.' }
            $entry = $zip.Entries | Where-Object FullName -eq ($prefix + 'tools/engines.json')
            $reader = [IO.StreamReader]::new($entry.Open()); try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            if ($manifest.runtime -ne $rid) { throw "Engine target mismatch: $rid" }
            if ($manifest.wit.available -ne ($rid -ne 'win-arm64')) { throw "Unexpected WIT availability: $rid" }
            if ($manifest.wit.available -and $manifest.wit.sha1Provider -notlike 'Mbed TLS 2.28.0*') { throw "Stale WIT provider: $rid" }
        } finally { $zip.Dispose() }
    } else {
        $listing = & tar -tf $archive
        if ($LASTEXITCODE -ne 0) { throw "Invalid tar archive: $rid" }
        $roots = @($listing | ForEach-Object { ($_ -split '/')[0] } | Sort-Object -Unique)
        if (Compare-Object (@('RVZConverter','README.txt','app','tools','docs','licenses') | Sort-Object) $roots) { throw "Unexpected Linux archive root: $rid" }
        foreach ($required in @('GPL-3.0.txt','ENGINE-MODIFICATIONS.txt','GCC-Runtime-Exception-3.1.txt')) {
            if ($listing -notcontains ('licenses/engines/' + $required)) { throw "Required distribution notice missing: $rid/$required" }
        }
        $manifestText = & tar -xOf $archive 'tools/engines.json'
        if ($LASTEXITCODE -ne 0) { throw "Engine manifest missing: $rid" }
        $manifest = ($manifestText -join "`n") | ConvertFrom-Json
        if ($manifest.runtime -ne $rid -or -not $manifest.wit.available -or $manifest.wit.sha1Provider -notlike 'Mbed TLS 2.28.0*') { throw "Stale or incompatible engine manifest: $rid" }
        $modes = & tar -tvf $archive
        if ($LASTEXITCODE -ne 0 -or -not ($modes | Where-Object { $_ -match '^-rwxr-xr-x .* RVZConverter$' })) { throw "Executable root entry missing: $rid" }
    }
    $sourceListing = & tar -tf $source
    if ($LASTEXITCODE -ne 0 -or -not ($sourceListing | Where-Object { $_ -eq 'dolphin/COPYING' }) -or -not ($sourceListing | Where-Object { $_ -eq 'wit/project/gpl-2.0.txt' })) { throw "Incomplete engine source: $rid" }
    if ($sourceListing | Where-Object { $_ -match '(^|/)\.git(/|$)|(^|/)(bin|obj)/|wit/project/(gen-ui|rvz-sha1-selftest)$|\.(iso|rvz|gcz|wia|ciso|wbfs|exe|dll|so|dylib|pdb|a|o)$' }) { throw "Private metadata, build output or disc fixture in source asset: $rid" }
    Write-Host "Verified clean $rid layout, engine manifest and corresponding source."
}
