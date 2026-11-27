# James's 26 Nov 04:50Z check-in of the LED stairs (Admin, 27 Nov 2026): pin versions (24 Nov 15:39Z) vs his, on copies only,
# per configuration: mass, volume and exact bounding box (SwGrout geomcfg). As jcompare.ps1: each set's parts and
# sub-assemblies are opened first (SwCmd keeps them in memory), so the parents use that set's files by name, not P:\'s;
# Close All between the sets.
# Usage: jcompare07.ps1 -Old <vault root copy> -New <vault root copy> -Out <folder>
param([string]$Old, [string]$New, [string]$Out)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$d = 'APEX\SC08\LS-08X-07'
$parts = '_Shared\08X07-BKP.SLDPRT', '_Shared\08X07-BRP-SHS.SLDPRT', '_Shared\08X07-BRP.SLDPRT', '_Shared\08X07-DBP.SLDPRT', '_Shared\08X07-SFP.SLDPRT'
$subs = 'ShaftA\08A07-SFA-1X.SLDASM', 'ShaftA\08A07-SFA-2X.SLDASM', 'ShaftA\08A07-SFA-3X.SLDASM', 'ShaftA\08A07-SFA-4X.SLDASM',
        'ShaftBC\08Z07-SFA-1X.SLDASM', 'ShaftBC\08Z07-SFA-2X.SLDASM', 'ShaftBC\08Z07-SFA-3X.SLDASM', 'ShaftBC\08Z07-SFA-4X.SLDASM'
$stas = '08A07-STA-01.SLDASM', '08Z07-STA-01.SLDASM'
foreach ($set in @(@('old', $Old), @('new', $New))) {
    & $S close-all --as sw | Out-Null
    foreach ($f in $parts + $subs) { & $S open (Join-Path (Join-Path $set[1] $d) $f) --as sw | Select-String 'Opened|FAILED|REFUSED' | ForEach-Object { $_.Line } }
    foreach ($f in $parts + $subs + $stas) {
        "=== $($set[0]) $f"
        & $X geomcfg (Join-Path (Join-Path $set[1] $d) $f) 2>&1 | Tee-Object (Join-Path $Out ("{0}-{1}.txt" -f $set[0], (Split-Path $f -Leaf))) | Select-String '^(OPENED|FAILED|DONE)' | ForEach-Object { $_.Line }
    }
    & $S status --as sw | Select-String 'P:\\APEX\\SC08\\LS-08X-07' | ForEach-Object { "  loaded from P: " + $_.Line.Trim() } | Select-Object -First 20
}
& $S close-all --as sw | Out-Null
"done"
