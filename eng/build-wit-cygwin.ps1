param([string]$EngineRoot,[string]$SourceRoot,[string]$SourceAssets)
. (Join-Path $PSScriptRoot 'common.ps1')
$EngineRoot = Assert-ArtifactPath $(if ([IO.Path]::IsPathRooted($EngineRoot)) { $EngineRoot } else { Join-Path $ProjectRoot $EngineRoot })
$SourceRoot = Assert-ArtifactPath $(if ([IO.Path]::IsPathRooted($SourceRoot)) { $SourceRoot } else { Join-Path $ProjectRoot $SourceRoot })
$SourceAssets = Assert-ArtifactPath $(if ([IO.Path]::IsPathRooted($SourceAssets)) { $SourceAssets } else { Join-Path $ProjectRoot $SourceAssets })
$root = Assert-ArtifactPath (Join-Path $ProjectRoot 'artifacts/cygwin')
$downloads = Assert-ArtifactPath (Join-Path $ProjectRoot 'artifacts/cygwin-downloads')
[IO.Directory]::CreateDirectory($downloads) | Out-Null
$setup = Join-Path $downloads 'setup-x86_64.exe'
Invoke-WebRequest 'https://cygwin.com/setup-x86_64.exe' -OutFile $setup
# Setup verifies the Cygwin mirror signature and package checksums. Do not disable
# these checks. Its installer and downloaded repository metadata are recorded too.
$setupHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($setup))).ToLowerInvariant()
$pinned = (Get-Content -Raw (Join-Path $PSScriptRoot 'engines.lock.json') | ConvertFrom-Json).cygwin
if ($setupHash -ne $pinned.installerSha256) { throw 'Cygwin installer differs from the reviewed lock file. Review and pin the new input before building.' }
$site = 'https://mirrors.kernel.org/sourceware/cygwin/'
$arguments = @('--quiet-mode','unattended','--no-admin','--no-desktop','--no-startmenu','--root',$root,'--local-package-dir',$downloads,'--site',$site,'--packages','gcc-core,make,binutils,gawk,libncurses-devel,zlib-devel,zstd')
if (-not (Test-Path (Join-Path $root 'bin/bash.exe'))) {
    $process = Start-Process -FilePath $setup -ArgumentList ($arguments | ForEach-Object { '"' + $_ + '"' }) -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Cygwin setup failed: $($process.ExitCode)" }
}
$bash = Join-Path $root 'bin/bash.exe'
$installed = Get-Content (Join-Path $root 'etc/setup/installed.db')
foreach ($property in $pinned.packages.PSObject.Properties) {
    $expectedArchive = $property.Name + '-' + $property.Value
    $line = @($installed | Where-Object { $_.StartsWith($property.Name + ' ') })
    if ($line.Count -ne 1 -or ($line[0] -split ' ')[1] -notmatch ('^' + [regex]::Escape($expectedArchive) + '(-x86_64)?\.tar\.')) {
        throw "Installed Cygwin dependency differs from the reviewed lock file: $($property.Name)."
    }
}
$project = (Join-Path $SourceRoot 'wit/project').Replace('\','/')
$drive = $project.Substring(0,1).ToLowerInvariant()
$project = '/cygdrive/' + $drive + $project.Substring(2)
Invoke-CheckedNative $bash @('--noprofile','--norc','-c',"export PATH=/usr/bin:/bin; cd '$project'; NO_FUSE=1 STATIC=1 make -j3 wit HAVE_SHA=0 HAVE_MD5=0 XDEF=-DOPENSSL_NO_ASM XFLAGS='-fcommon -ffile-prefix-map=$project=.'")
$destination = Join-Path $EngineRoot 'tools/wit'
Copy-Item -LiteralPath (Join-Path $SourceRoot 'wit/project/wit.exe') -Destination (Join-Path $destination 'wit.exe')
Copy-Item -LiteralPath (Join-Path $root 'bin/cygwin1.dll') -Destination (Join-Path $destination 'cygwin1.dll')
$imports = & (Join-Path $root 'bin/objdump.exe') -p (Join-Path $destination 'wit.exe')
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect WIT imports.' }
$dlls = @($imports | Select-String 'DLL Name:' | ForEach-Object { ($_.Line -split 'DLL Name:')[1].Trim() })
foreach ($dll in $dlls) { if ($dll -notmatch '^(?i)(cygwin1|kernel32)\.dll$') { throw "Unexpected WIT dependency requires a redistribution review: $dll" } }
$imports | Set-Content (Join-Path $EngineRoot 'wit-dependencies.txt')
$sourceDir = Join-Path $SourceRoot 'wit/cygwin-corresponding-source'
[IO.Directory]::CreateDirectory($sourceDir) | Out-Null
$cachedMetadata = @(Get-ChildItem -LiteralPath $downloads -Filter setup.ini -Recurse -File)
if ($cachedMetadata.Count -ne 1) { throw 'The exact Cygwin setup metadata is missing or ambiguous.' }
Copy-Item -LiteralPath $cachedMetadata[0].FullName -Destination (Join-Path $sourceDir 'setup.ini') -Force
$metadata = [IO.File]::ReadAllText((Join-Path $sourceDir 'setup.ini'))
Copy-Item -LiteralPath (Join-Path $root 'etc/setup/installed.db') -Destination $sourceDir
$installed = Get-Content (Join-Path $root 'etc/setup/installed.db')
$records = @()
foreach ($package in @('cygwin','libncurses-devel','zlib-devel')) {
    $installedLine = @($installed | Where-Object { $_.StartsWith($package + ' ') })
    if ($installedLine.Count -ne 1) { throw "Cannot identify installed Cygwin package $package" }
    $archiveName = ($installedLine[0] -split ' ')[1]
    $version = $archiveName.Substring($package.Length + 1) -replace '(-x86_64)?\.tar\.(xz|zst|bz2|gz)$',''
    $block = [regex]::Match($metadata,'(?ms)^@ ' + [regex]::Escape($package) + '\r?\n(.*?)(?=^@ |\z)').Value
    $versions = [regex]::Matches($block,'(?ms)^version: ([^\r\n]+)\r?\n(.*?)(?=^\[|^version:|\z)')
    $selected = @($versions | Where-Object { $_.Groups[1].Value -eq $version })
    if ($selected.Count -ne 1) { throw "Exact source metadata missing for $package $version" }
    $match = [regex]::Match($selected[0].Value,'(?m)^source: (\S+) (\d+) ([a-fA-F0-9]+)')
    if (-not $match.Success) { throw "Corresponding source unavailable for $package $version" }
    $url = $site + $match.Groups[1].Value
    $file = Join-Path $sourceDir ([IO.Path]::GetFileName($match.Groups[1].Value))
    if (-not (Test-Path -LiteralPath $file)) { Invoke-CheckedNative 'curl.exe' @('--fail','--location','--retry','3','--connect-timeout','30','--max-time','600',$url,'--output',$file) }
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($file))).ToLowerInvariant()
    if ($actual -ne $match.Groups[3].Value.ToLowerInvariant()) { throw "Cygwin source hash mismatch: $package" }
    $records += @{package=$package;version=$version;url=$url;sha512=$actual}
}
@{installerUrl='https://cygwin.com/setup-x86_64.exe';installerSha256=$setupHash;sourcePackages=$records} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $sourceDir 'INPUTS.json') -Encoding utf8
$notice = Join-Path $EngineRoot 'licenses/engines/Cygwin-dependencies'
[IO.Directory]::CreateDirectory($notice) | Out-Null
foreach ($name in @('cygwin','ncurses')) {
    $doc = Join-Path $root "usr/share/doc/$name"
    if (-not (Test-Path $doc)) { throw "Installed dependency notices missing: $name" }
    Copy-Item -LiteralPath $doc -Destination $notice -Recurse -Force
}
foreach ($name in @('cygwin','zlib')) {
    $package = if ($name -eq 'zlib') { 'zlib-devel' } else { 'cygwin' }
    $record = $records | Where-Object { $_.package -eq $package }
    $archive = Join-Path $sourceDir ([IO.Path]::GetFileName(([Uri]$record.url).AbsolutePath))
    $unpacked = Join-Path $sourceDir "unpack-$name"
    [IO.Directory]::CreateDirectory($unpacked) | Out-Null
    Invoke-CheckedNative 'tar' @('-xf',$archive,'-C',$unpacked)
    foreach ($nested in @(Get-ChildItem -LiteralPath $unpacked -Recurse -File | Where-Object { $_.Name -match '\.tar\.(xz|gz|bz2|zst)$' })) { Invoke-CheckedNative 'tar' @('-xf',$nested.FullName,'-C',$unpacked) }
    $licenseFiles = @(Get-ChildItem -LiteralPath $unpacked -Recurse -File | Where-Object { $_.Name -match '^(COPYING|LICENSE|LICENCE|COPYRIGHT|README)([.\-].*)?$' })
    if (-not $licenseFiles.Count) { throw "Dependency license texts not found: $name" }
    foreach ($file in $licenseFiles) { $dest = Join-Path $notice ($name + '/' + $file.FullName.Substring($unpacked.Length).TrimStart([char[]]'\/')); [IO.Directory]::CreateDirectory((Split-Path $dest)) | Out-Null; Copy-Item -LiteralPath $file.FullName -Destination $dest -Force }
}
Set-Content (Join-Path $notice 'SOURCE.txt') -Encoding utf8 -Value 'Exact Cygwin, ncurses and zlib source packages, upstream package checksums and installed versions are included in EngineSources-win-x64.tar.gz. Cygwin setup verifies upstream repository signatures. Locally recorded SHA256 values identify inputs and do not independently authenticate them.'
