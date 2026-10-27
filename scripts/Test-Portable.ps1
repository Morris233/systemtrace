[CmdletBinding()]
param([Parameter(Mandatory)][string]$Path, [string]$SummaryPath)
$ErrorActionPreference = 'Stop'
$payload = (Resolve-Path -LiteralPath $Path).Path
$executable = Join-Path $payload 'SystemTrace.App.exe'
foreach ($name in @('SystemTrace.App.exe','coreclr.dll','hostfxr.dll','e_sqlite3.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $name))) { throw "Missing portable file: $name" }
}
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('SystemTrace.PortableSmoke.' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch) | Out-Null
$process = $null
$second = $null
try {
    $start = [Diagnostics.ProcessStartInfo]::new($executable)
    $start.UseShellExecute = $false
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = $payload
    $start.Environment['SYSTEMTRACE_DATA_DIR'] = Join-Path $scratch 'data'
    $start.Environment['DOTNET_ROOT'] = Join-Path $scratch 'no-dotnet'
    $start.Environment['DOTNET_ROOT_X64'] = Join-Path $scratch 'no-dotnet'
    $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['PATH'] = "$env:SystemRoot\System32;$env:SystemRoot"
    $start.Environment.Remove('DOTNET_HOST_PATH') | Out-Null
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($start)
    if (-not $process.WaitForInputIdle(20000)) { throw 'Portable window did not respond' }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $process.Refresh()
        if ($process.HasExited) { throw "Portable startup failed: $($process.StandardError.ReadToEnd())" }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 50
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'No portable main window' }
    $startupMilliseconds = $watch.ElapsedMilliseconds
    $loadedRuntime = @($process.Modules | Where-Object ModuleName -EQ 'coreclr.dll')
    if ($loadedRuntime.Count -ne 1 -or -not [string]::Equals($loadedRuntime[0].FileName, (Join-Path $payload 'coreclr.dll'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Portable app did not use its bundled runtime'
    }
    $second = [Diagnostics.Process]::Start($start)
    if (-not $second.WaitForExit(15000) -or $second.ExitCode -ne 0) { throw 'Second instance did not hand off cleanly' }
    $process.Refresh()
    if ($process.HasExited) { throw 'Primary instance closed during handoff' }
    if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(15000)) { throw 'Portable process remained resident' }
    if ($process.ExitCode -ne 0) { throw "Portable exit failed: $($process.StandardError.ReadToEnd())" }
    $databaseCreated = Test-Path -LiteralPath (Join-Path $scratch 'data\system-v1.db')
    if ($databaseCreated) { throw 'Opening the portable application created an inventory database' }
    $result = [ordered]@{ startupMilliseconds = $startupMilliseconds; exitCode = $process.ExitCode;
        bundledRuntime = $true; secondInstanceExitCode = $second.ExitCode; databaseCreated = $false }
    $json = $result | ConvertTo-Json
    if ($SummaryPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($SummaryPath), $json) }
    Write-Output $json
}
finally {
    foreach ($ownedProcess in @($second,$process)) {
        if ($ownedProcess) { if (-not $ownedProcess.HasExited) { $ownedProcess.Kill($true); $ownedProcess.WaitForExit() }; $ownedProcess.Dispose() }
    }
    $resolvedScratch = [IO.Path]::GetFullPath($scratch)
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedScratch.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([IO.Path]::GetFileName($resolvedScratch)).StartsWith('SystemTrace.PortableSmoke.')) { throw 'Unsafe test cleanup path' }
    Remove-Item -LiteralPath $resolvedScratch -Recurse -Force
}
