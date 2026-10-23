param([ValidateSet('x64','arm64')][string]$Architecture = 'x64')
& (Join-Path $PSScriptRoot 'publish.ps1') -RuntimeIdentifier "linux-$Architecture"
