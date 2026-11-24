# 0807 floor anchors (APEX-A84, Admin brief 24 Nov 2026): one MDL-ANCHOR-GNRIC kit per GNRIC-PLP floor plate in
# 08A07-STA-01 and 08Z07-STA-01. FP-200x200 -> FLOOR_200X200_M12x185, FP-150X200 -> FLOOR_150X200_M12x185, concrete
# below (nut side up), 14 mm holes. Positions of each +STRUCTURE top before and after (SwGrout xforms).
# Usage: anc07.ps1 -Root <folder with APEX\...> -Out <results folder> -Step before|dry|place|after [-CheckedOut]
#   Root = C:\PDM-work\grout\anc07 for the rehearsal (copies in \a and \bc), P:\ for the live change (STA files checked out).
param([string]$Root, [string]$Out, [string]$Step, [string]$Which = 'a,bc', [switch]$CheckedOut)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$st = (& $S status) -join "`n"
if ($st -notmatch "`nLock: sw ") { "lock not held by sw; stopped"; exit 2 }
$screens = @{ a = @('08A07-STA-01', 'LS-08A-07'); bc = @('08Z07-STA-01', 'LS-08BC-07') }
foreach ($k in $Which.Split(',')) {
    $r = if ($Root -eq 'P:\') { $Root } else { Join-Path $Root $k }
    $kitK = Join-Path $r 'APEX\GNRIC\MDL-ANCHOR-GNRIC.SLDASM'
    $sta = Join-Path $r ('APEX\SC08\LS-08X-07\' + $screens[$k][0] + '.SLDASM')
    $top = Join-Path $r ('APEX\SC08\LS-08X-07\' + $screens[$k][1] + '+STRUCTURE.SLDASM')
    "=== $k  $sta"
    if ($Step -eq 'before' -or $Step -eq 'after') {
        & $X xforms $top Default (Join-Path $Out "$k-$Step.txt") 2>&1 | Select-String 'POSITIONS|WHATSWRONG|error|FAILED' | ForEach-Object { $_.Line }
        continue
    }
    foreach ($j in @(@('FLOOR_200X200_M12x185', 'FP-200x200'), @('FLOOR_150X200_M12x185', 'FP-150X200'))) {
        $args2 = @('placekits', $sta, $kitK, $j[0], 'GNRIC-PLP.SLDPRT', $j[1], 'down', '14')
        if ($Step -eq 'dry') { $args2 += 'dry' }
        if ($CheckedOut) { $args2 += '--checked-out' }
        "--- $($j[1])"
        & $X @args2 2>&1 | Tee-Object -FilePath (Join-Path $Out "$k-$Step-$($j[1]).txt") | Select-String 'KIT\t|PLATES|WOULD|PLACED|PROBLEM|MATE FAILED|PLACEKITS|SAVED|FAILED|REFUSED|SKIP' | ForEach-Object { $_.Line.Substring(0, [Math]::Min(230, $_.Line.Length)) }
        if ($LASTEXITCODE -ne 0) { "STOPPED (exit $LASTEXITCODE)"; exit 1 }
    }
}
& $S close-all --as sw | Select-Object -Last 1
