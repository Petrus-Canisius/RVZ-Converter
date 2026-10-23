param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-DotNet10
if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src/RvzConverter.App/Assets/App.ico'))) { & (Join-Path $PSScriptRoot 'create-icon.ps1') }
Invoke-CheckedNative 'dotnet' @('build', (Join-Path $ProjectRoot 'RvzConverter.slnx'), '-c', $Configuration, '--nologo')
