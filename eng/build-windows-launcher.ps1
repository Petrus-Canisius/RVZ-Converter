param(
    [Parameter(Mandatory=$true)][ValidateSet('win-x64','win-arm64')][string]$RuntimeIdentifier,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$CompilerDirectory = ''
)
. (Join-Path $PSScriptRoot 'common.ps1')

$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $ProjectRoot $OutputPath }
$output = Assert-ArtifactPath $resolvedOutput
$outputDirectory = Split-Path -Parent $output
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$resource = Join-Path $outputDirectory 'RvzConverterLauncher.res'
$object = Join-Path $outputDirectory 'RvzConverterLauncher.obj'
$isWindowsHost = [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)
$compiler = if ($isWindowsHost) { Get-Command cl.exe -ErrorAction SilentlyContinue } else { $null }

if ($compiler) {
    $rc = Get-Command rc.exe -ErrorAction SilentlyContinue
    $link = Get-Command link.exe -ErrorAction SilentlyContinue
    if (-not $rc -or -not $link) { throw 'Visual Studio C++ build tools are incomplete. Activate the matching Developer Command Prompt.' }
    Push-Location $ProjectRoot
    try {
        Invoke-CheckedNative $rc.Source @('/nologo','/fo',$resource,'eng/launcher/RvzConverterLauncher.rc')
        Invoke-CheckedNative $compiler.Source @('/nologo','/O2','/W4','/WX','/EHsc','/MT','/DUNICODE','/D_UNICODE','/DWIN32_LEAN_AND_MEAN','/c','eng/launcher/RvzConverterLauncher.cpp',"/Fo$object")
        $linkOutputArgument = '/OUT:' + $output
        Invoke-CheckedNative $link.Source @('/nologo','/SUBSYSTEM:WINDOWS','/MANIFEST:NO',$linkOutputArgument,$object,$resource,'shell32.lib','user32.lib')
    }
    finally { Pop-Location }
}
else {
    if ([string]::IsNullOrWhiteSpace($CompilerDirectory)) { $CompilerDirectory = $env:RVZ_WINDOWS_TOOLCHAIN_DIR }
    $prefix = if ($RuntimeIdentifier -eq 'win-x64') { 'x86_64-w64-mingw32' } else { 'aarch64-w64-mingw32' }
    $compilerCommand = Get-Command ($prefix + '-g++.exe') -ErrorAction SilentlyContinue
    $compilerPath = if ($CompilerDirectory) { Join-Path $CompilerDirectory ($prefix + '-g++.exe') } elseif ($compilerCommand) { $compilerCommand.Source } else { '' }
    if (-not $compilerPath -or -not (Test-Path -LiteralPath $compilerPath)) { throw "A matching Visual C++ or MinGW compiler is required for $RuntimeIdentifier." }
    $windresCommand = Get-Command ($prefix + '-windres.exe') -ErrorAction SilentlyContinue
    $windresPath = if ($CompilerDirectory) { Join-Path $CompilerDirectory ($prefix + '-windres.exe') } elseif ($windresCommand) { $windresCommand.Source } else { '' }
    if (-not $windresPath -or -not (Test-Path -LiteralPath $windresPath)) {
        if ($RuntimeIdentifier -eq 'win-x64' -and $CompilerDirectory) { $windresPath = Join-Path $CompilerDirectory 'windres.exe' }
    }
    if (-not $windresPath -or -not (Test-Path -LiteralPath $windresPath)) { throw "A matching Windows resource compiler is required for $RuntimeIdentifier." }
    Push-Location $ProjectRoot
    try {
        Invoke-CheckedNative $windresPath @('-i','eng/launcher/RvzConverterLauncher.rc','-o',$resource,'--output-format=coff','-I',$ProjectRoot)
        Invoke-CheckedNative $compilerPath @('-std=c++17','-O2','-static','-static-libgcc','-static-libstdc++','-municode','-mwindows','-DUNICODE','-D_UNICODE','-DWIN32_LEAN_AND_MEAN','-I',$ProjectRoot,'eng/launcher/RvzConverterLauncher.cpp',$resource,'-lshell32','-o',$output)
    }
    finally { Pop-Location }
}

if (-not (Test-Path -LiteralPath $output -PathType Leaf)) { throw 'Launcher compiler did not create the expected executable.' }
Write-Host "Built native $RuntimeIdentifier launcher: $output"
