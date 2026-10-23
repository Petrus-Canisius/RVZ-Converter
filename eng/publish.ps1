param([ValidateSet('win-x64','win-arm64','linux-x64','linux-arm64','osx-x64','osx-arm64')][string[]]$RuntimeIdentifier = @('win-x64'), [ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$WithEngines, [switch]$SkipChecks)
. (Join-Path $PSScriptRoot 'common.ps1')
Assert-DotNet10
if (-not $SkipChecks) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration
    & (Join-Path $PSScriptRoot 'test.ps1') -Configuration $Configuration -WithEngines:$WithEngines
}
foreach ($rid in $RuntimeIdentifier) {
    $staging = Assert-ArtifactPath (Join-Path $ProjectRoot ('artifacts/publish/.' + $rid + '-' + [Guid]::NewGuid().ToString('N')))
    $output = Assert-ArtifactPath (Join-Path $ProjectRoot "artifacts/publish/$rid")
    Invoke-CheckedNative 'dotnet' @('publish', (Join-Path $ProjectRoot 'src/RvzConverter.App/RvzConverter.App.csproj'), '-c', $Configuration, '-r', $rid, '--self-contained', 'true', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', '-o', $staging, '--nologo')
    & (Join-Path $PSScriptRoot 'copy-runtime-notices.ps1') -PublishDirectory $staging -RuntimeIdentifier $rid
    foreach ($name in @('README.md','THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $ProjectRoot $name) -Destination (Join-Path $staging $name) }
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'src/RvzConverter.App/Assets/App.ico') -Destination (Join-Path $staging 'App.ico')
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'docs') -Destination (Join-Path $staging 'docs') -Recurse
    if (Test-Path -LiteralPath $output) {
        $backup = Assert-ArtifactPath (Join-Path $ProjectRoot ('artifacts/previous/' + $rid + '-' + [Guid]::NewGuid().ToString('N')))
        [IO.Directory]::CreateDirectory((Split-Path $backup)) | Out-Null
        Move-Item -LiteralPath $output -Destination $backup
    }
    Move-Item -LiteralPath $staging -Destination $output
    Invoke-CheckedNative 'dotnet' @('run', '--project', (Join-Path $PSScriptRoot 'pack/RvzConverter.Pack.csproj'), '-c', 'Release', '--', $output, $rid, (Join-Path $ProjectRoot 'artifacts/packages'))
    Write-Host "Self-contained publish complete: $output. Native runtime validation is a separate check."
}
