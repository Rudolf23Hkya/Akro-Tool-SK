<#
  Checks the picture database against the table definitions in the app (Models/AgeGroups.cs):
  pictures per table, missing positions, and problems in index.csv (unknown rows, values that differ
  from the table, missing files). Uses the built app itself, so the check always matches the code.

  Usage (after building):
    .\tools\check-database.ps1 [-Exe <path to Akro Tool SK.exe>]
  Exit code 0 = complete and no problems, 1 = something to look at.
#>
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\bin\Debug\net8.0-windows\Akro Tool SK.exe')
)
$ErrorActionPreference = 'Stop'

$exePath = (Resolve-Path $Exe).Path
$report = Join-Path (Split-Path $exePath) 'database-check.txt'
if (Test-Path $report) { Remove-Item $report }

$p = Start-Process -FilePath $exePath -ArgumentList '--check-database' -Wait -PassThru
Get-Content $report -Encoding UTF8
exit $p.ExitCode
