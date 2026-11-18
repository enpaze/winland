param(
    [string]$Feature,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'tests/Winland.Smoke.Tests/Winland.Smoke.Tests.csproj'
$map = Join-Path $root 'docs/features.md'
$results = Join-Path $root 'TestResults'

$features = @(Select-String -LiteralPath $map -Pattern '^## ([a-z0-9-]+)\s*$' -CaseSensitive | ForEach-Object { $_.Matches[0].Groups[1].Value })
if ($features.Count -eq 0) {
    throw "No feature heading in $map."
}
if ($Feature) {
    if ($Feature -cnotin $features) {
        Write-Output "docs/features.md has no feature '$Feature'. The features are: $($features -join ', ')."
        exit 2
    }
    $features = @($Feature)
}

$env:WINLAND_SMOKE = '1'
if (-not $NoBuild) {
    Write-Output "dotnet build $project"
    dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Invoke-Driver([string]$id) {
    $name = "feature-$id.trx"
    $report = Join-Path $results $name
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report -Force }
    dotnet test $project --configuration Release --nologo --no-build --filter "Feature=$id" --logger 'console;verbosity=detailed' --logger "trx;LogFileName=$name" --results-directory $results | Out-Host
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
    Write-Output "== feature ${id}: pwsh -NoProfile -File scripts/smoke.ps1 -Feature $id"
    if (Invoke-Driver $id) {
        Write-Output "== feature ${id}: passed"
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
