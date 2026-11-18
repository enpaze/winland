$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'check.ps1')

$script:failures = New-Object System.Collections.Generic.List[string]
$script:passed = 0

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $script:passed++
    } catch {
        $script:failures.Add("$Name`: $($_.Exception.Message)")
    }
}

function Assert-Equal($Actual, $Expected) {
    if ($Actual -cne $Expected) { throw "expected '$Expected', got '$Actual'" }
}

Test-Case 'a tracked file that names the banned framework fails the check' {
    $root = Join-Path ([System.IO.Path]::GetTempPath()) ('ban-' + [guid]::NewGuid().ToString('n'))
    New-Item -ItemType Directory -Path $root | Out-Null
    try {
        & git -C $root init -q
        if ($LASTEXITCODE -ne 0) { throw 'git init failed' }
        $token = 'x' + 'unit'
        Set-Content -LiteralPath (Join-Path $root 'sample.cs') -Value "namespace Sample; public class T { public string Name => `"$token`"; }" -NoNewline
        & git -C $root add -- sample.cs
        if ($LASTEXITCODE -ne 0) { throw 'git add failed' }
        $hits = @(Find-BannedFramework $root)
        Assert-Equal $hits.Count 1
        Assert-Equal $hits[0] 'sample.cs'
    } finally {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}

Test-Case 'this repo has no banned framework reference' {
    $hits = @(Find-BannedFramework $repoRoot)
    if ($hits.Count -ne 0) { throw ($hits -join ', ') }
}

if ($script:failures.Count -gt 0) {
    foreach ($failure in $script:failures) { Write-Output "FAIL $failure" }
    Write-Output "$($script:failures.Count) failed, $($script:passed) passed"
    exit 1
}
Write-Output "$($script:passed) check tests passed"
