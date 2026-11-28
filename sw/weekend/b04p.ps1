# Weekend B redo on P:\ (APEX-A130 / Desk A32), 28 Nov 2026: the same steps as proven on the B2 copies, on the 6 checked-out
# vault files: GNRIC-LM (930 configuration), 08X04-LMW (9 B configurations: +10 legs, GNRIC-LM-1 10 down), 08X04-HBW-04
# (leg 590 -> 600, top unchanged), 08B04-SFA-5B and 08C04-SFA-5B (the 4 GNRIC-MSW-01 bracket distances +10), 08C04-STA-01
# (M12 kit at 08X04-MSW-01). The 08x04-SFA-2B files are NOT changed: with HBW-04 600 long their Coincident26 holds the tie.
param([switch]$Go)
if (-not $Go) { "dry: pass -Go after the 6 files are checked out"; exit 0 }
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'; $SWC = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
function Run([string[]]$argv) { & $SWC close-all --as sw | Out-Null; & $X @argv '--checked-out' 2>&1 | Select-String '^(SET|MATEDIST|SAVED|NOT SAVED|SAVE FAILED|PROBLEM|FAILED|REFUSED)' | ForEach-Object { '  ' + $_.Line.Substring(0, [Math]::Min(170, $_.Line.Length)) } }
& C:\code\pdm\sw\weekend\b04r.ps1 -Root 'P:\' -CheckedOut
"08X04-HBW-04: leg 590 -> 600"
Run @('setdims', 'P:\APEX\SC08\LS-08X-04\_Shared\08X04-HBW-04.SLDPRT', 'Default<As Machined>', 'D2@Sketch1=600')
foreach ($sc in @(@('ShaftB', '08B04'), @('ShaftC', '08C04'))) {
  "$($sc[1])-SFA-5B: GNRIC-MSW-01 distances +10"
  foreach ($m in 'Distance3', 'Distance4', 'Distance5', 'Distance6') { Run @('matedist', "P:\APEX\SC08\LS-08X-04\$($sc[0])\$($sc[1])-SFA-5B.SLDASM", $m, '10') }
}
& $SWC close-all --as sw | Out-Null
"done (then: b04r-proofs.ps1 -Root P:\ -Out <proof> -Screens A,B,C -KitC -CheckedOut)"
