param([string]$AssetDirectory = 'artifacts/release')
. (Join-Path $PSScriptRoot 'common.ps1')
$directory = if ([IO.Path]::IsPathRooted($AssetDirectory)) { $AssetDirectory } else { Join-Path $ProjectRoot $AssetDirectory }
$assets = @(Get-ChildItem -LiteralPath $directory -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' -and ($_.Name.EndsWith('.zip') -or $_.Name.EndsWith('.tar.gz')) } | Sort-Object Name)
if (-not $assets.Count) { throw 'No release archives found.' }
$lines = foreach ($asset in $assets) {
    $hasher = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($asset.FullName)
    try { $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
    finally { $stream.Dispose(); $hasher.Dispose() }
    "$hash  $($asset.Name)"
}
[IO.File]::WriteAllLines((Join-Path $directory 'SHA256SUMS.txt'),$lines,[Text.Encoding]::ASCII)
Write-Host "Generated SHA256SUMS.txt for $($assets.Count) release assets."
