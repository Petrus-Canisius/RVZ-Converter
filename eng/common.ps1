$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Invoke-CheckedNative {
    param([Parameter(Mandatory=$true)][string]$Executable, [string[]]$Arguments = @())
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "'$Executable' failed with exit code $LASTEXITCODE." }
}
function Assert-DotNet10 {
    $sdkList = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) { throw 'Could not query .NET SDKs.' }
    if (-not ($sdkList | Where-Object { $_ -match '^10\.' })) { throw 'Install the .NET 10 SDK for the build machine.' }
}
function Assert-ArtifactPath([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $boundary = [IO.Path]::GetFullPath((Join-Path $ProjectRoot 'artifacts')) + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase)) { throw "Artifact path is outside the workspace: $full" }
    return $full
}
