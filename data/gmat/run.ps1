# Runs return_routes.script in GMAT and copies the output CSVs into this folder.
# Usage:  powershell -ExecutionPolicy Bypass -File data\gmat\run.ps1 -Gmat "C:\path\to\gmat-win-R2026a"
# The script is copied into GMAT's output folder first, because GMAT may fail on non-ASCII paths.
# (Keep this file ASCII-only: Windows PowerShell 5.1 misreads UTF-8 without BOM.)
param(
    [string]$Gmat = "$env:USERPROFILE\Downloads\gmat-win-R2026a"
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $Gmat 'output'
$script = Join-Path $out 'return_routes.script'

Copy-Item (Join-Path $here 'return_routes.script') $script -Force
$outputs = 'route_a.csv', 'route_b.csv', 'route_summary.csv'
foreach ($f in $outputs) { Remove-Item (Join-Path $out $f) -ErrorAction SilentlyContinue }

Push-Location (Join-Path $Gmat 'bin')
try { & .\GmatConsole.exe --run $script | Tee-Object -FilePath (Join-Path $out 'return_routes.log') | Select-String -Pattern 'error|converged|exceeded|Mission run|failed' }
finally { Pop-Location }

foreach ($f in $outputs) { Copy-Item (Join-Path $out $f) (Join-Path $here $f) -Force }
Get-Content (Join-Path $here 'route_summary.csv')
