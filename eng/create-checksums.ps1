$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageDirectory = Join-Path $repoRoot 'artifacts/packages'
$packageNames = @(
    'RVZConverter-win-x64.zip'
    'RVZConverter-win-arm64.zip'
    'RVZConverter-linux-x64.tar.gz'
    'RVZConverter-linux-arm64.tar.gz'
    'RVZConverter-osx-x64.zip'
    'RVZConverter-osx-arm64.zip'
)

$lines = foreach ($name in $packageNames) {
    $path = Join-Path $packageDirectory $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing release package: $path"
    }

    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($path)
        try {
            $hash = (($hasher.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) -join '')
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $hasher.Dispose()
    }
    "$hash  $name"
}

Set-Content -LiteralPath (Join-Path $packageDirectory 'SHA256SUMS.txt') -Value $lines -Encoding ascii
Write-Output "Wrote SHA256SUMS.txt for $($packageNames.Count) release packages."
