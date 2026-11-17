param(
    [string]$Configuration = 'Release',
    [switch]$Elevated,
    [switch]$Stop,
    [string]$Config
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dev = Join-Path $env:LOCALAPPDATA 'winland-dev'
$keep = 3

function Resolve-DotnetRoot {
    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    $beside = Split-Path -Parent $dotnet
    if (Test-Path (Join-Path $beside 'shared/Microsoft.NETCore.App')) { return $beside }
    foreach ($line in (& $dotnet --list-runtimes)) {
        if ($line -match '^Microsoft\.NETCore\.App \S+ \[(.+)\]$') {
            $shared = Split-Path -Parent $Matches[1]
            return Split-Path -Parent $shared
        }
    }
    throw 'Could not find a .NET install to set DOTNET_ROOT.'
}

function Get-Winland {
    $found = @(Get-CimInstance Win32_Process -Filter "Name = 'winland.exe'" -ErrorAction SilentlyContinue)
    $pipe = if ([string]::IsNullOrWhiteSpace($env:WINLAND_PIPE)) { '' } else { $env:WINLAND_PIPE }
    $session = (Get-Process -Id $PID).SessionId
    $found = @($found | Where-Object {
        if (-not $_.ExecutablePath) { return $false }
        if (-not $pipe -and $_.SessionId -ne $session) { return $false }
        if ($_.ExecutablePath.StartsWith($dev + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $folder = Split-Path (Split-Path $_.ExecutablePath -Parent) -Parent
            $marker = Join-Path $folder 'pipe'
            return (Test-Path -LiteralPath $marker) -and [IO.File]::ReadAllText($marker) -eq $pipe
        }
        return -not $pipe
    })
    @($found | ForEach-Object { Get-Process -Id $_.ProcessId -ErrorAction SilentlyContinue } | Where-Object { $_ })
}

function Stop-Winland([string]$cli) {
    $running = Get-Winland
    if ($running.Count -eq 0) { return }

    Write-Output "Stopping winland ($(($running | ForEach-Object { $_.Id }) -join ', '))."
    if ($cli -and (Test-Path -LiteralPath $cli)) {
        & $cli dispatch exit *> $null
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    while ((Get-Winland).Count -gt 0 -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }

    $left = Get-Winland
    if ($left.Count -eq 0) { return }

    Write-Warning 'winland did not exit when asked. Stopping it.'
    foreach ($process in $left) {
        try { Stop-Process -Id $process.Id -Force -ErrorAction Stop }
        catch { throw "Could not stop winland (process $($process.Id)): $($_.Exception.Message) Close it by hand, or run this from an elevated shell." }
    }
    Start-Sleep -Milliseconds 500
}

$env:DOTNET_ROOT = Resolve-DotnetRoot

if ($Stop) {
    $cli = Get-ChildItem -Path $dev -Filter winland.exe -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Stop-Winland ($cli.FullName)
    Write-Output 'winland is stopped.'
    return
}

$arguments = @()
if (-not $Elevated) { $arguments += '--no-elevate' }
if ($Config) {
    if (-not $Config.EndsWith('.conf', [StringComparison]::OrdinalIgnoreCase)) {
        throw "--config takes a .conf file, not '$Config'."
    }

    $arguments += @('--config', (Resolve-Path -LiteralPath $Config).Path)
}

$stamp = [DateTime]::Now.ToString('yyyyMMdd-HHmmss')
$build = Join-Path $dev $stamp
New-Item -ItemType Directory -Force -Path $build | Out-Null

Write-Output 'Publishing Winland.Daemon.'
$log = dotnet publish (Join-Path $root 'src/Winland.Daemon/Winland.Daemon.csproj') -c $Configuration -o (Join-Path $build 'winland') --nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) {
    $log | Select-Object -Last 40 | ForEach-Object { Write-Output $_ }
    Remove-Item -LiteralPath $build -Recurse -Force -ErrorAction SilentlyContinue
    throw 'dotnet publish of Winland.Daemon failed. The running winland was left alone.'
}

$exe = Join-Path $build 'winland/winland.exe'
[IO.File]::WriteAllText((Join-Path $build 'pipe'), $(if ([string]::IsNullOrWhiteSpace($env:WINLAND_PIPE)) { '' } else { $env:WINLAND_PIPE }))
Stop-Winland $exe

$stdout = Join-Path $build 'winland.out.log'
$stderr = Join-Path $build 'winland.err.log'

if ($Elevated) {
    Write-Output 'Approve the UAC prompt to start winland.'
    $start = @{ FilePath = $exe; WorkingDirectory = $build; WindowStyle = 'Hidden'; PassThru = $true }
    if ($arguments.Count -gt 0) { $start.ArgumentList = $arguments }
}
else {
    $quoted = ($arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $command = '/c ""{0}" {1} > "{2}" 2> "{3}""' -f $exe, $quoted, $stdout, $stderr
    $start = @{ FilePath = 'cmd.exe'; ArgumentList = $command; WorkingDirectory = $build; WindowStyle = 'Hidden'; PassThru = $true }
}

$process = Start-Process @start

$deadline = [DateTime]::UtcNow.AddSeconds($(if ($Elevated) { 90 } else { 30 }))
$ready = $false
while ([DateTime]::UtcNow -lt $deadline) {
    if ($process.HasExited -and -not $Elevated) { break }
    & $exe clients *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Milliseconds 250
}

if (-not $ready) {
    foreach ($file in @($stdout, $stderr)) {
        if (Test-Path -LiteralPath $file) { Get-Content -LiteralPath $file -Tail 30 | ForEach-Object { Write-Output $_ } }
    }
    throw 'The new winland did not come up.'
}

$pipe = if ([string]::IsNullOrWhiteSpace($env:WINLAND_PIPE)) { '' } else { $env:WINLAND_PIPE }
$active = @(Get-CimInstance Win32_Process -Filter "Name = 'winland.exe'" -ErrorAction SilentlyContinue |
    Where-Object ExecutablePath | ForEach-Object { Split-Path (Split-Path $_.ExecutablePath -Parent) -Parent })
Get-ChildItem -Path $dev -Directory | Where-Object {
    $marker = Join-Path $_.FullName 'pipe'
    $_.Name -ne $stamp -and $_.FullName -notin $active -and
        (Test-Path -LiteralPath $marker) -and [IO.File]::ReadAllText($marker) -eq $pipe
} | Sort-Object Name -Descending |
    Select-Object -Skip ($keep - 1) | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }

Write-Output "winland is running from $exe"
if (-not $Elevated) { Write-Output "Logs: $stdout and $stderr" }
Write-Output "Stop it with: pwsh -NoProfile -File scripts/run.ps1 -Stop"
