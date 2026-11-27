# Weekend run, change B (APEX-A115, 28 Nov 2026): LS-08X-04 floor plates on the floor. For one 04 work copy (or P:\ with
# -CheckedOut, files checked out first): GNRIC-LM gets SPL-250+8M+930-FP1<As Machined> (920 + 10, GNRIC-LMP-20) unless it
# has it; 08X04-LMW's 9 B configurations take the +10 legs (670->680, 170->180, 920->930); the lower subframes then rebuild
# 10 mm down on the longer legs (as 0806's A55). -Kit adds C-04's M12 kit at its MSW-01 (A-04's, 5938 mm along x), fixed.
# Usage: b04.ps1 -Root <folder that holds APEX\...> [-Kit C] [-CheckedOut]
param([string]$Root, [string]$Kit = '', [switch]$CheckedOut)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'; $SWC = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
$co = @(); if ($CheckedOut) { $co = @('--checked-out') }
$LM = Join-Path $Root 'APEX\GNRIC\GNRIC-LM.SLDPRT'
$LMW = Join-Path $Root 'APEX\SC08\LS-08X-04\_Shared\08X04-LMW.SLDASM'
function Run([string[]]$argv) { & $SWC close-all --as sw | Out-Null; & $X @argv @co 2>&1 | Select-String '^(ADDED|SET|GLOBAL|PROP|COMPONENT|SAVED|NOT SAVED|SAVE FAILED|PROBLEM|FAILED|REFUSED)' | ForEach-Object { '  ' + $_.Line.Substring(0, [Math]::Min(170, $_.Line.Length)) } }
$new = 'SPL-250+8M+930-FP1<As Machined>'
$has = (& $X geomcfg $LM 2>&1 | Select-String ([regex]::Escape("CFG`t$new"))) -ne $null
& $SWC close-all --as sw | Out-Null
if (-not $has) {
  "GNRIC-LM: add $new"
  Run @('addcfg', $LM, 'SPL-250+8M+920-FP1<As Machined>', $new)
  Run @('gv', $LM, $new, 'End_Offset=930')
  Run @('setprops', $LM, $new, 'PartNo=GNRIC-LMP-20', 'Description=RHS 40x80x8mm MEMBER x3180mm')
} else { "GNRIC-LM: $new already there" }
$map = [ordered]@{ 'LS-08X-04-LCMM40X80-02B.2'='SPL-250+7M+680-FP1<As Machined>'; 'LS-08X-04-LCMM40X80-02B.3'='SPL-250+7M+680-FP1<As Machined>';
  'LS-08X-04-LCMM40X80-04B.1'='SPL-500+9M+180-FP1<As Machined>'; 'LS-08X-04-LCMM40X80-04B.2'='SPL-500+9M+180-FP1<As Machined>';
  'LS-08X-04-LCMM40X80-04B.3'='SPL-500+9M+180-FP1<As Machined>'; 'LS-08X-04-LCMM40X80-04B.4'='SPL-500+9M+180-FP1<As Machined>';
  'LS-08X-04-LCMM40X80-05B'=$new; 'LS-08X-04-LCMM40X80-06B'=$new; 'LS-08X-04-LCMM40X80-05B.2'=$new }
"08X04-LMW: 9 B configurations"
foreach ($k in $map.Keys) { Run @('setcomp', $LMW, $k, 'GNRIC-LM-1', $map[$k]) }
if ($Kit -eq 'C') {
  $STA = Join-Path $Root 'APEX\SC08\LS-08X-04\08C04-STA-01.SLDASM'; $F = Join-Path $Root '_Shared\_Fasteners'
  "08C04-STA-01: M12 kit at 08X04-MSW-01 (A-04's kit moved -5938 in x; positions after the plates come down)"
  Run @('addfixed', $STA, "$F\FAST-HEX-8.8-HDG.SLDPRT", 'ISO 4017 - M12 x 80-N', '-1 0 0 0 0.38185 -0.92422 0 -0.92422 -0.38185', '1368 -5415 -3320')
  Run @('addfixed', $STA, "$F\FAST-NUT-8.8-HDG.SLDPRT", 'BS EN 24032 - M12 - W - N', '0 0 1 0 1 0 -1 0 0', '1290.7 -5415 -3320')
  Run @('addfixed', $STA, "$F\FAST-WSH-8.8-HDG.SLDPRT", 'Washer ISO 7089 - 12', '1 0 0 0 1 0 0 0 1', '1360.5 -5415 -3320')
  Run @('addfixed', $STA, "$F\FAST-WSH-8.8-HDG.SLDPRT", 'Washer ISO 7089 - 12', '-1 0 0 0 0.17365 0.98481 0 0.98481 -0.17365', '1301.5 -5415 -3320')
}
& $SWC close-all --as sw | Out-Null
"done"
