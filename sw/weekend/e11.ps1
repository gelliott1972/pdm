# Weekend run, change E (APEX-A124, joint 8.2), 28 Nov 2026: one 11 screen. Lists from e_lists.py (rebuilt positions before).
# STA: the 12 GNRIC-BRA rails and the 24 MDL-80X40-TIE-BOLTS-GNRIC kits out; 12 rails 08X11-BRA-01 and 24 x (M12x65 ISO 4014,
# 2 washers, nut) in, fixed. Subframes: their GNRIC-MSW-01 seats out, 08X11-MSP-01 seats in, fixed (08x11-SFA: in PART 2,
# suppressed in PART 1, as the old seats). New parts and FAST-HEX's new configuration must already be in the copy.
# Usage: e11.ps1 -Root <copy root holding APEX\...> -Lists <folder with sta.tsv and <SFA>.tsv> [-Sta 08A11-STA-01.SLDASM] [-CheckedOut]
param([string]$Root, [string]$Lists, [string]$Sta = '', [switch]$CheckedOut)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'; $SWC = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
$co = @(); if ($CheckedOut) { $co = @('--checked-out') }
function Run([string[]]$argv) { & $SWC close-all --as sw | Out-Null; & $X @argv @co 2>&1 | Select-String '^(DELETED|ACTIVE|SUPPRESSED|COUNT|SAVED|NOT SAVED|SAVE FAILED|PROBLEM|FAILED|REFUSED|COMPONENTS)' | ForEach-Object { '  ' + $_.Line.Substring(0, [Math]::Min(170, $_.Line.Length)) } }
$d = Join-Path $Root 'APEX\SC08\LS-08X-11'
if ($Sta -eq '') { $Sta = '08?11-STA-01.SLDASM' }
$sta = @(Get-ChildItem $d -Filter $Sta)
if ($sta.Count -ne 1) { "PROBLEM: $($sta.Count) STAs match $Sta"; exit 1 }
$sta = $sta[0]
"STA $($sta.Name)"
Run @('delcomp', $sta.FullName, 'GNRIC-BRA.SLDPRT')
Run @('delcomp', $sta.FullName, 'MDL-80X40-TIE-BOLTS-GNRIC.SLDASM')
Run @('addfixed', $sta.FullName, (Join-Path $Lists 'sta.tsv'))
foreach ($t in Get-ChildItem $Lists -Filter '*SFA*.tsv') {
  $name = $t.BaseName + '.SLDASM'
  $f = Get-ChildItem $d -Recurse -Filter $name | Select-Object -First 1
  if ($f -eq $null) { "  PROBLEM no $name"; continue }
  "SFA $($f.FullName.Substring($Root.Length))"
  Run @('delcomp', $f.FullName, 'GNRIC-MSW-01.SLDPRT')
  Run @('addfixed', $f.FullName, $t.FullName)
}
& $SWC close-all --as sw | Out-Null
"done"
