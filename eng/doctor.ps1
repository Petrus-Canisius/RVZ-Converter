. (Join-Path $PSScriptRoot 'common.ps1')
Assert-DotNet10
Invoke-CheckedNative 'dotnet' @('--info')
Invoke-CheckedNative 'dotnet' @('run', '--project', (Join-Path $ProjectRoot 'tests/RvzConverter.Tests'), '-c', 'Release', '--', '--doctor')
