param([Parameter(Mandatory=$true)][string]$RuntimeIdentifier)
. (Join-Path $PSScriptRoot 'common.ps1')
$extension = if ($RuntimeIdentifier.StartsWith('linux-')) { '.tar.gz' } else { '.zip' }
$archive = Join-Path $ProjectRoot "artifacts/packages/RVZConverter-$RuntimeIdentifier$extension"
$testRoot = Assert-ArtifactPath (Join-Path $ProjectRoot ('artifacts/package-check-' + [Guid]::NewGuid().ToString('N') + ' space é 日本語'))
$working = Join-Path $testRoot 'another working directory'
$package = Join-Path $testRoot 'relocated package é 日本語'
[IO.Directory]::CreateDirectory($package) | Out-Null
[IO.Directory]::CreateDirectory($working) | Out-Null
$report = Join-Path $testRoot 'engine-integration.txt'
$savedPath = $env:PATH
try {
    if ($RuntimeIdentifier.StartsWith('osx-')) { Invoke-CheckedNative 'ditto' @('-x','-k',$archive,$package) }
    elseif ($extension -eq '.zip') { [IO.Compression.ZipFile]::ExtractToDirectory($archive,$package) }
    else { Invoke-CheckedNative 'tar' @('-xzf',$archive,'-C',$package) }
    $entry = if ($RuntimeIdentifier.StartsWith('win-')) { Join-Path $package 'RVZConverter.exe' } elseif ($RuntimeIdentifier.StartsWith('osx-')) { Join-Path $package 'RVZ Converter.app/Contents/MacOS/RvzConverter' } else { Join-Path $package 'RVZConverter' }
    if (-not (Test-Path -LiteralPath $entry)) { throw 'Package entry point missing.' }
    if (-not $RuntimeIdentifier.StartsWith('win-')) {
        Invoke-CheckedNative 'chmod' @('-R','a-w',$package)
        Invoke-CheckedNative 'chmod' @('a+x',$entry)
    }
    # No engine paths are injected. The OS utility paths needed by app launch remain.
    $env:PATH = if ($RuntimeIdentifier.StartsWith('win-')) { "$env:SystemRoot/System32;$env:SystemRoot" } else { '/usr/bin:/bin:/usr/sbin:/sbin' }
    Push-Location $working
    try {
        Invoke-CheckedNative $entry @('--verify-package',$report)
        $visual = Join-Path $testRoot 'native UI'
        if ($RuntimeIdentifier.StartsWith('osx-')) {
            Invoke-CheckedNative '/usr/bin/open' @('-W','-n', (Join-Path $package 'RVZ Converter.app'),'--args','--smoke-ui',$visual)
        } else { Invoke-CheckedNative $entry @('--smoke-ui',$visual) }
        if (-not (Test-Path -LiteralPath (Join-Path $visual 'native-launch.txt')) -or (Test-Path -LiteralPath (Join-Path $visual 'error.txt'))) { throw 'Extracted package UI launch failed.' }
    } finally { Pop-Location }
    Get-Content -LiteralPath $report
    Write-Host "PASS extracted, relocated native package: $RuntimeIdentifier"
} finally {
    $env:PATH = $savedPath
    if (-not $RuntimeIdentifier.StartsWith('win-')) { & chmod -R u+w $package }
    # Keep reports and screenshots as CI validation artifacts. They are ignored by Git.
}
