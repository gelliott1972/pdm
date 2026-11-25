# APEX-A93 renumber, step R3 (brief docs\briefs\c18-0811C-braces.md, "Renumber first"; 25 Nov 2026): with 08A11-STA-01
# open (all loaded), Save As (not copy) each of the four brace parts to _Shared\08X11-*, parts first, then HBA-04, so
# the open parents follow; PartNo and the file-link expressions (Weight, Material) re-written to the new name (SwGrout
# renprops); parents saved; Close All. The old files stay on disk for Admin's pdm move (R4).
# Usage: renum0811.ps1 -Root <folder holding APEX\...> -Out <results folder> [-Live]
#   -Live: Root = P:\, the parents checked out, new vault files allowed only as "new" rows of <Out>\round.csv.
param([string]$Root, [string]$Out, [switch]$Live)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$st = (& $S status) -join "`n"
if ($st -notmatch "`nLock: sw ") { "lock not held by sw; stopped"; exit 2 }
function Stop-If($cond, $why) { if ($cond) { "STOPPED: $why"; exit 1 } }
$L = Join-Path $Root 'APEX\SC08\LS-08X-11'
$map = @(@('ShaftA\08A11-HBW-04.SLDPRT', '_Shared\08X11-HBW-04.SLDPRT'), @('ShaftA\08A11-DBW-01.SLDPRT', '_Shared\08X11-DBW-01.SLDPRT'),
         @('ShaftA\08A11-HBW-05.SLDPRT', '_Shared\08X11-HBW-05.SLDPRT'), @('ShaftA\08A11-HBA-04.SLDASM', '_Shared\08X11-HBA-04.SLDASM'))
$parents = @("$L\ShaftA\08A11-HBA-01.SLDASM", "$L\ShaftA\08A11-HBA-03.SLDASM", "$L\08A11-STA-01.SLDASM")
$co = @(); $round = @()
if ($Live) {
    $co = @('--checked-out')
    $rows = @('action,old_path,new_path') + ($map | ForEach-Object { 'new,,APEX\SC08\LS-08X-11\' + $_[1] })
    $rows | Set-Content -Encoding utf8 "$Out\round.csv"; $round = @('--round', "$Out\round.csv")
}
& $S close-all --as sw | Out-Null
"=== open 08A11-STA-01"
$o = & $S open "$L\08A11-STA-01.SLDASM" --as sw 2>&1; $o | Select-String 'Opened|wrong|error' | ForEach-Object { $_.Line }
"=== Save As"
foreach ($m in $map) {
    $o = & $S save-as "$L\$($m[0])" "$L\$($m[1])" @round --as sw 2>&1; $o | Add-Content "$Out\saveas.txt"
    $o | Select-String 'Saved as|FAILED|REFUSED|PROBLEM' | ForEach-Object { $_.Line }
    Stop-If (-not ($o -match '^Saved as ')) "save-as $($m[0])"
}
"=== PartNo and file links"
foreach ($m in $map) {
    $old = [IO.Path]::GetFileNameWithoutExtension($m[0]); $new = [IO.Path]::GetFileNameWithoutExtension($m[1])
    $o = & $X renprops "$L\$($m[1])" $old $new @co 2>&1; $o | Add-Content "$Out\renprops.txt"
    $o | Select-String 'SET|MENTIONS|RENPROPS|SAVED|NOTHING|FAILED|REFUSED' | ForEach-Object { $_.Line }
    Stop-If (-not ($o -match '^(SAVED|NOTHING TO CHANGE)')) "renprops $new"
}
"=== save the parents"
foreach ($p in $parents) {
    $o = & $S save $p @co --as sw 2>&1; $o | Add-Content "$Out\parents.txt"
    $o | Select-String 'Saved|Not|REFUSED|PROBLEM' | ForEach-Object { $_.Line }
    Stop-If (-not ($o -match '^Saved')) "save $p"
}
& $S close-all --as sw | Select-Object -Last 1
"=== references (Document Manager)"
foreach ($f in $parents + ($map | ForEach-Object { "$L\$($_[1])" })) {
    $o = & C:\code\pdm\tools\sw-macros\bin\DmRefPaths.exe $f 2>&1
    $o | Add-Content "$Out\refs.txt"
    $old = $o | Select-String '08A11-(HBW-04|HBW-05|DBW-01|HBA-04)'
    "{0}: {1} reference lines, {2} naming an old file" -f [IO.Path]::GetFileName($f), ($o | Measure-Object).Count, ($old | Measure-Object).Count
}
"done"
