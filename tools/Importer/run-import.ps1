# run-import.ps1: one screen's import, in order. Stops at the first step that fails.
#   1. export_actions.py   reviewed plan workbook -> action CSVs
#   2. Importer             dry run (checks only)
#   3. Importer --apply     copy, repoint (Document Manager), PartNo, formulas, ledger, re-read, BOM report
#   4. SwFixRefs --apply    repoint the paths SOLIDWORKS itself stores (needs SOLIDWORKS closed)
#   5. SwFixRefs            gate: no stored link outside the vault
# Then run SwCheck on the top assemblies.
# Usage: .\run-import.ps1 -Plan <plan.xlsx> -Source <PackAndGo root> -Screen <LS-08A-06> [-Vault P:\] [-Apply]
param(
    [Parameter(Mandatory)] [string] $Plan,
    [Parameter(Mandatory)] [string] $Source,
    [Parameter(Mandatory)] [string] $Screen,
    [string] $Vault = 'P:\',
    [switch] $Apply
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path "$PSScriptRoot\..\.."
$base = [IO.Path]::ChangeExtension($Plan, $null).TrimEnd('.')
$files, $partnos = "$base-files.csv", "$base-partnos.csv"
$report = "$base-report.md".Replace('-import-plan-report', '-import-report')
$dryReport = $report.Replace('-report.md', '-dryrun-report.md')   # so a later dry run can't overwrite a real run's report
$ledger = Join-Path $repo 'data\rename-ledger.csv'

# Runs a native tool, shows its output (stdout and stderr) as it comes and returns it; stops on a non-zero exit.
# 'Continue' inside: under 'Stop', PowerShell 5.1 turns the tool's first stderr line into a terminating
# NativeCommandError and the tool's own message is lost.
function Step($name, $exe, [string[]] $a) {
    Write-Host "== $name"
    $ErrorActionPreference = 'Continue'
    $out = & $exe @a 2>&1 | ForEach-Object { $s = "$_"; Write-Host $s; $s }
    if ($LASTEXITCODE -ne 0) { Write-Host "STOPPED at: $name (exit $LASTEXITCODE)"; exit 1 }
    $out
}

$null = Step 'Export actions' python @('-I', "$repo\tools\importplan\export_actions.py", $Plan)
$null = Step 'Importer dry run' "$repo\tools\Importer\bin\Importer.exe" @($files, $partnos, $Source, $Vault, $ledger, $Screen, $dryReport)
if (-not $Apply) { Write-Host "Dry run passed. Add -Apply to import."; exit 0 }
if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) { Write-Host 'SOLIDWORKS is open: close it first (step 4 needs it closed).'; exit 1 }
$null = Step 'Importer' "$repo\tools\Importer\bin\Importer.exe" @($files, $partnos, $Source, $Vault, $ledger, $Screen, $report, '--apply')
$fix = Step 'SOLIDWORKS repoint' "$repo\tools\SwFixRefs\bin\SwFixRefs.exe" @($Vault, $files, '--apply')
$gate = Step 'Gate: no links outside the vault' "$repo\tools\SwFixRefs\bin\SwFixRefs.exe" @($Vault, $files)
Add-Content -Encoding UTF8 $report "`n## SOLIDWORKS repoint and gate`n`n- $($fix | Select-Object -Last 1)`n- Gate: $($gate | Select-Object -Last 1)`n"
Write-Host "Import done. Next: SwCheck on the top assemblies. Report: $report"
