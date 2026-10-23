. (Join-Path $PSScriptRoot 'common.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
foreach ($rid in @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')) {
    $extension = if ($rid.StartsWith('linux')) { '.tar.gz' } else { '.zip' }
    $archive = Join-Path $ProjectRoot "artifacts/packages/RVZConverter-$rid$extension"
    $expectedHash = ([IO.File]::ReadAllText($archive + '.sha256').Split(' ')[0]).Trim()
    $hashStream = [IO.File]::OpenRead($archive)
    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $actualHash = [BitConverter]::ToString($hasher.ComputeHash($hashStream)).Replace('-', '') }
    finally { $hashStream.Dispose(); $hasher.Dispose() }
    if ($actualHash -ne $expectedHash) { throw "Archive hash mismatch: $rid" }
    if ($extension -eq '.zip') {
        $zip = [IO.Compression.ZipFile]::OpenRead($archive)
        try {
            $licensePath = $null
            $licenseSuffix = $null
            if ($rid.StartsWith('win')) {
                $rootNames = @($zip.Entries | ForEach-Object { ($_.FullName -split '/')[0] } | Sort-Object -Unique)
                $expectedRoot = @('RVZConverter.exe','README.txt','app','docs','licenses') | Sort-Object
                if (Compare-Object $expectedRoot $rootNames) { throw "Unexpected Windows package root contents: $rid ($($rootNames -join ', '))" }
                if (-not ($zip.Entries | Where-Object { $_.FullName -eq 'RVZConverter.exe' })) { throw "Root launcher missing: $rid" }
                if (-not ($zip.Entries | Where-Object { $_.FullName -eq 'README.txt' })) { throw "Portable README missing: $rid" }
                if (-not ($zip.Entries | Where-Object { $_.FullName -eq 'app/RvzConverter.exe' })) { throw "Application executable missing: $rid" }
                if (-not ($zip.Entries | Where-Object { $_.FullName.StartsWith('docs/') })) { throw "Detailed documentation missing: $rid" }
                if (-not ($zip.Entries | Where-Object { $_.FullName -eq 'licenses/THIRD-PARTY-NOTICES.md' })) { throw "Third-party notices missing: $rid" }
                if (@($zip.Entries | Where-Object { $_.FullName -match '^[^/]+\.dll$' }).Count) { throw "Loose runtime DLL found at package root: $rid" }
                $configPath = 'app/RvzConverter.runtimeconfig.json'
                $licensePath = 'licenses/Microsoft.NETCore.App/LICENSE.TXT'
            }
            else {
                $launcher = @($zip.Entries | Where-Object { $_.FullName -match '/RvzConverter(\.exe)?$' })
                if ($launcher.Count -ne 1) { throw "Expected one launcher: $rid" }
                $mode = ($launcher[0].ExternalAttributes -shr 16) -band 511
                if ($mode -ne 493) { throw "Launcher execute mode is not 0755: $rid" }
                $configPath = ($zip.Entries | Where-Object { $_.FullName.EndsWith('/RvzConverter.runtimeconfig.json') } | Select-Object -First 1).FullName
                $licenseSuffix = '/licenses/Microsoft.NETCore.App/LICENSE.TXT'
            }
            $config = @($zip.Entries | Where-Object { $_.FullName -eq $configPath })
            if ($config.Count -ne 1) { throw "Runtime configuration missing: $rid" }
            $stream = $config[0].Open(); $reader = [IO.StreamReader]::new($stream)
            try { $runtime = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            if ($runtime.runtimeOptions.includedFrameworks[0].name -ne 'Microsoft.NETCore.App') { throw "Self-contained runtime missing: $rid" }
            if (@($zip.Entries | Where-Object { $_.Name -in @('wit','wit.exe','DolphinTool.exe','dolphin-tool','cygwin1.dll') }).Count) { throw "Engine found: $rid" }
            if ($licensePath) { $runtimeLicense = @($zip.Entries | Where-Object { $_.FullName -eq $licensePath }) }
            else { $runtimeLicense = @($zip.Entries | Where-Object { $_.FullName.EndsWith($licenseSuffix) }) }
            if ($runtimeLicense.Count -ne 1) { throw "Runtime license missing: $rid" }
            if ($rid.StartsWith('osx') -and @($zip.Entries | Where-Object { $_.FullName.EndsWith('/Contents/Resources/App.icns') }).Count -ne 1) { throw "macOS icon missing: $rid" }
        } finally { $zip.Dispose() }
    } else {
        $listing = & tar -tvf $archive
        if ($LASTEXITCODE -ne 0) { throw "Could not read tar archive: $rid" }
        if (@($listing | Where-Object { $_ -match '^-rwxr-xr-x .*RVZConverter/RvzConverter$' }).Count -ne 1) { throw "Launcher mode missing from tar: $rid" }
        if (@($listing | Where-Object { $_ -match 'licenses/Microsoft.NETCore.App/LICENSE.TXT$' }).Count -ne 1) { throw "Runtime license missing: $rid" }
    }
    Write-Host "Verified $rid archive: hash, launcher, runtime and notices."
}
