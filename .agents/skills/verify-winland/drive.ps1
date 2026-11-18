param(
    [string]$Feature,
    [switch]$NoBuild,
    [switch]$Doctor,
    [switch]$Cleanup
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$project = Join-Path $PSScriptRoot 'driver/Winland.Verify.Tests.csproj'
$map = Join-Path $PSScriptRoot 'features'
$results = Join-Path $root 'TestResults'
$self = 'pwsh -NoProfile -File .agents/skills/verify-winland/drive.ps1'

$features = @(Get-ChildItem -LiteralPath $map -Filter '*.md' -File | Where-Object Name -cne 'README.md' | Sort-Object Name | ForEach-Object BaseName)
if ($features.Count -eq 0) {
    throw "No feature file in $map."
}

if ($Doctor) {
    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name = 'winland.exe'" -ErrorAction SilentlyContinue)) {
        $problems.Add("winland.exe $($process.ProcessId) runs from '$($process.ExecutablePath)'. Every driver skips while any Winland runs. Stop it the way it was started: Quit from its tray icon, or scripts/run.ps1 -Stop with the WINLAND_PIPE it was started with. Never stop a Winland you did not start without asking.")
    }
    if ((Get-Process -Id $PID).SessionId -eq 0) {
        $problems.Add('This shell runs in session 0, which has no desktop. Run the drivers from a signed-in desktop session.')
    }
    Push-Location $root
    try {
        $sdk = dotnet --version 2>&1
        if ($LASTEXITCODE -ne 0) { $problems.Add("No .NET SDK matches global.json: $sdk") }
    } finally {
        Pop-Location
    }
    $paths = @(Get-ChildItem -Path (Join-Path $PSScriptRoot 'driver/bin/Release') -Filter 'verify.paths' -Recurse -File -ErrorAction SilentlyContinue)
    $daemon = if ($paths.Count -gt 0) { (Get-Content -LiteralPath $paths[0].FullName -Raw).Trim() } else { '' }
    if (-not $daemon -or -not (Test-Path -LiteralPath $daemon)) {
        $problems.Add("No Release build of the drivers. Run $self -Feature <id> without -NoBuild.")
    } else {
        Write-Output "build: $daemon, $((Get-Item -LiteralPath $daemon).LastWriteTime.ToString('s'))"
    }
    $system = if ($env:WINLAND_VERIFY_SYSTEM -eq '1') { 'run' } else { 'skip, so they fail; set WINLAND_VERIFY_SYSTEM=1 to let them change this user''s install and settings' }
    Write-Output "the install, update and tweaks drivers will $system"
    Write-Output "features: $($features -join ', ')"
    if ($problems.Count -gt 0) {
        $problems | ForEach-Object { Write-Output "doctor: $_" }
        exit 1
    }
    Write-Output 'doctor: ok'
    exit 0
}

if ($Cleanup) {
    $driver = Join-Path $PSScriptRoot 'driver'
    foreach ($process in @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessId -ne $PID -and $_.CommandLine -and $_.CommandLine.Contains($driver, [StringComparison]::OrdinalIgnoreCase) })) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
        Write-Output "stopped driver host $($process.ProcessId) ($($process.Name))"
    }
    $paths = @(Get-ChildItem -Path (Join-Path $driver 'bin/Release') -Filter 'verify.paths' -Recurse -File -ErrorAction SilentlyContinue)
    $daemon = if ($paths.Count -gt 0) { (Get-Content -LiteralPath $paths[0].FullName -Raw).Trim() } else { '' }
    $live = [System.Collections.Generic.HashSet[string]]::new()
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name = 'winland.exe'" -ErrorAction SilentlyContinue)) {
        $run = [regex]::Match([string]$process.CommandLine, 'winland-verify-[0-9a-f]{32}').Value
        if (-not $run) { continue }
        if ($daemon -and $process.ExecutablePath -eq $daemon) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
            Write-Output "stopped driver winland $($process.ProcessId) of $run"
        } else {
            [void]$live.Add($run)
        }
    }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) { $env:DOTNET_ROOT = Split-Path $dotnet.Source -Parent }
    $env:WINLAND_NO_ELEVATE = '1'
    foreach ($folder in @(Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'winland-verify-*' -ErrorAction SilentlyContinue | Where-Object { -not $live.Contains($_.Name) })) {
        $state = Join-Path $folder.FullName 'state.json'
        $config = Join-Path $folder.FullName 'winland.conf'
        if ((Test-Path -LiteralPath $state) -and (Test-Path -LiteralPath $config) -and $daemon -and (Test-Path -LiteralPath $daemon)) {
            $env:WINLAND_PIPE = "winland-verify-cleanup-$([guid]::NewGuid().ToString('n'))"
            $env:WINLAND_STATE = $state
            $recovery = Start-Process -FilePath $daemon -ArgumentList @('--config', "`"$config`"") -WindowStyle Hidden -PassThru
            $deadline = [DateTime]::UtcNow.AddSeconds(20)
            do {
                Start-Sleep -Milliseconds 200
                & $daemon clients *> $null
            } until ($LASTEXITCODE -eq 0 -or $recovery.HasExited -or [DateTime]::UtcNow -gt $deadline)
            & $daemon dispatch exit *> $null
            if (-not $recovery.WaitForExit(20000)) { Stop-Process -Id $recovery.Id -Force }
            Write-Output "put back the windows and taskbar that $($folder.Name) hid"
        }
        Remove-Item -LiteralPath $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
        Write-Output "removed $($folder.FullName)"
    }
    foreach ($run in $live) {
        Write-Output "left $run alone: its winland is not this checkout's build"
    }
    Write-Output 'cleanup: done. TestResults/ is kept.'
    exit 0
}

if ($Feature) {
    if ($Feature -cnotin $features) {
        Write-Output "No feature file .agents/skills/verify-winland/features/$Feature.md. The features are: $($features -join ', ')."
        exit 2
    }
    $features = @($Feature)
}

$env:WINLAND_VERIFY = '1'
if (-not $NoBuild) {
    Write-Output "dotnet build $project"
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Invoke-Driver([string]$id) {
    $name = "feature-$id.trx"
    $report = Join-Path $results $name
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
    dotnet test $project --configuration Release --nologo --no-build --results-directory $results -- --filter "Feature=$id" --report-trx --report-trx-filename $name | Out-Host
    $code = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $report)) {
        Write-Host "The driver for '$id' wrote no test report, so no test has [Trait(""Feature"", ""$id"")]."
        return $false
    }
    [xml]$trx = Get-Content -LiteralPath $report -Raw
    $outcomes = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_ })
    $ran = @($outcomes | Where-Object { $_.outcome -ne 'NotExecuted' })
    if ($ran.Count -eq 0) {
        if ($outcomes.Count -eq 0) {
            Write-Host "The driver for '$id' found no test. Tag its driver class with [Trait(""Feature"", ""$id"")]."
        } else {
            Write-Host "Every test of the driver for '$id' was skipped, so it verified nothing."
            foreach ($result in $outcomes) {
                Write-Host "  $($result.testName): $($result.Output.ErrorInfo.Message)"
            }
        }
        return $false
    }
    return $code -eq 0
}

$failed = [System.Collections.Generic.List[string]]::new()
$flaky = [System.Collections.Generic.List[string]]::new()
foreach ($id in $features) {
    Write-Output "== feature ${id}: $self -Feature $id"
    if (Invoke-Driver $id) {
        Write-Output "== feature ${id}: passed, evidence in TestResults/feature-$id.trx"
        continue
    }
    Write-Output "== feature ${id}: failed, running its driver once more"
    if (Invoke-Driver $id) {
        $flaky.Add($id)
        continue
    }
    $failed.Add($id)
}

foreach ($id in $flaky) {
    Write-Output "FLAKY: the driver for '$id' failed, then passed on a second run. Fix it; a retry is not a pass."
    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Output "::warning title=Flaky feature driver::The driver for '$id' failed, then passed on a second run."
        if ($env:GITHUB_STEP_SUMMARY) {
            Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "- Flaky feature driver: $id failed, then passed on a second run."
        }
    }
}

if ($failed.Count -gt 0) {
    Write-Output "These feature drivers failed: $($failed -join ', ')."
    exit 1
}
Write-Output "Every driver passed: $($features -join ', ')."
