# plates-0804 (brief docs\briefs\plates-0804.md): SW's run, on a rehearsal copy (default) or on P:\ (-Real -Go).
#   -Stage 1: SOLIDWORKS under the lock: PLP-32/-33 configurations, six weldment-only parts (save-as + delete-feature),
#             six assemblies (new-asm + place), six SFAs (swap-asm). Only the planned files may be written: the other
#             writable files in the vault (the six old parts on P:\: checked out) are made read-only for the stage, and
#             every file's write time is compared after.
#   -Stage 2: checks (no saves): masses old part vs new assembly; PartNo properties (DmEdit batch); probe.
#             SwCheck needs SOLIDWORKS closed, so it is run separately (Stage 3).
#   -Stage 3: SwCheck of the six SFAs and the three STA-01s (SOLIDWORKS must be closed).
#   -Stage 4 (-Real only): pdm unlock the six old parts (unchanged), pdm load (--dry first), pdm remove them (--dry first).
# Stops at the first failure. Logs to -Log.
param([Parameter(Mandatory)] [int] $Stage, [switch] $Real, [switch] $Go, [string] $V = "C:\PDM-work\plates-0804\rehearsal",
      [string] $Log = "C:\code\pdm\sw\plates-0804-rehearsal", [string] $From = "", [int] $FromPlace = 1)
# -From <old part, e.g. 08B04-SFP-02> -FromPlace <n>: resume stage 1 after a stop. PLP configurations and the jobs before
# <old part> are skipped (done); in that job, save-as/delete-feature and new-asm are skipped if their file exists, and
# placing starts at plate <n>. Logs of a resumed run get the suffix "-r".
$repo = "C:\code\pdm"; $T = "$repo\tools"; $S = "$T\SwCmd\bin\SwCmd.exe"; $round = "$repo\scratch\import\plates-0804\round.csv"
$Pdm = "$env:LOCALAPPDATA\PDM\current\pdm.exe"
if ($Real) {
    if (-not $Go) { "Not started: -Real needs -Go (Admin's 'network OK, go' and a passed gate)."; exit 2 }
    $V = "P:\"; $Log = "$repo\sw\plates-0804-real"
}
$Vb = $V.TrimEnd('\'); $L = "$Vb\APEX\SC08\LS-08X-04"
New-Item -ItemType Directory -Force $Log | Out-Null
$w = @('--as', 'sw'); if ($Real) { $w += '--checked-out' }
$rnd = @(); if ($Real) { $rnd = @('--round', $round) }

function Step($name, [string[]] $a, [int[]] $ok = @(0), $exe = $S) {
    $f = "$Log\s$Stage-" + ($name -replace '[^A-Za-z0-9]+', '-') + ".txt"
    $t0 = Get-Date
    & $exe @a > $f 2>&1; $code = $LASTEXITCODE
    Write-Host ("$name : exit $code, $([int]((Get-Date) - $t0).TotalSeconds) s; " + ((Get-Content $f | Select-Object -Last 1) -join ''))
    if ($ok -notcontains $code) { throw "STOPPED at $name (exit $code, see $f)" }
    return $f
}

$PLP = "$L\_Shared\08X04-PLP.SLDPRT"; $LPP = "$Vb\APEX\GNRIC\GNRIC-LPP.SLDPRT"
$T1 = '180x140x10_SLOTS_SHORT_AXIS'; $T2 = '180x140x10_SLOTS_LONG_AXIS'; $src = 'Default<As Machined>'
$hole = [string][char]0x00D8 + '10.0 (10) Diameter Hole1'
# shaft, SFA, old part (= new assembly), weldment-only part, features to delete, plates (body, part, configuration), --accept-gone mates
$jobs = @(
 @{ sh = 'ShaftA'; sfa = '08A04-SFA-2T'; old = '08A04-HBW-02'; new = '08A04-HBW-04'; del = @('PLATE_1', 'M12 Clearance Slot1', 'LPattern1', 'PLATE_2', 'M12 Clearance Slot2');
    plates = @(@('M12 Clearance Slot1', $PLP, $T1), @('LPattern1', $PLP, $T1), @('M12 Clearance Slot2', $PLP, $T2)); gone = @() },
 @{ sh = 'ShaftA'; sfa = '08A04-SFA-3T'; old = '08A04-HBW-06'; new = '08A04-HBW-05'; del = @('Boss-Extrude1', 'M12 Clearance Slot1', 'Boss-Extrude2', 'M12 Clearance Slot2');
    plates = @(@('M12 Clearance Slot1', $PLP, $T1), @('M12 Clearance Slot2', $PLP, $T1)); gone = @() },
 @{ sh = 'ShaftB'; sfa = '08B04-SFA-3T'; old = '08B04-SFP-01'; new = '08B04-SFP-04'; del = @('Boss-Extrude1', 'M12 Clearance Slot1', 'Boss-Extrude2', 'M12 Clearance Slot2');
    plates = @(@('M12 Clearance Slot1', $PLP, $T1), @('M12 Clearance Slot2', $PLP, $T1)); gone = @() },
 @{ sh = 'ShaftB'; sfa = '08B04-SFA-2T'; old = '08B04-SFP-02'; new = '08B04-SFP-05'; del = @('GNRIC-LPP1', 'Front Plane-GNRIC-LPP1', 'Top Plane-GNRIC-LPP1', 'Right Plane-GNRIC-LPP1', 'PLATE_TOP_ALIGN_REF-GNRIC-LPP1', 'PLATE_MID_VERTICAL_ALIGN_REF-GNRIC-LPP1', 'Body-Move/Copy2');
    plates = @(, @('<GNRIC-LPP>-<DOUBLE_BOTTOM>', $LPP, 'DOUBLE_BOTTOM')); gone = @() },
 @{ sh = 'ShaftC'; sfa = '08C04-SFA-2T'; old = '08C04-SFP-02'; new = '08C04-SFP-05'; del = @('PLATE_1', 'M12 Clearance Slot1', 'LPattern1', 'PLATE_2', 'M12 Clearance Slot2');
    plates = @(@('M12 Clearance Slot1', $PLP, $T1), @('LPattern1', $PLP, $T1), @('M12 Clearance Slot2', $PLP, $T2)); gone = @() },
 @{ sh = 'ShaftC'; sfa = '08C04-SFA-3T'; old = '08C04-SFP-03'; new = '08C04-SFP-06'; del = @('Boss-Extrude1', 'M12 Clearance Slot1', 'Boss-Extrude2', 'M12 Clearance Slot2');
    plates = @(@('M12 Clearance Slot1', $PLP, $T1), @('M12 Clearance Slot2', $PLP, $T1)); gone = @() })

switch ($Stage) {
    1 {
        if (-not (Test-Path $PLP)) { "No $PLP"; exit 1 }
        $fi = if ($From) { [array]::FindIndex([object[]]$jobs, [Predicate[object]]{ param($j) $j.old -eq $From }) } else { 0 }
        if ($fi -lt 0) { "STOPPED: -From $From is not one of the six"; exit 1 }
        if ($From) { $script:Log = "$Log" + [char]92 + "resume-$From"; New-Item -ItemType Directory -Force $script:Log | Out-Null }
        foreach ($j in $jobs[($fi + 1)..($jobs.Count - 1)] + $(if (-not $From) { @($jobs[0]) } else { @() })) { foreach ($x in "$L\$($j.sh)\$($j.new).SLDPRT", "$L\$($j.sh)\$($j.old).SLDASM") { if (Test-Path $x) { "STOPPED: $x exists already"; exit 1 } } }
        $allowed = @($PLP) + @(foreach ($j in $jobs) { "$L\$($j.sh)\$($j.new).SLDPRT"; "$L\$($j.sh)\$($j.old).SLDASM"; "$L\$($j.sh)\$($j.sfa).SLDASM" })
        $files = @(Get-ChildItem -LiteralPath $V -Recurse -File | Where-Object { $_.FullName -notlike "*\.pdm\*" })
        $snap = @{}; foreach ($f in $files) { $snap[$f.FullName] = $f.LastWriteTimeUtc }
        $madeRo = @($files | Where-Object { -not $_.IsReadOnly -and $allowed -notcontains $_.FullName })
        $madeRo | ForEach-Object { $_.FullName } | Set-Content "$Log\s1-made-readonly.txt"
        foreach ($f in $madeRo) { $f.IsReadOnly = $true }
        Write-Host "made read-only for this stage: $($madeRo.Count) file(s)"
        Step 'lock' @('lock', '--as', 'sw', '--purpose', "plates-0804 run ($V)") | Out-Null
        try {
            Step 'close-all' @('close-all', '--as', 'sw') | Out-Null
            if (-not $From) {
            Step 'add-config PLP-32' (@('add-config', $PLP, $T1, '--from', '150x140x10_SLOTS_SHORT_AXIS', '--dim', 'D2@Sketch1=180', '--dim', 'Thru Hole Dia.@Sketch5=15', '--suppress', $hole, '--save') + $w) | Out-Null
            Step 'add-config PLP-33' (@('add-config', $PLP, $T2, '--from', '150x140x10_SLOTS_LONG_AXIS', '--dim', 'D2@Sketch1=180', '--suppress', $hole, '--save') + $w) | Out-Null
            Step 'close-all plp' @('close-all', '--as', 'sw') | Out-Null
            }
            foreach ($j in $jobs[$fi..($jobs.Count - 1)]) {
                $d = "$L\$($j.sh)"; $o = "$d\$($j.old).SLDPRT"; $n = "$d\$($j.new).SLDPRT"; $a = "$d\$($j.old).SLDASM"
                $resume = $From -and $j.old -eq $From
                if (-not ($resume -and (Test-Path $n))) {
                Step "save-as $($j.new)" (@('save-as', $o, $n, '--copy') + $rnd + @('--as', 'sw')) | Out-Null
                Step "delete-feature $($j.new)" (@('delete-feature', $n) + $j.del + @('--save') + $w) | Out-Null
                Step "close-all a $($j.new)" @('close-all', '--as', 'sw') | Out-Null
                }
                if (-not ($resume -and (Test-Path $a))) { Step "new-asm $($j.old)" (@('new-asm', $a, $n, '--config', $src) + $rnd + @('--as', 'sw')) | Out-Null }
                $i = 0
                foreach ($p in $j.plates) { $i++; if ($resume -and $i -lt $FromPlace) { continue }; Step "place $($j.old) $i" (@('place', $a, $p[1], $o, $p[0], '--config', $p[2], '--src-config', $src, '--save') + $w) | Out-Null }
                Step "close-all b $($j.old)" @('close-all', '--as', 'sw') | Out-Null
            }
            foreach ($j in $jobs) {
                $d = "$L\$($j.sh)"; $acc = @(); foreach ($m in $j.gone) { $acc += @('--accept-gone', $m) }
                Step "swap-asm $($j.sfa)" (@('swap-asm', "$d\$($j.sfa).SLDASM", "$d\$($j.old).SLDPRT", "$d\$($j.old).SLDASM", "$d\$($j.new).SLDPRT") + $acc + @('--save') + $w) | Out-Null
                Step "close-all c $($j.sfa)" @('close-all', '--as', 'sw') | Out-Null
            }
        } finally {
            & $S close-all --as sw | Out-Null
            & $S unlock --as sw | Out-Null
            foreach ($f in $madeRo) { $f.Refresh(); if ($f.Exists) { $f.IsReadOnly = $false } }
            Write-Host "made writable again: $($madeRo.Count) file(s)"
        }
        $wr = @(Get-ChildItem -LiteralPath $V -Recurse -File | Where-Object { $_.FullName -notlike "*\.pdm\*" -and $allowed -notcontains $_.FullName -and (-not $snap.ContainsKey($_.FullName) -or $snap[$_.FullName] -ne $_.LastWriteTimeUtc) })
        if ($wr.Count) { $wr | ForEach-Object { "  written, not planned: " + $_.FullName } | Tee-Object "$Log\s1-unplanned-writes.txt"; "STOPPED: unplanned writes"; exit 1 }
        $must = if ($From) { @(foreach ($j in $jobs) { "$L\$($j.sh)\$($j.sfa).SLDASM" }) + @(foreach ($j in $jobs[$fi..($jobs.Count - 1)]) { "$L\$($j.sh)\$($j.old).SLDASM" }) } else { $allowed }
        $miss = @($must | Where-Object { -not (Test-Path $_) -or $snap[$_] -eq (Get-Item $_).LastWriteTimeUtc })
        if ($miss.Count) { $miss | ForEach-Object { "  planned, not written: $_" }; "STOPPED: planned files not all written"; exit 1 }
        "stage 1 done: all $($allowed.Count) planned files written, nothing else"
    }
    2 {
        Step 'lock mass' @('lock', '--as', 'sw', '--purpose', "plates-0804 masses ($V), read-only") | Out-Null
        $mt = @()
        try {
            foreach ($j in $jobs) {
                $d = "$L\$($j.sh)"
                $fo = Step "mass old $($j.old)" @('mass', "$d\$($j.old).SLDPRT", '--config', $src, '--as', 'sw')
                $fn = Step "mass new $($j.old)" @('mass', "$d\$($j.old).SLDASM", '--as', 'sw')
                Step "close-all m $($j.old)" @('close-all', '--as', 'sw') | Out-Null
                $mo = ((Get-Content $fo | Where-Object { $_ -like 'MASS*' }) -split "`t")[2]; $mn = ((Get-Content $fn | Where-Object { $_ -like 'MASS*' }) -split "`t")[2]
                $mt += "$($j.old)`t$mo`t$mn`t" + ($(if ($mo -eq $mn) { 'same' } else { 'DIFFERENT' }))
            }
        } finally { & $S close-all --exit --as sw | Out-Null; & $S unlock --as sw | Out-Null }
        $mt | Set-Content "$Log\s2-masses.tsv"; $mt
        if ($mt -match 'DIFFERENT') { "STOPPED: a mass differs"; exit 1 }
        $pf = "$Log\s2-probe-before-props"; Remove-Item -Recurse -Force $pf -ErrorAction SilentlyContinue
        Step 'probe' @($L, $pf) @(0) "$T\DmProbe\bin\DmProbe.exe" | Out-Null
        $tsv = "$Log\s2-props.tsv"
        $mode = if ($Real) { 'no-asm-partno' } else { 'all' }   # P:\: the assemblies' PartNo after the old parts are removed (stage 4)
        Step 'props list' @('-I', "$repo\scratch\import\plates-0804\sw\make_props.py", $pf, "$repo\scratch\inventory\plates-0804-before", $tsv, '08X04-PLP-33', $mode) @(0) python | Out-Null
        $dm = @('batch', $tsv, $V); if ($Real) { $dm += '--checked-out' }
        Step 'props' $dm @(0) "$T\DmEdit\bin\DmEdit.exe" | Out-Null
        $pa = "$Log\s2-probe-after"; Remove-Item -Recurse -Force $pa -ErrorAction SilentlyContinue
        Step 'probe after' @($L, $pa) @(0) "$T\DmProbe\bin\DmProbe.exe" | Out-Null
        "stage 2 done"
    }
    3 {
        if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) { "SOLIDWORKS is open: SwCheck needs it closed (SwCmd close-all --exit)"; exit 1 }
        foreach ($j in $jobs) { Step "swcheck $($j.sfa)" @("$L\$($j.sh)\$($j.sfa).SLDASM", $V) @(0, 1) "$T\SwCheck\bin\SwCheck.exe" | Out-Null }
        foreach ($x in 'A', 'B', 'C') { Step "swcheck 08${x}04-STA-01" @("$L\08${x}04-STA-01.SLDASM", $V) @(0, 1) "$T\SwCheck\bin\SwCheck.exe" | Out-Null }
        "stage 3 done: read the SwCheck logs"
    }
    4 {
        if (-not $Real) { "Stage 4 is the vault run only"; exit 2 }
        # The load refuses one PartNo on two files, and the remove refuses a file still used: so the old parts are
        # unlocked (unchanged), the rest loaded (new assemblies without PartNo yet), the old parts removed, then the six
        # assemblies checked out again, given their PartNo and loaded.
        $note = 'plates-0804: plates out of six 0804 weldments into assemblies (Grant 13 Nov)'
        $olds = @(foreach ($j in $jobs) { "$L\$($j.sh)\$($j.old).SLDPRT" }); $asms = @(foreach ($j in $jobs) { "$L\$($j.sh)\$($j.old).SLDASM" })
        function PdmStep($name, [string[]] $a, $expect) {
            $f = Step $name $a @(0) $Pdm
            $v = (Get-Content $f | Where-Object { $_ -like 'Vault:*' } | Select-Object -First 1)
            if (-not $v -or $v -like '*test*') { throw "STOPPED at ${name}: vault line '$v'" }
            $bad = @(Get-Content $f | Where-Object { $_ -cmatch '^\s*(CHECK|Stopped|NOT OK|Not removed|Not in the vault)\b' -or $_ -match 'more than one file' })
            if ($bad.Count) { throw "STOPPED at ${name}: $($bad[0]) (see $f)" }
            if ($expect) { $n = @(Get-Content $f | Where-Object { $_ -match $expect }).Count; Write-Host "  $n line(s) matching '$expect'" }
            return $f
        }
        PdmStep 'pdm unlock old parts dry' (@('unlock') + $olds + @('--dry')) $null | Out-Null
        PdmStep 'pdm unlock old parts' (@('unlock') + $olds) $null | Out-Null
        $f = PdmStep 'pdm load dry' @('load', '--dry', '--note', $note) '^\s+(new|changed)\s'
        $up = @(Get-Content $f | Where-Object { $_ -match '^\s+(new|changed)\s' })
        if ($up.Count -ne 19) { $up; throw "STOPPED: the load would upload $($up.Count) files, 19 planned (12 new, 6 SFAs, 08X04-PLP)" }
        PdmStep 'pdm load' @('load', '--note', $note) $null | Out-Null
        $rm = "plates moved to assembly"
        foreach ($j in $jobs) { PdmStep "pdm remove dry $($j.old)" @('remove', "$L\$($j.sh)\$($j.old).SLDPRT", '--note', "$rm $($j.old).SLDASM", '--dry') 'Would remove' | Out-Null }
        foreach ($j in $jobs) { PdmStep "pdm remove $($j.old)" @('remove', "$L\$($j.sh)\$($j.old).SLDPRT", '--note', "$rm $($j.old).SLDASM") 'Removed' | Out-Null }
        PdmStep 'pdm checkout asms' (@('checkout') + $asms) 'Checked out' | Out-Null
        $pf = "$Log\s4-probe"; Remove-Item -Recurse -Force $pf -ErrorAction SilentlyContinue
        Step 'probe s4' @($L, $pf) @(0) "$T\DmProbe\bin\DmProbe.exe" | Out-Null
        $tsv = "$Log\s4-asm-partno.tsv"
        Step 'asm partno list' @('-I', "$repo\scratch\import\plates-0804\sw\make_props.py", $pf, "$repo\scratch\inventory\plates-0804-before", $tsv, '08X04-PLP-33', 'asm-partno-only') @(0) python | Out-Null
        Step 'asm partno' @('batch', $tsv, $V, '--checked-out') @(0) "$T\DmEdit\bin\DmEdit.exe" | Out-Null
        $f = PdmStep 'pdm load 2 dry' @('load', '--dry', '--note', $note) '^\s+(new|changed)\s'
        $up = @(Get-Content $f | Where-Object { $_ -match '^\s+(new|changed)\s' })
        if ($up.Count -ne 6) { $up; throw "STOPPED: the second load would upload $($up.Count) files, 6 planned (the assemblies)" }
        PdmStep 'pdm load 2' @('load', '--note', "$note (PartNo)") $null | Out-Null
        "stage 4 done"
    }
}
