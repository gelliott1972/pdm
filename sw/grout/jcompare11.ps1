# James's 25 Nov 22:30-22:31Z check-in (Admin, 27 Nov 2026): the previous version of each model vs his, on copies only:
# per configuration mass, volume and exact bounding box (SwGrout geomcfg), and a STEP of each changed part (SwStep, every
# configuration's default: the active one) for slot and hole sizes. As jcompare.ps1: each set's parts and sub-assemblies are
# opened first so the parents use that set's files by name; Close All between the sets.
# Usage: jcompare11.ps1 -Old <vault root copy> -New <vault root copy> -Out <folder>
param([string]$Old, [string]$New, [string]$Out)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$T = 'C:\code\pdm\tools\sw-macros\bin\SwStep.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$parts = 'APEX\SC08\LS-08X-11\_Shared\08X11-DBW-01.SLDPRT', 'APEX\SC08\LS-08X-11\_Shared\08X11-HBW-04.SLDPRT', 'APEX\SC08\_Shared\08XXX-HBA-01.SLDPRT'
$subs = 'APEX\GNRIC\MDL-ANCHOR-GNRIC.SLDASM', 'APEX\GNRIC\MDL-HEX-BOLT-WASHERS-NUTS-GNRIC.SLDASM', 'APEX\SC08\_Shared\MDL-ROOF-SLEEVE-BOLT-08XXX.SLDASM',
        'APEX\SC08\LS-08X-11\ShaftA\08A11-HBA-01.SLDASM', 'APEX\SC08\LS-08X-11\ShaftA\08A11-HBA-03.SLDASM', 'APEX\SC08\LS-08X-11\_Shared\08X11-HBA-04.SLDASM'
$top = 'APEX\SC08\LS-08X-11\08A11-STA-01.SLDASM'
foreach ($set in @(@('old', $Old), @('new', $New))) {
    & $S close-all --as sw | Out-Null
    foreach ($f in $parts + $subs) { & $S open (Join-Path $set[1] $f) --as sw | Select-String 'Opened|FAILED|REFUSED' | ForEach-Object { $_.Line } }
    foreach ($f in $parts + $subs + @($top)) {
        "=== $($set[0]) $f"
        & $X geomcfg (Join-Path $set[1] $f) 2>&1 | Out-File -Encoding utf8 (Join-Path $Out ("{0}-{1}.txt" -f $set[0], (Split-Path $f -Leaf)))
        Get-Content (Join-Path $Out ("{0}-{1}.txt" -f $set[0], (Split-Path $f -Leaf))) | Select-String '^(OPENED|FAILED|DONE)' | ForEach-Object { $_.Line }
    }
    & $S status --as sw | Select-String 'P:\\' | ForEach-Object { "  loaded from P: " + $_.Line.Trim() } | Select-Object -First 30
}
& $S close-all --as sw | Out-Null
"done"
