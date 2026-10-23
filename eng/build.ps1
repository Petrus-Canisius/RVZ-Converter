param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-DotNet10
if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'src/RvzConverter.App/Assets/App.ico'))) { & (Join-Path $PSScriptRoot 'create-icon.ps1') }
# Visual Studio developer prompts set the `Platform` environment variable to
# the native CPU architecture. The solution itself is architecture-neutral and
# only has an `Any CPU` solution configuration. Runtime-specific architectures
# are selected later by `dotnet publish -r`.
Invoke-CheckedNative 'dotnet' @('build', (Join-Path $ProjectRoot 'RvzConverter.slnx'), '-c', $Configuration, '-p:Platform=Any CPU', '--nologo')
