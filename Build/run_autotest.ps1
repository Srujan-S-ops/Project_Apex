<#
  Runs ProjectApex.exe in automated test mode: menu → garage → setup → full race (AI autopilot drives
  the player car) → results, capturing screenshots and a JSON report.
  Usage: powershell -File Build\run_autotest.ps1 [-Laps 2] [-Difficulty Hard] [-TimeTrial] [-NoBackend] [-Name run1]
#>
param([int]$Laps = 1, [string]$Difficulty = "Hard", [switch]$TimeTrial, [switch]$NoBackend, [string]$Name = "run", [int]$TimeoutMinutes = 20)
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root "Build\Windows\ProjectApex.exe"
$out = Join-Path $root "Build\autotest\$Name"
New-Item -ItemType Directory -Force $out | Out-Null
$args = @("-apexAutotest", "-apexScreens", "-apexOut", $out, "-apexLaps", $Laps, "-apexDifficulty", $Difficulty,
          "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0", "-logFile", (Join-Path $out "player.log"))
if ($TimeTrial) { $args += "-apexTimeTrial" }
if ($NoBackend) { $args += "-apexNoBackend" }
$p = Start-Process -FilePath $exe -ArgumentList $args -PassThru
if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) { Stop-Process -Id $p.Id -Force; Write-Host "TIMEOUT" -ForegroundColor Red; exit 2 }
Get-Process java -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "*Build\Windows\Backend*" } | Stop-Process -Force
Write-Host "exit code $($p.ExitCode); output in $out"
Get-ChildItem $out | Select-Object Name, Length | Format-Table
