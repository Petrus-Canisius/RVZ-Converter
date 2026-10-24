param([Parameter(Mandatory=$true)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
$SourceRoot = [IO.Path]::GetFullPath($SourceRoot)
$project = Join-Path $SourceRoot 'wit/project'
$mbed = Join-Path $SourceRoot 'dolphin/Externals/mbedtls'
foreach ($required in @((Join-Path $project 'Makefile'),(Join-Path $mbed 'library/sha1.c'),(Join-Path $mbed 'LICENSE'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw 'The pinned WIT and Dolphin source trees are required.' }
}
$destination = Join-Path $project 'rvz-mbedtls'
[IO.Directory]::CreateDirectory($destination) | Out-Null
foreach ($name in @('include','library','LICENSE')) { Copy-Item -LiteralPath (Join-Path $mbed $name) -Destination $destination -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wit-sha1.c') -Destination (Join-Path $project 'src/rvz-sha1.c') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wit-sha1.h') -Destination (Join-Path $project 'src/crypto/rvz-sha1.h') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wit-sha1-config.h') -Destination (Join-Path $project 'src/crypto/rvz-sha1-config.h') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'wit-sha1-selftest.c') -Destination (Join-Path $project 'rvz-sha1-selftest.c') -Force
