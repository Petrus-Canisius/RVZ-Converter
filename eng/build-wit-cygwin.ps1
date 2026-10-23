param([string]$EngineRoot,[string]$SourceRoot,[string]$SourceAssets)
. (Join-Path $PSScriptRoot 'common.ps1')
$root = Assert-ArtifactPath (Join-Path $ProjectRoot 'artifacts/cygwin')
$downloads = Assert-ArtifactPath (Join-Path $ProjectRoot 'artifacts/cygwin-downloads')
[IO.Directory]::CreateDirectory($downloads) | Out-Null
$setup = Join-Path $downloads 'setup-x86_64.exe'
Invoke-WebRequest 'https://cygwin.com/setup-x86_64.exe' -OutFile $setup
# Setup verifies the Cygwin mirror signature and package checksums. Do not disable
# these checks. Its installer and downloaded repository metadata are recorded too.
$setupHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($setup))).ToLowerInvariant()
$site = 'https://mirrors.kernel.org/sourceware/cygwin/'
$arguments = @('--quiet-mode','unattended','--no-admin','--no-desktop','--no-startmenu','--root',$root,'--local-package-dir',$downloads,'--site',$site,'--packages','gcc-core,make,binutils,gawk,libncurses-devel,zlib-devel,zstd')
$process = Start-Process -FilePath $setup -ArgumentList ($arguments | ForEach-Object { '"' + $_ + '"' }) -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Cygwin setup failed: $($process.ExitCode)" }
$bash = Join-Path $root 'bin/bash.exe'
$project = (Join-Path $SourceRoot 'wit/project').Replace('\','/')
$drive = $project.Substring(0,1).ToLowerInvariant()
$project = '/cygdrive/' + $drive + $project.Substring(2)
Invoke-CheckedNative $bash @('--noprofile','--norc','-c',"export PATH=/usr/bin:/bin; cd '$project'; NO_FUSE=1 STATIC=1 make -j3 wit HAVE_SHA=0 HAVE_MD5=0 CFLAGS='-O2 -fcommon -std=gnu99 -Isrc/libwbfs -Isrc -Isrc/ui -I. -Iwork'")
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
Invoke-WebRequest ($site + 'x86_64/setup.ini') -OutFile (Join-Path $sourceDir 'setup.ini')
$metadata = [IO.File]::ReadAllText((Join-Path $sourceDir 'setup.ini'))
Copy-Item -LiteralPath (Join-Path $root 'etc/setup/installed.db') -Destination $sourceDir
$installed = Get-Content (Join-Path $root 'etc/setup/installed.db')
$records = @()
foreach ($package in @('cygwin','libncurses-devel','zlib-devel')) {
    $installedLine = @($installed | Where-Object { $_.StartsWith($package + ' ') })
    if ($installedLine.Count -ne 1) { throw "Cannot identify installed Cygwin package $package" }
    $archiveName = ($installedLine[0] -split ' ')[1]
    $version = $archiveName.Substring($package.Length + 1) -replace '\.tar\.(xz|zst|bz2|gz)$',''
    $block = [regex]::Match($metadata,'(?ms)^@ ' + [regex]::Escape($package) + '\r?\n(.*?)(?=^@ |\z)').Value
    $versions = [regex]::Matches($block,'(?ms)^version: ([^\r\n]+)\r?\n(.*?)(?=^\[|^version:|\z)')
    $selected = @($versions | Where-Object { $_.Groups[1].Value -eq $version })
    if ($selected.Count -ne 1) { throw "Exact source metadata missing for $package $version" }
    $match = [regex]::Match($selected[0].Value,'(?m)^source: (\S+) (\d+) ([a-fA-F0-9]+)')
    if (-not $match.Success) { throw "Corresponding source unavailable for $package $version" }
    $url = $site + $match.Groups[1].Value
    $file = Join-Path $sourceDir ([IO.Path]::GetFileName($match.Groups[1].Value))
    Invoke-WebRequest $url -OutFile $file
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($file))).ToLowerInvariant()
    if ($actual -ne $match.Groups[3].Value.ToLowerInvariant()) { throw "Cygwin source hash mismatch: $package" }
    $records += @{package=$package;version=$version;url=$url;sha512=$actual}
}
@{installerUrl='https://cygwin.com/setup-x86_64.exe';installerSha256=$setupHash;sourcePackages=$records} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $sourceDir 'INPUTS.json') -Encoding utf8
$notice = Join-Path $EngineRoot 'licenses/engines/Cygwin-dependencies'
[IO.Directory]::CreateDirectory($notice) | Out-Null
foreach ($name in @('cygwin','ncurses','zlib')) {
    $doc = Join-Path $root "usr/share/doc/$name"
    if (-not (Test-Path $doc)) { throw "Installed dependency notices missing: $name" }
    Copy-Item -LiteralPath $doc -Destination $notice -Recurse
}
Invoke-WebRequest 'https://www.gnu.org/licenses/gpl-3.0.txt' -OutFile (Join-Path $notice 'GPL-3.0.txt')
Invoke-WebRequest 'https://www.gnu.org/licenses/lgpl-3.0.txt' -OutFile (Join-Path $notice 'LGPL-3.0.txt')
Set-Content (Join-Path $notice 'SOURCE.txt') -Encoding utf8 -Value 'Exact Cygwin, ncurses and zlib source packages, upstream package checksums and installed versions are included in EngineSources-win-x64.tar.gz. Cygwin setup verifies upstream repository signatures. Locally recorded SHA256 values identify inputs and do not independently authenticate them.'
