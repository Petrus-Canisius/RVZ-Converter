param([Parameter(Mandatory=$true)][string]$Archive)
. (Join-Path $PSScriptRoot 'common.ps1')
function Quote-WindowsArgument([string]$Value) {
    $builder = [Text.StringBuilder]::new()
    [void]$builder.Append([char]0x22)
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq [char]0x5c) { $slashes++; continue }
        if ($character -eq [char]0x22) {
            for ($index = 0; $index -lt (2 * $slashes + 1); $index++) { [void]$builder.Append([char]0x5c) }
            [void]$builder.Append([char]0x22)
            $slashes = 0
            continue
        }
        for ($index = 0; $index -lt $slashes; $index++) { [void]$builder.Append([char]0x5c) }
        $slashes = 0
        [void]$builder.Append($character)
    }
    for ($index = 0; $index -lt (2 * $slashes); $index++) { [void]$builder.Append([char]0x5c) }
    [void]$builder.Append([char]0x22)
    return $builder.ToString()
}
if (-not [Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([Runtime.InteropServices.OSPlatform]::Windows)) {
    Write-Host 'Launcher runtime test skipped. Native Windows is required to launch the package.'
    return
}
$expectedArchitecture = if ($Archive -match 'win-arm64') { 'Arm64' } else { 'X64' }
if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -ne $expectedArchitecture) {
    Write-Host "Launcher runtime test skipped. This machine is $([Runtime.InteropServices.RuntimeInformation]::OSArchitecture), package is $expectedArchitecture."
    return
}
$resolvedArchive = if ([IO.Path]::IsPathRooted($Archive)) { $Archive } else { Join-Path $ProjectRoot $Archive }
$archivePath = [IO.Path]::GetFullPath($resolvedArchive)
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) { throw "Launcher archive not found: $archivePath" }
$unicode = [string]::Concat(' [', [char]0x00E9, ' ', [char]0x65E5, [char]0x672C, [char]0x8A9E, ']')
$testRoot = Assert-ArtifactPath (Join-Path $ProjectRoot ('artifacts/launcher-validation-' + [Guid]::NewGuid().ToString('N') + $unicode))
try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $testRoot
    $launcher = Join-Path $testRoot 'RVZConverter.exe'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw 'The archive has no root launcher.' }
    $workingDirectory = Join-Path $testRoot ('working directory' + $unicode)
    $smokeOutput = Join-Path $testRoot ('smoke output [space &]' + $unicode)
    [IO.Directory]::CreateDirectory($workingDirectory) | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $launcher
    $start.WorkingDirectory = $workingDirectory
    $start.UseShellExecute = $false
    $start.Arguments = (Quote-WindowsArgument '--smoke-ui') + ' ' + (Quote-WindowsArgument $smokeOutput)
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process) { throw 'Windows did not start the launcher.' }
    try {
        if (-not $process.WaitForExit(90000)) { $process.Kill($true); throw 'The launcher smoke test timed out.' }
        if ($process.ExitCode -ne 0) { throw "The launched application returned exit code $($process.ExitCode)." }
    }
    finally { $process.Dispose() }
    if (-not (Test-Path -LiteralPath (Join-Path $smokeOutput 'native-launch.txt') -PathType Leaf)) { throw 'The application did not receive the Unicode smoke-test path from the launcher.' }
    if (Test-Path -LiteralPath (Join-Path $workingDirectory 'native-launch.txt')) { throw 'The application used the launcher working directory instead of the forwarded output path.' }
    Write-Host 'Launcher passed from another working directory with spaces and Unicode arguments.'
}

finally {
    $boundary = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    $full = [IO.Path]::GetFullPath($testRoot)
    if (-not $full.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -or -not (Split-Path -Leaf $full).StartsWith('launcher-validation-', [StringComparison]::Ordinal)) { throw "Refusing to clean outside the launcher test directory: $full" }
    if (Test-Path -LiteralPath $full) { [IO.Directory]::Delete($full, $true) }
}
