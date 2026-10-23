param([Parameter(Mandatory=$true)][string]$PublishDirectory, [Parameter(Mandatory=$true)][string]$RuntimeIdentifier)
. (Join-Path $PSScriptRoot 'common.ps1')
$assets = Get-Content -LiteralPath (Join-Path $ProjectRoot 'src/RvzConverter.App/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageRoots = @($assets.packageFolders.PSObject.Properties.Name)
$licenseRoot = Join-Path $PublishDirectory 'licenses'
[IO.Directory]::CreateDirectory($licenseRoot) | Out-Null
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'licenses/Avalonia-LICENSE.txt') -Destination $licenseRoot -Force
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'licenses/MicroCom-LICENSE.txt') -Destination $licenseRoot -Force
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'licenses/Tmds-DBus-LICENSE.txt') -Destination $licenseRoot -Force
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    foreach ($packageRoot in $packageRoots) {
        $cache = Join-Path $packageRoot $library.Value.path
        if (-not (Test-Path -LiteralPath $cache)) { continue }
        $files = @(Get-ChildItem -LiteralPath $cache -File | Where-Object { $_.Name -match '(?i)(license|licence|notice|\.nuspec$)' })
        if ($files.Count -gt 0) {
            $target = Join-Path $licenseRoot ($library.Name.Replace('/', '-'))
            [IO.Directory]::CreateDirectory($target) | Out-Null
            foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name) -Force }
        }
        break
    }
}
$runtime = Get-Content -LiteralPath (Join-Path $PublishDirectory 'RvzConverter.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $package = 'microsoft.netcore.app.runtime.' + $RuntimeIdentifier
    $found = $false
    foreach ($packageRoot in $packageRoots) {
        $cache = Join-Path $packageRoot "$package/$($framework.version)"
        if (-not (Test-Path -LiteralPath $cache)) { continue }
        $target = Join-Path $licenseRoot 'Microsoft.NETCore.App'
        [IO.Directory]::CreateDirectory($target) | Out-Null
        foreach ($name in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
            $file = Join-Path $cache $name
            if (-not (Test-Path -LiteralPath $file)) { throw "Missing runtime notice: $file" }
            Copy-Item -LiteralPath $file -Destination (Join-Path $target $name) -Force
        }
        $found = $true; break
    }
    if (-not $found) { throw "Runtime notices not found for $RuntimeIdentifier." }
}
