# LS-08B-11 doubled components, conflict C16 (Desk apex-admin-21, Admin 25 Nov 2026, option A). Kept: the copies that
# something else uses. Sleeve <19> (Interface_Plate_Layout_Sketch drives packers/bolt pattern), sleeve <24> (balloon 6 on
# 08B11-STA-01.SLDDRW), spigot 08XXX-HBW-01<2> (balloon 2 and dimension D14 on 08B11-HBA-01.SLDDRW). Order matters:
#  1. 08B11-STA-01: bolt kit <12>'s two mates on sleeve <16> move to sleeve <19> (same part, same place).
#  1b. 08B11-STA-01: sleeve <24>'s three mates and kit <18>'s one mate on spigot <1> move to spigot <2>.
#  2. 08B11-STA-01: delete bolt kit <17>, sleeve <23> and sleeve <16>; Roof_Plate_Layout_Sketch's relation to <23>'s
#     origin is re-pointed to <24>'s (SwGrout fixrel).
#  3. 08B11-HBA-01: delete spigot 08XXX-HBW-01<1>.
# Positions of LS-08B-11+STRUCTURE before and after (SwGrout xforms).
# Usage: c16.ps1 -Root <folder with APEX\...> -Out <results folder> [-CheckedOut]
param([string]$Root, [string]$Out, [switch]$CheckedOut)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$st = (& $S status) -join "`n"
if ($st -notmatch "`nLock: sw ") { "lock not held by sw; stopped"; exit 2 }
$M = Join-Path $Root 'APEX\SC08\LS-08X-11'
$sta = "$M\08B11-STA-01.SLDASM"; $hba = "$M\ShaftB\08B11-HBA-01.SLDASM"; $top = "$M\LS-08B-11+STRUCTURE.SLDASM"
$co = @(); if ($CheckedOut) { $co = @('--checked-out') }
function Stop-If($cond, $why) { if ($cond) { "STOPPED: $why"; & $S close-all --as sw | Out-Null; exit 1 } }
& $S close-all --as sw | Out-Null
"=== before"; & $X xforms $top Default "$Out\before.txt" 2>&1 | Select-String 'POSITIONS|WHATSWRONG' | ForEach-Object { $_.Line }
"=== 1 move kit 12's mates from sleeve 16 to sleeve 19"
$o = & $X movemates $sta 08XXX-HBA-01-16 08XXX-HBA-01-19 'MDL-ROOF-SLEEVE-BOLT-08XXX-12/' @co 2>&1
$o | Set-Content "$Out\1-movemates.txt"; $o | Select-String 'MATE\t|TARGET|REPLACED|MOVEMATES|SAVED|PROBLEM|error' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(220, $_.Line.Length)) }
Stop-If (-not ($o -match '^SAVED')) 'movemates not saved'
& $S close-all --as sw | Out-Null
"=== 1b move sleeve 24's and kit 18's mates from spigot 1 to spigot 2"
$o = & $X movemates $sta 08B11-HBA-01-1/08XXX-HBW-01-1 08B11-HBA-01-1/08XXX-HBW-01-2 '08XXX-HBA-01-24,MDL-ROOF-SLEEVE-BOLT-08XXX-18/' @co 2>&1
$o | Set-Content "$Out\1b-movemates.txt"; $o | Select-String 'MATE\t|TARGET|REPLACED|SELECT|MOVEMATES|SAVED|PROBLEM|error' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(220, $_.Line.Length)) }
Stop-If (-not ($o -match '^SAVED')) 'movemates 1b not saved'
& $S close-all --as sw | Out-Null
"=== 2 STA deletions"
foreach ($c in 'MDL-ROOF-SLEEVE-BOLT-08XXX-17', '08XXX-HBA-01-23', '08XXX-HBA-01-16') {
    $o = & $S delete $sta $c @co --as sw 2>&1; $o | Add-Content "$Out\2-sta.txt"
    $o | Select-String 'Deleted|warning|error|wrong' | ForEach-Object { $_.Line }
    Stop-If ($o -match '^\s*error') "after deleting $c"
}
"=== 2b re-point Roof_Plate_Layout_Sketch to sleeve 24 (saves the STA when clean)"
$o = & $X fixrel $sta Roof_Plate_Layout_Sketch 08XXX-HBA-01-24 @co 2>&1
$o | Set-Content "$Out\2b-fixrel.txt"; $o | Select-String 'DANGLING|POINT|RELATED|FIXREL|SAVED|error|warning' | ForEach-Object { $_.Line }
Stop-If (-not ($o -match '^SAVED')) 'fixrel not saved'
& $S close-all --as sw | Out-Null
$o = & $S rebuild $sta --as sw 2>&1; $o | Select-String 'Rebuilt|warning|error|wrong' | ForEach-Object { $_.Line }
Stop-If (-not ($o -match "What's wrong: nothing")) 'STA rebuild after fixrel'
& $S close-all --as sw | Out-Null
"=== 3 HBA-01 deletion"
$o = & $S delete $hba '08XXX-HBW-01-1' --save @co --as sw 2>&1; $o | Set-Content "$Out\3-hba.txt"
$o | Select-String 'Deleted|warning|error|wrong|Saved|REFUSED' | ForEach-Object { $_.Line }
Stop-If (-not ($o -match '^Saved')) 'HBA-01 save'
& $S close-all --as sw | Out-Null
"=== 4 after"; & $X xforms $top Default "$Out\after.txt" 2>&1 | Select-String 'POSITIONS|WHATSWRONG|warning|error' | ForEach-Object { $_.Line }
$o = & $S rebuild $sta --as sw 2>&1; $o | Select-String 'Rebuilt|warning|error|wrong' | ForEach-Object { $_.Line }
& $X mates $sta > "$Out\sta-mates-after.txt" 2>&1
& $S close-all --as sw | Out-Null
"done"
