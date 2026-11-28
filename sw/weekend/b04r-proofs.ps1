# Weekend B redo (Desk A30 / APEX-A130), 28 Nov 2026: after b04r.ps1 and the re-mates, add C-04's M12 kit (if -KitC), then
# the proofs for the listed screens: whole-top xforms, STA STEP (for the every-bolt check), and the floor check.
# Usage: b04r-proofs.ps1 -Root <folder holding LS-08x-04 copies> -Out <proof folder> -Screens A,B,C [-KitC] [-NoFloor]
param([string]$Root, [string]$Out, [string[]]$Screens, [switch]$KitC, [switch]$NoFloor, [switch]$CheckedOut)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'; $T = 'C:\code\pdm\tools\sw-macros\bin\SwStep.exe'; $SWC = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
$co = @(); if ($CheckedOut) { $co = @('--checked-out') }
function Top($sh) { if ($Root -match '^P:') { return 'P:\' } return (Join-Path $Root "LS-08$sh-04") }
if ($KitC) {
  $STA = Join-Path (Top 'C') 'APEX\SC08\LS-08X-04\08C04-STA-01.SLDASM'; $F = Join-Path (Top 'C') '_Shared\_Fasteners'
  "08C04-STA-01: M12 kit at 08X04-MSW-01 (A-04's kit moved -5938 in x)"
  $kit = @(@("$F\FAST-HEX-8.8-HDG.SLDPRT", 'ISO 4017 - M12 x 80-N', '-1 0 0 0 0.38185 -0.92422 0 -0.92422 -0.38185', '1368 -5415 -3320'),
           @("$F\FAST-NUT-8.8-HDG.SLDPRT", 'BS EN 24032 - M12 - W - N', '0 0 1 0 1 0 -1 0 0', '1290.7 -5415 -3320'),
           @("$F\FAST-WSH-8.8-HDG.SLDPRT", 'Washer ISO 7089 - 12', '1 0 0 0 1 0 0 0 1', '1360.5 -5415 -3320'),
           @("$F\FAST-WSH-8.8-HDG.SLDPRT", 'Washer ISO 7089 - 12', '-1 0 0 0 0.17365 0.98481 0 0.98481 -0.17365', '1301.5 -5415 -3320'))
  foreach ($k in $kit) { & $SWC close-all --as sw | Out-Null; & $X addfixed $STA $k[0] $k[1] $k[2] $k[3] @co 2>&1 | Select-String '^(ADDED|PROBLEM|SAVED|NOT SAVED|FAILED|REFUSED|COUNT)' | ForEach-Object { '  ' + $_.Line.Substring(0, [Math]::Min(160, $_.Line.Length)) } }
}
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($sh in $Screens) {
  $d = Join-Path (Top $sh) 'APEX\SC08\LS-08X-04'
  & $SWC close-all --as sw | Out-Null
  & $X xforms "$d\LS-08$sh-04+STRUCTURE.SLDASM" Default "$Out\xforms-after-LS-08$sh-04.txt" 2>&1 | Select-String 'POSITIONS|FAILED' | ForEach-Object { "LS-08$sh-04 " + $_.Line }
  & $SWC close-all --as sw | Out-Null
  & $T "$d\08$($sh)04-STA-01.SLDASM" Default "$Out\08$($sh)04-STA-01-after.STEP" 2>&1 | Select-String 'SAVED|FAIL' | ForEach-Object { "LS-08$sh-04 " + $_.Line }
}
if (-not $NoFloor) { foreach ($sh in $Screens) {
  $d = Join-Path (Top $sh) 'APEX\SC08\LS-08X-04'
  & $SWC close-all --as sw | Out-Null
  & $X floor "$d\08$($sh)04-STA-01.SLDASM" Default 2>&1 | Out-File -Encoding utf8 "$Out\floor-after-$($sh)04.txt"
  "LS-08$sh-04 floor: " + ((Get-Content "$Out\floor-after-$($sh)04.txt" | Select-String '^(LEVEL|LOWEST|DONE)' | ForEach-Object { $_.Line }) -join ' | ')
} }
& $SWC close-all --as sw | Out-Null
"done"
