#requires -Version 7.0
[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\release'),
      [string]$Dotnet = 'dotnet', [string]$NuGetConfig, [string]$Commit = $env:GITHUB_SHA)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destination = [IO.Path]::GetFullPath($OutputDirectory)
[xml]$properties = Get-Content -LiteralPath (Join-Path $repository 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
if (-not $Commit) { $Commit = 'local' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$stage = Join-Path $destination ('.publish-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage "SystemTrace-v$version"
[IO.Directory]::CreateDirectory($payload) | Out-Null
Push-Location $repository
try {
    $restoreArguments = @('restore','src/SystemTrace.App/SystemTrace.App.csproj','--runtime','win-x64','--locked-mode','/p:SelfContained=true')
    if ($NuGetConfig) { $restoreArguments += @('--configfile',$NuGetConfig) }
    & $Dotnet @restoreArguments
    if ($LASTEXITCODE -ne 0) { throw 'Portable locked restore failed' }
    & $Dotnet publish src/SystemTrace.App/SystemTrace.App.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output $payload '/p:PublishTrimmed=false' '/p:PublishSingleFile=false' '/p:DebugType=None' '/p:DebugSymbols=false' "/p:SourceRevisionId=$Commit"
    if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed' }
    foreach ($document in @('README.md','README.zh-CN.md','CHANGELOG.md','LICENSE')) {
        Copy-Item -LiteralPath (Join-Path $repository $document) -Destination $payload
    }
    Copy-Item -LiteralPath (Join-Path $repository 'docs') -Destination (Join-Path $payload 'docs') -Recurse
    Copy-Item -LiteralPath (Join-Path $repository 'CONTRIBUTING.md') -Destination $payload
    $assetFile = Get-Content -LiteralPath 'src/SystemTrace.App/obj/project.assets.json' -Raw | ConvertFrom-Json -AsHashtable
    $packageRoot = $assetFile.project.restore.packagesPath
    $packages = [Collections.Generic.Dictionary[string,string]]::new()
    foreach ($entry in $assetFile.libraries.GetEnumerator()) {
        if ($entry.Value.type -eq 'package') { $packages[$entry.Key] = $entry.Value.path }
    }
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $payload 'SystemTrace.App.runtimeconfig.json') -Raw | ConvertFrom-Json
    foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
        $packageId = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
        $packages["$packageId/$($framework.version)"] = "$packageId/$($framework.version)"
    }
    $licenseDirectory = Join-Path $payload 'licenses'
    [IO.Directory]::CreateDirectory($licenseDirectory) | Out-Null
    $packageList = @()
    foreach ($package in $packages.GetEnumerator() | Sort-Object Key) {
        $folder = Join-Path $packageRoot $package.Value
        if (-not (Test-Path -LiteralPath $folder)) { throw "Package provenance missing: $($package.Key)" }
        $name = $package.Key.Replace('/','-')
        $target = Join-Path $licenseDirectory $name
        [IO.Directory]::CreateDirectory($target) | Out-Null
        $notices = @(Get-ChildItem -LiteralPath $folder -File | Where-Object Name -Match '^(licen[cs]e|notice|third.?party|copying|copyright)')
        $spec = @(Get-ChildItem -LiteralPath $folder -Filter '*.nuspec' -File)[0]
        [xml]$metadata = Get-Content -LiteralPath $spec.FullName -Raw
        $licenseNode = $metadata.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='license']")
        if (@($notices | Where-Object Name -Match '^licen[cs]e').Count -eq 0) {
            $fallback = switch ($licenseNode.InnerText) { 'Apache-2.0' { 'Apache-2.0.txt' }; 'MIT' { 'DotNet-MIT.txt' }; default { throw "No included license for $($package.Key)" } }
            Copy-Item -LiteralPath (Join-Path $repository "licenses\$fallback") -Destination $target
        }
        foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination $target }
        Copy-Item -LiteralPath $spec.FullName -Destination $target
        $packageList += [ordered]@{ package = $package.Key; license = $licenseNode.InnerText }
    }
    $packageList | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $licenseDirectory 'packages.json') -Encoding utf8
    $smokePath = Join-Path $stage 'portable-smoke.json'
    & (Join-Path $PSScriptRoot 'Test-Portable.ps1') -Path $payload -SummaryPath $smokePath
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Portable smoke failed' }
    if (@(Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Extension -In @('.pdb','.db','.log')).Count) {
        throw 'Private/debug data in portable payload'
    }
    $buildInfo = [ordered]@{
        version = $version; commit = $Commit; rid = 'win-x64'; selfContained = $true
        sdk = (& $Dotnet --version | Select-Object -Last 1)
        frameworks = $runtimeConfig.runtimeOptions.includedFrameworks
        smoke = (Get-Content -LiteralPath $smokePath -Raw | ConvertFrom-Json)
        packages = $packageList
    }
    $buildInfo | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $payload 'BUILD-INFO.json') -Encoding utf8
    $temporaryZip = Join-Path $stage 'payload.zip'
    [IO.Compression.ZipFile]::CreateFromDirectory($payload, $temporaryZip, [IO.Compression.CompressionLevel]::Optimal, $false)
    $roundtrip = Join-Path $stage 'unpacked'
    [IO.Compression.ZipFile]::ExtractToDirectory($temporaryZip, $roundtrip)
    & (Join-Path $PSScriptRoot 'Test-Portable.ps1') -Path $roundtrip
    Write-Output 'ZIP extraction and runtime-isolated round-trip smoke passed'
    $zip = Join-Path $destination "SystemTrace-v$version-win-x64.zip"
    [IO.File]::Move($temporaryZip, $zip, $true)
    Copy-Item -LiteralPath (Join-Path $payload 'BUILD-INFO.json') -Destination (Join-Path $destination 'BUILD-INFO.json') -Force
    $checksum = @($zip,(Join-Path $destination 'BUILD-INFO.json')) | ForEach-Object {
        (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_)
    }
    [IO.File]::WriteAllLines((Join-Path $destination 'SHA256SUMS.txt'), $checksum)
    Write-Output "Portable package verified: $zip"
}
finally {
    Pop-Location
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    $allowed = $destination.TrimEnd('\') + '\'
    if (-not $resolvedStage.StartsWith($allowed, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($resolvedStage)).StartsWith('.publish-')) { throw 'Unsafe build cleanup path' }
    if (Test-Path -LiteralPath $resolvedStage) { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
}
