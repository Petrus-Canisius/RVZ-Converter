param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$WithEngines, [switch]$Visual)
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-DotNet10
$testArguments = @('run', '--project', (Join-Path $ProjectRoot 'tests/RvzConverter.Tests/RvzConverter.Tests.csproj'), '-c', $Configuration, '--')
if ($WithEngines) { $testArguments += '--engines' }
Invoke-CheckedNative 'dotnet' $testArguments
if ($Visual) {
    $visualDirectory = Join-Path $ProjectRoot ('artifacts/visual-avalonia/smoke-' + [Guid]::NewGuid().ToString('N'))
    Invoke-CheckedNative 'dotnet' @('run', '--project', (Join-Path $ProjectRoot 'src/RvzConverter.App'), '-c', $Configuration, '--', '--smoke-ui', $visualDirectory)
    if (Test-Path -LiteralPath (Join-Path $visualDirectory 'error.txt')) { throw "Native UI smoke check reported an error. Inspect $visualDirectory/error.txt." }
    if (-not (Test-Path -LiteralPath (Join-Path $visualDirectory 'native-launch.txt'))) { throw 'Native UI launch did not complete.' }
}
