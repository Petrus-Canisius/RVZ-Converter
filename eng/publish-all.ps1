param([switch]$SkipChecks)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'publish.ps1') -RuntimeIdentifier @('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64') -SkipChecks:$SkipChecks
