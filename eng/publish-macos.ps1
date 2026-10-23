param([ValidateSet('x64','arm64')][string]$Architecture = 'arm64')
& (Join-Path $PSScriptRoot 'publish.ps1') -RuntimeIdentifier "osx-$Architecture"
