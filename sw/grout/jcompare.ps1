# James's 01:46Z check-ins (Admin, 25 Nov 2026): old vs new versions of the 6 files, on copies only, per configuration:
# mass, volume and exact bounding box (SwGrout geomcfg). For each set the 4 parts are opened first (SwCmd keeps them
# in memory), so the two assemblies use that set's parts by name, not P:\'s; Close All between the sets.
# Usage: jcompare.ps1 -Old <folder> -New <folder> -Out <folder>
param([string]$Old, [string]$New, [string]$Out)
$X = 'C:\code\pdm\tools\sw-macros\bin\SwGrout.exe'
$S = 'C:\code\pdm\tools\SwCmd\bin\SwCmd.exe'
New-Item -ItemType Directory -Force $Out | Out-Null
$parts = 'GNRIC-LM.SLDPRT', '08X06-SFP-03.SLDPRT', '08X06-SFW-04.SLDPRT', '08X06-SFW.SLDPRT'
$asms = '08X06-SFW-01.SLDASM', '08X06-LMW.SLDASM'
foreach ($set in @(@('old', $Old), @('new', $New))) {
    & $S close-all --as sw | Out-Null
    foreach ($f in $parts) { & $S open (Join-Path $set[1] $f) --as sw | Select-String 'Opened|FAILED|REFUSED' | ForEach-Object { $_.Line } }
    foreach ($f in $asms + $parts) {
        "=== $($set[0]) $f"
        & $X geomcfg (Join-Path $set[1] $f) 2>&1 | Tee-Object (Join-Path $Out ("{0}-{1}.txt" -f $set[0], $f)) | Select-String '^(OPENED|FAILED|DONE)' | ForEach-Object { $_.Line }
    }
    & $S status --as sw | Select-String 'P:\\' | ForEach-Object { "  loaded from P: " + $_.Line.Trim() } | Select-Object -First 20
}
& $S close-all --as sw | Out-Null
"done"
