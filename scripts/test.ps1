param(
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scriptTests = @(Get-ChildItem -Path $PSScriptRoot -Filter '*.tests.ps1' -File | Sort-Object Name)
foreach ($scriptTest in $scriptTests) {
    Write-Output "pwsh $($scriptTest.FullName)"
    pwsh -NoProfile -File $scriptTest.FullName
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
$solution = Join-Path $root 'Winland.slnx'
$projects = @(([xml](Get-Content -LiteralPath $solution -Raw)).SelectNodes('//Project/@Path') | ForEach-Object Value | Where-Object { $_ -like '*.Tests.csproj' } | ForEach-Object { Get-Item -LiteralPath (Join-Path $root $_) } | Sort-Object FullName)
if ($projects.Count -eq 0) {
    throw 'No test project in Winland.slnx.'
}

if (-not $NoBuild) {
    Write-Output "dotnet build $solution"
    dotnet build $solution --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$results = $projects | ForEach-Object -ThrottleLimit ([Math]::Max(2, [Environment]::ProcessorCount)) -Parallel {
    $output = & dotnet test $_.FullName --configuration Release --nologo --no-build 2>&1 | Out-String
    [pscustomobject]@{ Project = $_.FullName; ExitCode = $LASTEXITCODE; Output = $output }
}

$failed = 0
$flaky = [System.Collections.Generic.List[string]]::new()
foreach ($result in $results | Sort-Object Project) {
    Write-Output "dotnet test $($result.Project)"
    Write-Output $result.Output.TrimEnd()
    if ($result.ExitCode -eq 0) { continue }

    $names = @([regex]::Matches($result.Output, '(?m)^\s+Failed (\S+) \[') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    Write-Output "dotnet test $($result.Project) (second run)"
    $again = & dotnet test $result.Project --configuration Release --nologo --no-build 2>&1 | Out-String
    $code = $LASTEXITCODE
    Write-Output $again.TrimEnd()
    if ($code -ne 0) {
        if ($failed -eq 0) { $failed = $code }
        continue
    }

    $listed = if ($names.Count -gt 0) { $names -join ', ' } else { 'a test the output did not name' }
    $flaky.Add("$([System.IO.Path]::GetFileNameWithoutExtension($result.Project)): $listed")
}

foreach ($entry in $flaky) {
    Write-Output "FLAKY: $entry failed, then passed on a second run. Fix it; a retry is not a pass."
    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Output "::warning title=Flaky test::$entry failed, then passed on a second run."
        if ($env:GITHUB_STEP_SUMMARY) {
            Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "- Flaky: $entry failed, then passed on a second run."
        }
    }
}

exit $failed
