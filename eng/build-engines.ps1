param([Parameter(Mandatory=$true)][ValidateSet('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')][string]$RuntimeIdentifier)
. (Join-Path $PSScriptRoot 'common.ps1')
$lock = Get-Content -Raw (Join-Path $PSScriptRoot 'engines.lock.json') | ConvertFrom-Json
$engineRoot = Assert-ArtifactPath (Join-Path $ProjectRoot "artifacts/engines/$RuntimeIdentifier")
if (Test-Path $engineRoot) { throw "Use a fresh engine build directory: $engineRoot" }
$sourceRoot = Assert-ArtifactPath (Join-Path $ProjectRoot 'artifacts/engine-source')
$sourceAssets = Assert-ArtifactPath (Join-Path $ProjectRoot 'artifacts/sources')
[IO.Directory]::CreateDirectory($sourceRoot) | Out-Null
[IO.Directory]::CreateDirectory($sourceAssets) | Out-Null
foreach ($name in @('dolphin','wit')) {
    $item = $lock.$name
    $path = Join-Path $sourceRoot $name
    if (-not (Test-Path (Join-Path $path '.git'))) {
        Invoke-CheckedNative 'git' @('init',$path)
        Invoke-CheckedNative 'git' @('-C',$path,'config','core.autocrlf','false')
        Invoke-CheckedNative 'git' @('-C',$path,'remote','add','origin',$item.repository)
        Invoke-CheckedNative 'git' @('-C',$path,'fetch','--depth','1','origin',$item.revision)
        Invoke-CheckedNative 'git' @('-C',$path,'checkout','--detach','FETCH_HEAD')
    }
    $revision = & git -C $path rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $revision -ne $item.revision) { throw "Unexpected $name source revision." }
}
$dolphin = Join-Path $sourceRoot 'dolphin'
$dolphinPatch = Join-Path $PSScriptRoot 'patches/dolphin-no-debug-paths.patch'
& git -C $dolphin apply --reverse --check $dolphinPatch 2>$null
if ($LASTEXITCODE -ne 0) { Invoke-CheckedNative 'git' @('-C',$dolphin,'apply','--check',$dolphinPatch); Invoke-CheckedNative 'git' @('-C',$dolphin,'apply',$dolphinPatch) }
$witSource = Join-Path $sourceRoot 'wit'
foreach ($patchFile in Get-ChildItem (Join-Path $PSScriptRoot 'patches') -Filter 'wit-*.patch') {
    $patch = $patchFile.FullName
    & git -C $witSource apply --ignore-space-change --reverse --check $patch 2>$null
    if ($LASTEXITCODE -ne 0) { Invoke-CheckedNative 'git' @('-C',$witSource,'apply','--ignore-space-change','--check',$patch); Invoke-CheckedNative 'git' @('-C',$witSource,'apply','--ignore-space-change',$patch) }
}
$submoduleArgs = @('-c','core.longpaths=true','-c','core.autocrlf=false','-C',$dolphin,'submodule','update','--init','--recursive','--depth','1','--jobs','4','--') + @($lock.dolphin.submodules)
Invoke-CheckedNative 'git' $submoduleArgs
$tools = Join-Path $engineRoot 'tools'
$notices = Join-Path $engineRoot 'licenses/engines'
[IO.Directory]::CreateDirectory((Join-Path $tools 'dolphin')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $tools 'wit')) | Out-Null
[IO.Directory]::CreateDirectory($notices) | Out-Null
foreach ($noticeFile in Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'licenses/engines') -File) {
    Copy-Item -LiteralPath $noticeFile.FullName -Destination $notices
}
$build = Assert-ArtifactPath (Join-Path $ProjectRoot "artifacts/engine-build/$RuntimeIdentifier/dolphin")
$cmakeArgs = @('-S',$dolphin,'-B',$build,'-G','Ninja','-DCMAKE_BUILD_TYPE=Release','-DCMAKE_POLICY_VERSION_MINIMUM=3.5','-DENABLE_QT=OFF','-DENABLE_NOGUI=OFF','-DENABLE_TESTS=OFF','-DENABLE_CLI_TOOL=ON','-DENABLE_ANALYTICS=OFF','-DENABLE_AUTOUPDATE=OFF','-DENABLE_VULKAN=OFF','-DENABLE_X11=OFF','-DENABLE_EGL=OFF','-DENABLE_ALSA=OFF','-DENABLE_PULSEAUDIO=OFF','-DENABLE_CUBEB=OFF','-DENABLE_SDL=OFF','-DENABLE_LLVM=OFF','-DENABLE_EVDEV=OFF','-DENABLE_HWDB=OFF','-DUSE_UPNP=OFF','-DUSE_MGBA=OFF','-DUSE_RETRO_ACHIEVEMENTS=OFF','-DUSE_DISCORD_PRESENCE=OFF','-DENCODE_FRAMEDUMPS=OFF','-DUSE_SYSTEM_LIBS=OFF','-DBUILD_SHARED_LIBS=OFF','-DDISTRIBUTOR=RVZConverter')
if ($RuntimeIdentifier.StartsWith('win-')) {
    $processor = if ($RuntimeIdentifier.EndsWith('arm64')) { 'aarch64' } else { 'AMD64' }
    $cmakeArgs += @('-DCMAKE_SYSTEM_NAME=Windows',"-DCMAKE_SYSTEM_PROCESSOR=$processor",'-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded')
    # Upstream's Ninja flag probe uses GCC's -Werror with MSVC and drops
    # /EHsc. Enable the required C++ unwind semantics explicitly.
    $cmakeArgs += @("-DCMAKE_C_FLAGS=/pathmap:$dolphin=.","-DCMAKE_CXX_FLAGS=/EHsc /pathmap:$dolphin=.")
}
else { $cmakeArgs += @("-DCMAKE_C_FLAGS=-ffile-prefix-map=$dolphin=.","-DCMAKE_CXX_FLAGS=-ffile-prefix-map=$dolphin=.") }
if ($RuntimeIdentifier.StartsWith('osx-')) { $cmakeArgs += @('-DCMAKE_OSX_DEPLOYMENT_TARGET=14.0','-DMACOS_CODE_SIGNING=OFF','-DSKIP_POSTPROCESS_BUNDLE=ON','-DUSE_BUNDLED_MOLTENVK=OFF') }
Invoke-CheckedNative 'cmake' $cmakeArgs
Invoke-CheckedNative 'cmake' @('--build',$build,'--target','dolphin-tool','--parallel','2')
$dolphinName = if ($RuntimeIdentifier.StartsWith('win-')) { 'dolphin-tool.exe' } else { 'dolphin-tool' }
$builtDolphin = @(Get-ChildItem -LiteralPath $build -Recurse -File -Filter $dolphinName | Where-Object { $_.DirectoryName -notmatch 'CMakeFiles' })
if ($builtDolphin.Count -ne 1) { throw 'Expected exactly one compiled DolphinTool.' }
$dolphinDest = Join-Path $tools ('dolphin/' + $(if ($RuntimeIdentifier.StartsWith('win-')) { 'DolphinTool.exe' } else { 'dolphin-tool' }))
Copy-Item -LiteralPath $builtDolphin[0].FullName -Destination $dolphinDest
$witAvailable = $RuntimeIdentifier -ne 'win-arm64'
if ($RuntimeIdentifier -eq 'win-x64') {
    & (Join-Path $PSScriptRoot 'build-wit-cygwin.ps1') -EngineRoot $engineRoot -SourceRoot $sourceRoot -SourceAssets $sourceAssets
} elseif ($witAvailable) {
    Invoke-CheckedNative 'bash' @((Join-Path $PSScriptRoot 'build-wit-unix.sh'),(Join-Path $sourceRoot 'wit/project'),(Join-Path $tools 'wit/wit'))
} else {
    Set-Content -LiteralPath (Join-Path $tools 'wit/UNAVAILABLE.txt') -Encoding utf8 -Value 'Native WIT is unavailable on Windows ARM64. Upstream uses POSIX/Cygwin on Windows. Cygwin has no native ARM64 runtime. CISO and WBFS output are unavailable. No emulated executable is bundled.'
}
Copy-Item -LiteralPath (Join-Path $dolphin 'COPYING') -Destination (Join-Path $notices 'Dolphin-GPL-2.0.txt')
Copy-Item -LiteralPath (Join-Path $dolphin 'LICENSES') -Destination (Join-Path $notices 'Dolphin-LICENSES') -Recurse
Copy-Item -LiteralPath (Join-Path $sourceRoot 'wit/project/gpl-2.0.txt') -Destination (Join-Path $notices 'WIT-GPL-2.0.txt')
# Include all source-tree dependency notices, not just the top-level GPL text.
foreach ($file in Get-ChildItem (Join-Path $dolphin 'Externals') -Recurse -File | Where-Object { $_.Name -match '^(COPYING|LICENSE|LICENCE|COPYRIGHT|NOTICE|AUTHORS)([.\-].*)?$' }) {
    $relative = $file.FullName.Substring($dolphin.Length).TrimStart([char[]]'\/')
    $dest = Join-Path $notices ('Dolphin-dependencies/' + $relative)
    [IO.Directory]::CreateDirectory((Split-Path $dest)) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $dest
}
foreach ($file in Get-ChildItem (Join-Path $sourceRoot 'wit/project') -Recurse -File | Where-Object { $_.Name -match '^(COPYING|LICENSE|LICENCE|COPYRIGHT|NOTICE)([.\-].*)?$' }) {
    $relative = $file.FullName.Substring((Join-Path $sourceRoot 'wit').Length).TrimStart([char[]]'\/')
    $dest = Join-Path $notices ('WIT-dependencies/' + $relative)
    [IO.Directory]::CreateDirectory((Split-Path $dest)) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $dest
}
$patchRecords = @(Get-ChildItem (Join-Path $PSScriptRoot 'patches') -File | ForEach-Object { @{name=$_.Name;sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($_.FullName))).ToLowerInvariant()} })
$manifest = @{ runtime=$RuntimeIdentifier; dolphin=@{version=$lock.dolphin.version; revision=$lock.dolphin.revision}; wit=@{version=$lock.wit.version; revision=$lock.wit.revision; available=$witAvailable}; patches=$patchRecords; sourceAssets=@("EngineSources-$RuntimeIdentifier.tar.gz"); redistribution='Separate processes. Engine GPL licenses do not assign the application a license.' }
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $tools 'engines.json') -Encoding utf8
if ($RuntimeIdentifier.StartsWith('linux-')) { Invoke-CheckedNative 'bash' @((Join-Path $PSScriptRoot 'inspect-linux-engines.sh'),$engineRoot,$sourceAssets) }
if ($RuntimeIdentifier.StartsWith('osx-')) { Invoke-CheckedNative 'bash' @((Join-Path $PSScriptRoot 'inspect-macos-engines.sh'),$engineRoot) }
if ($RuntimeIdentifier.StartsWith('win-')) {
    $imports = & dumpbin /dependents $dolphinDest
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect DolphinTool dependencies.' }
    $imports | Set-Content (Join-Path $engineRoot 'dolphin-dependencies.txt')
    # /MT builds must not rely on a separately installed Visual C++ runtime.
    if ($imports -match '(?i)(vcruntime|msvcp)\d.*\.dll') { throw 'DolphinTool unexpectedly requires an external Visual C++ runtime.' }
}
Invoke-CheckedNative $dolphinDest @('convert','--help')
if ($witAvailable) { $witName = if ($RuntimeIdentifier.StartsWith('win-')) { 'wit.exe' } else { 'wit' }; Invoke-CheckedNative (Join-Path $tools "wit/$witName") @('VERSION') }
# Corresponding source is shipped as a release asset, with exact submodules and build scripts.
$material = Join-Path $sourceAssets "material-$RuntimeIdentifier"
[IO.Directory]::CreateDirectory($material) | Out-Null
foreach ($relative in (& git -C $ProjectRoot ls-files eng docs/BUILD-ENGINES.md licenses/engines | Where-Object { $_ -match '^eng/(build-engines\.ps1|build-wit-|inspect-|common\.ps1|engines\.lock\.json|patches/)|^docs/BUILD-ENGINES\.md$|^licenses/engines/' })) {
    $dest = Join-Path $material $relative
    [IO.Directory]::CreateDirectory((Split-Path $dest)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $ProjectRoot $relative) -Destination $dest
}
$lock | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $material 'SOURCE-REVISIONS.json') -Encoding utf8
& git -C $dolphin submodule status --recursive | Set-Content (Join-Path $material 'Dolphin-submodules.txt')
if ($LASTEXITCODE -ne 0) { throw 'Cannot record source submodules.' }
Invoke-CheckedNative 'tar' @('--exclude=.git','--exclude=build','--exclude=bin','--exclude=obj','--exclude=.build-tools','--exclude=wit/project/wit','--exclude=*.o','--exclude=*.d','--exclude=*.exe','--exclude=*.iso','--exclude=*.rvz','--exclude=*.gcz','--exclude=*.wia','--exclude=*.ciso','--exclude=*.wbfs','-czf',(Join-Path $sourceAssets "EngineSources-$RuntimeIdentifier.tar.gz"),'-C',$sourceRoot,'dolphin','wit','-C',$material,'.')
Write-Host "Built native engine distribution: $engineRoot"
