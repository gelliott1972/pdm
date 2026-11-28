# A136 on P:\ (28 Nov 2026): the joint 8.2 parts as proven on the A136 copies. 08X11-BRA-01: tube 438 -> 436, caps 4 -> 5
# (start 218), holes +/-200 -> +/-199 (398 apart, 19 from the tube end, 446 overall unchanged). 08X11-MSP-01: hole 30 -> 31 from
# the post face, material "1.0553 (S355J0)" (SolidWorks DIN Materials). Each 08x11-STA-01: the 96 E bolt-set pieces 1 mm along
# the rail toward its centre (lists move-A/B/C.tsv from a136_list.py). The 5 files must be checked out first.
# Usage: a136p.ps1 -Lists <folder with move-A/B/C.tsv> -Go
param([string]$Lists, [switch]$Go)
if (-not $Go) { "dry: pass -Go after the 5 files are checked out"; exit 0 }
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'; $SWC = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
function Run([string[]]$argv) { & $SWC close-all --as sw | Out-Null; & $X @argv '--checked-out' 2>&1 | Select-String '^(SET|POINT|MATERIAL|CFG|COUNT|SAVED|NOT SAVED|SAVE FAILED|PROBLEM|FAILED|REFUSED)' | ForEach-Object { '  ' + $_.Line.Substring(0, [Math]::Min(170, $_.Line.Length)) } }
$S = 'P:\APEX\SC08\LS-08X-11\_Shared'
"08X11-BRA-01: tube 436, caps 5, holes 398"
Run @('setdims', "$S\08X11-BRA-01.SLDPRT", '*', 'D1@Boss-Extrude1=436', 'D19@Boss-Extrude2=218', 'D1@Boss-Extrude2=5', 'D19@Boss-Extrude3=218', 'D1@Boss-Extrude3=5')
Run @('skpt', "$S\08X11-BRA-01.SLDPRT", 'Sketch4', '200 0', '199 0', '-200 0', '-199 0')
"08X11-MSP-01: hole 31, S355J0"
Run @('skpt', "$S\08X11-MSP-01.SLDPRT", 'Sketch2', '30 0', '31 0')
Run @('setmat', "$S\08X11-MSP-01.SLDPRT", 'SolidWorks DIN Materials', '1.0553 (S355J0)')
foreach ($sh in 'A', 'B', 'C') { "08$($sh)11-STA-01: 96 bolt-set pieces 1 mm"; Run @('movelist', "P:\APEX\SC08\LS-08X-11\08$($sh)11-STA-01.SLDASM", (Join-Path $Lists "move-$sh.tsv")) }
& $SWC close-all --as sw | Out-Null
"done"
