# C18 / APEX-A91 (brief docs\briefs\c18-0811C-braces.md; Admin 25 Nov 2026): LS-08C-11 gets A's two sleeve braces, mirrored
# (x_C = -9881.174 - x_A). Same 08X11-HBW-05 (Grant A19 = A). Steps:
#  1. 08C11-STA-01: every bolt kit's washer-face coincident and Sketch1-line parallel mates on sleeve 08XXX-HBA-01<1> move to
#     the plain sleeve <14> (same y and z, so the same planes); sleeve <1> is about to go.
#  2. 08C11-HBA-01 and -03: spigot 08XXX-HBW-01<6> / <1> replaced by _Shared\08X11-HBW-05 (Replace Components, mates kept).
#  3. 08C11-STA-01: sleeves <19> (x -2995) and <1> (x -6745) replaced by _Shared\08X11-HBA-04, placed by its centre
#     (origin = sleeve x - 640.394, y -1458.66, z -1272.099 as on A), mated as C's sleeves (roof plate on ROOF_PLATE_PLANE,
#     post Right/Front Planes on the spigot's); kits <5> and <1> to BRACED_ROOF_SLEEVE with their slot mate on the post;
#     layout-sketch points tied to the old sleeves re-tied to the post's origin (SwGrout brace).
# Usage: c18.ps1 -Root <folder with APEX\...> -Out <results folder> [-CheckedOut]
param([string]$Root, [string]$Out, [switch]$CheckedOut)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$st = (& $S status) -join "`n"
if ($st -notmatch "`nLock: sw ") { "lock not held by sw; stopped"; exit 2 }
function Stop-If($cond, $why) { if ($cond) { "STOPPED: $why"; & $S close-all --as sw | Out-Null; exit 1 } }
$L = Join-Path $Root 'APEX\SC08\LS-08X-11'
$sta = "$L\08C11-STA-01.SLDASM"; $top = "$L\LS-08C-11+STRUCTURE.SLDASM"; $hba04 = "$L\_Shared\08X11-HBA-04.SLDASM"; $hbw05 = "$L\_Shared\08X11-HBW-05.SLDPRT"
$co = @(); if ($CheckedOut) { $co = @('--checked-out') }
& $S close-all --as sw | Out-Null
"=== before"; & $X xforms $top Default "$Out\before.txt" 2>&1 | Select-String 'POSITIONS|WHATSWRONG' | ForEach-Object { $_.Line }
"=== 1 kit washer/line mates from sleeve 1 to sleeve 14"
$o = & $X movemates $sta 08XXX-HBA-01-1 08XXX-HBA-01-14 'MDL-ROOF-SLEEVE-BOLT-08XXX-*' anyplace types=coincident,parallel @co 2>&1
$o | Set-Content "$Out\1-movemates.txt"; $o | Select-String 'MOVEMATES|SAVED|PROBLEM|SELECT' | ForEach-Object { $_.Line }
Stop-If (-not ($o -match '^SAVED')) 'movemates'
& $S close-all --as sw | Out-Null
"=== 2 spigots"
foreach ($j in @(@('ShaftC\08C11-HBA-01.SLDASM', '08XXX-HBW-01-6'), @('ShaftC\08C11-HBA-03.SLDASM', '08XXX-HBW-01-1'))) {
    # SwGrout replcomp saves only the assembly (SwCmd's save also wants to write HBW-05, which the rebuild marks modified)
    $o = & $X replcomp "$L\$($j[0])" $j[1] $hbw05 'Default<As Machined>' @co 2>&1; $o | Add-Content "$Out\2-replace.txt"
    $o | Select-String 'REPLCOMP|SAVED|FAILED|error|warning' | ForEach-Object { $_.Line }
    Stop-If (-not ($o -match '^SAVED')) "replace in $($j[0])"
    & $S close-all --as sw | Out-Null
}
"=== 3 braced sleeves"
foreach ($j in @(@('08XXX-HBA-01-19', '08C11-HBA-01-1/08X11-HBW-05-6', 'MDL-ROOF-SLEEVE-BOLT-08XXX-5', '-3635.394'), @('08XXX-HBA-01-1', '08C11-HBA-03-1/08X11-HBW-05-1', 'MDL-ROOF-SLEEVE-BOLT-08XXX-1', '-7385.394'))) {
    $o = & $X brace $sta $j[0] $j[1] $j[2] BRACED_ROOF_SLEEVE $hba04 $j[3] -1458.66 -1272.099 @co 2>&1
    $o | Add-Content "$Out\3-brace.txt"
    $o | Select-String 'DELETED|ADDED|MATE\t|KIT|SKETCH|RE-TIED|ORIGIN|BRACE|SAVED|FAILED|error|warning' | ForEach-Object { $_.Line }
    Stop-If (-not ($o -match '^SAVED')) "brace $($j[0])"
    & $S close-all --as sw | Out-Null
}
"=== 4 after"
& $X xforms $top Default "$Out\after.txt" 2>&1 | Select-String 'POSITIONS|WHATSWRONG|warning|error' | ForEach-Object { $_.Line }
$o = & $S rebuild $sta --as sw 2>&1; $o | Select-String 'Rebuilt|warning|error|wrong' | ForEach-Object { $_.Line }
& $X mates $sta > "$Out\sta-mates-after.txt" 2>&1
& $S close-all --as sw | Out-Null
"done"
