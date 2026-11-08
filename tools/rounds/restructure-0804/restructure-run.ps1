# 0804 restructure round: Dev's parts, in stages, on the rehearsal copy (default) or on P:\ (-Real, only after Grant's go).
#   -Stage 1: gate before; pdm checkout (--dry, then real); pdm move (--dry, then real); repoint (SwFixRefs --round --apply)
#   -Stage 2: SW's sequence with SwCmd: dissolve B, C; form-sub A, B, C (B: two stale mates accepted); saved
#   -Stage 3: check the STA-01s are there; resave (SwResave --round); gate; STA-01 properties (DmProbe + DmEdit batch);
#             SwCheck and SwSuppDump of the 3 tops; DmProbe of LS-08X-04 and LS-08X-11
#   -Stage 4: pdm load (--dry, then real); pdm remove (--dry, then real)
#   -Stage 5 (rehearsal only): the engineer PC's pdm get (-Engineer <its pdm.exe>), twice; the second must find nothing to do
# Stops at the first failure. Writes the Desk status at each step. -Pdm: the pdm.exe to use (App's test copy for the rehearsal).
param([Parameter(Mandatory)] [int] $Stage, [Parameter(Mandatory)] [string] $Pdm, [switch] $Real, [switch] $Go, [string] $Note = "0804 restructure (Grant 7 Nov)", [string] $RemoveNote = "Retired: steel into the STA-01, screen into the top (S2)", [string] $Engineer = "", [string] $From = "")
$repo = "C:\code\pdm"; $T = "$repo\tools"; $round = "$repo\scratch\import\restructure-0804\round.csv"; $st = "$repo\desk\status\dev.json"
if ($Real) {
    if (-not $Go) { "Not started: -Real needs -Go, and only after Grant's go on the Desk."; exit 2 }
    $V = "P:\"; $log = "$repo\scratch\import\restructure-0804\real"; $where = "P:\ (Grant's go)"
} else {
    $V = "$repo\scratch\import\rehearsal-restructure\vault"; $log = "$repo\scratch\import\rehearsal-restructure\logs"; $where = "rehearsal copy, not P:\"
}
New-Item -ItemType Directory -Force $log | Out-Null
# Paths below the vault are built from $Vb (no trailing backslash): on P:\ a "$V\..." path had a double backslash (real run, 8 Nov:
# the tops didn't match the allowed list and were made read-only, so form-sub refused, nothing changed).
$Vb = $V.TrimEnd('\')
# The vault faults the gate shows that this round doesn't touch (7 Nov, before and after the tool test).
$known = @(
    'APEX\GNRIC\GNRIC-BRW-20.SLDDRW: no such vault file: P:\APEX\GNRIC\GNRIC-BRW-20.SLDPRT',
    'APEX\GNRIC\GNRIC-LPP-05.SLDDRW: outside the vault: C:\Users\DELL\Dropbox\Models\Screen Models\311v3\_Shared\PLATE_LED_CABINET.SLDPRT',
    'APEX\SC08\_Shared\08XXX-PLP-33.SLDDRW: outside the vault: C:\Users\DELL\Dropbox\Models\Screen Models\311v3\SC08\_Shared\LS-08X-X_TIE_BEAM_INTERFACE_PLATE.SLDPRT')

function Desk($doing, $state = "working") {
    $j = @{ name = "dev"; state = $state; doing = $doing; waitingOn = ""; updatedAt = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") } | ConvertTo-Json -Compress
    try { [IO.File]::WriteAllText("$st.tmp", $j, (New-Object Text.UTF8Encoding $false)); Move-Item -Force "$st.tmp" $st } catch { }
}
# -From <step name>: restart a stage part way; the steps before it are skipped (their earlier logs are kept and re-checked).
$script:reached = -not $From
function Step($name, $exe, [string[]] $a, [int[]] $okCodes = @(0)) {
    $f = "$log\s$Stage-" + ($name -replace '[^A-Za-z0-9]+', '-') + ".txt"
    if (-not $script:reached -and $name -eq $From) { $script:reached = $true }
    if (-not $script:reached) { Write-Host "$name : skipped (-From $From), earlier log kept"; return $f }
    Desk "0804 restructure ($where): stage $Stage, $name"
    $t0 = Get-Date
    $ErrorActionPreference = 'Continue'
    & $exe @a > $f 2>&1; $code = $LASTEXITCODE
    Write-Host ("$name : exit $code, $([int]((Get-Date) - $t0).TotalSeconds) s; " + ((Get-Content $f | Select-Object -Last 1) -join ''))
    if ($okCodes -notcontains $code) { Desk "0804 restructure ($where) STOPPED at stage $Stage, $name (exit $code): see $f" "blocked"; Write-Host "STOPPED at $name"; exit 1 }
    return $f
}
# A pdm.exe step: its first line names the vault; the rehearsal must be on the TEST vault and the real run must not.
# A dry run with CHECK or Stopped lines stops the round.
function PdmStep($name, $exe, [string[]] $a) {
    $f = Step $name $exe $a
    $v = (Get-Content $f | Where-Object { $_ -like 'Vault:*' } | Select-Object -First 1)
    $bad = if (-not $v) { "no Vault line" } elseif (-not $Real -and $v -notlike '*pdm-apex-test*') { "not the test vault: $v" } elseif ($Real -and $v -like '*test*') { "the test vault on a real run: $v" } else { $null }
    $chk = @(Get-Content $f | Where-Object { $_ -cmatch '^\s*(CHECK|Stopped)\b' })
    if ($chk.Count) { $bad = "$($chk.Count) CHECK/Stopped line(s): $($chk[0])" }
    if ($bad) { Desk "0804 restructure ($where) STOPPED at stage $Stage, ${name}: $bad (see $f)" "blocked"; Write-Host "STOPPED at ${name}: $bad"; exit 1 }
    Write-Host "  $v"
    return $f
}
# The gate passes when the only findings are the known faults outside this round.
function Gate($name) {
    $f = Step $name "$T\SwFixRefs\bin\SwFixRefs.exe" @($V, '--round', $round) @(0, 1)
    $found = @(Get-Content $f | Where-Object { $_ -like '*  ** GATE **' } | ForEach-Object { ($_ -replace '  \*\* GATE \*\*$', '') -replace [regex]::Escape($V.TrimEnd('\') + '\'), 'P:\' })
    $new = @($found | Where-Object { $known -notcontains $_ })
    if ($new.Count -gt 0) { $new | Select-Object -First 20; Desk "0804 restructure ($where) STOPPED: $name found $($new.Count) new problem(s): see $f" "blocked"; "STOPPED at $name"; exit 1 }
    "$name : only the $($found.Count) known faults outside this round"
}
if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) { "SOLIDWORKS is open: stop"; exit 1 }

switch ($Stage) {
    1 {
        $f = Step 'gate before' "$T\SwFixRefs\bin\SwFixRefs.exe" @($V, '--round', $round) @(1)
        PdmStep 'pdm checkout dry' $Pdm @('checkout', $round, '--dry') | Out-Null
        PdmStep 'pdm checkout' $Pdm @('checkout', $round) | Out-Null
        PdmStep 'pdm move dry' $Pdm @('move', $round, '--note', $Note, '--dry') | Out-Null
        PdmStep 'pdm move' $Pdm @('move', $round, '--note', $Note) | Out-Null
        foreach ($r in Import-Csv $round | Where-Object { $_.action -like 'moved*' }) {
            if (-not (Test-Path -LiteralPath (Join-Path $V $r.new_path)) -or (Test-Path -LiteralPath (Join-Path $V $r.old_path))) { Desk "0804 restructure ($where) STOPPED: move not as planned for $($r.new_path)" "blocked"; "STOPPED: $($r.old_path) -> $($r.new_path) not moved"; exit 1 }
        }
        "moves: all $(@(Import-Csv $round | Where-Object { $_.action -like 'moved*' }).Count) files at their new paths"
        Step 'repoint' "$T\SwFixRefs\bin\SwFixRefs.exe" @($V, '--round', $round, '--apply') | Out-Null
        Desk "0804 restructure ($where): stage 1 done (checkout, move, repoint); next SW's dissolve and form-sub" "waiting"
    }
    2 {
        # SW's sequence (7 Nov, tested on SW's copy): A form-sub; B and C dissolve then form-sub; B's two stale mates accepted.
        $S = "$T\SwCmd\bin\SwCmd.exe"; $L = "$Vb\APEX\SC08\LS-08X-04"
        $w = @('--as', 'dev'); if ($Real) { $w += '--checked-out' }
        $rnd = @(); if ($Real) { $rnd = @('--round', $round) }
        # Only the 3 tops may be written here, and the 3 STA-01s made. SOLIDWORKS saves every modified referenced file it can
        # write (rehearsal, 8 Nov), so the other writable files (the round's check-outs) are read-only for this stage, and
        # every file's write time is compared after each shaft.
        $allowed = @(foreach ($x in "A", "B", "C") { "$L\LS-08$x-04+STRUCTURE.SLDASM"; "$L\08${x}04-STA-01.SLDASM" })
        $files = @(Get-ChildItem -LiteralPath $V -Recurse -File | Where-Object { $_.FullName -notlike "*\.pdm\*" })
        $snap = @{}; foreach ($f in $files) { $snap[$f.FullName] = $f.LastWriteTimeUtc }
        $madeRo = @($files | Where-Object { -not $_.IsReadOnly -and $allowed -notcontains $_.FullName })
        $madeRo | ForEach-Object { $_.FullName } | Set-Content "$log\s2-made-readonly.txt"
        foreach ($f in $madeRo) { $f.IsReadOnly = $true }
        Write-Host "made read-only for this stage: $($madeRo.Count) file(s) (listed in s2-made-readonly.txt)"
        function Written {
            $w = @(Get-ChildItem -LiteralPath $V -Recurse -File | Where-Object { $_.FullName -notlike "*\.pdm\*" -and $allowed -notcontains $_.FullName -and (-not $snap.ContainsKey($_.FullName) -or $snap[$_.FullName] -ne $_.LastWriteTimeUtc) })
            if ($w.Count) { $w | ForEach-Object { "  written, not planned: " + $_.FullName } | Tee-Object -Append "$log\s2-unplanned-writes.txt" | Write-Host; Desk "0804 restructure ($where) STOPPED at stage 2: $($w.Count) file(s) written that weren't planned (see s2-unplanned-writes.txt)" "blocked"; Write-Host "STOPPED: unplanned writes"; exit 1 }
        }
        Step 'lock' $S @('lock', '--as', 'dev', '--purpose', "0804 restructure ($where): dissolve and form-sub") | Out-Null
        try {
            foreach ($x in "A", "B", "C") {
                $top = "$L\LS-08$x-04+STRUCTURE.SLDASM"; $sta = "$L\08${x}04-STA-01.SLDASM"
                if (Test-Path $sta) { Write-Host "STOPPED: $sta exists already"; exit 1 }
                Step "open $x" $S (@('open', $top) + @('--as', 'dev')) | Out-Null
                if ($x -ne "A") {
                    $acc = @(); if ($x -eq "B") { $acc = @('--accept-gone', 'Coincident70') }
                    Step "dissolve $x" $S (@('dissolve', $top, "LS-08$x-04_WITH_SCREEN<1>") + $acc + @('--save') + $w) | Out-Null
                }
                $acc = @(); if ($x -eq "B") { $acc = @('--accept-gone', 'Coincident42') }
                Step "form-sub $x" $S (@('form-sub', $top, $sta, '--except', "LS-08$x-04<1>") + $acc + $rnd + @('--save') + $w) | Out-Null
                Step "close-all $x" $S @('close-all', '--as', 'dev') | Out-Null
                Written
            }
        } finally {
            & $S close-all --exit --as dev | Out-Null
            & $S unlock --as dev | Out-Null
            foreach ($f in $madeRo) { $f.Refresh(); if ($f.Exists) { $f.IsReadOnly = $false } }
            Write-Host "made writable again: $($madeRo.Count) file(s)"
        }
        Written
        Desk "0804 restructure ($where): stage 2 done (dissolve, form-sub); next resave, gate, properties, checks" "working"
    }
    3 {
        foreach ($x in "A", "B", "C") { if (-not (Test-Path "$Vb\APEX\SC08\LS-08X-04\08${x}04-STA-01.SLDASM")) { "STOPPED: no 08${x}04-STA-01.SLDASM yet (SW's form-sub)"; exit 1 } }
        Step 'resave' "$T\sw-macros\bin\SwResave-import.exe" @('--round', $round, $V) | Out-Null
        Gate 'gate after'
        $pf = "$log\s2-sta-probe"; Step 'probe STA-01s' "$T\DmProbe\bin\DmProbe.exe" @("$Vb\APEX\SC08\LS-08X-04", $pf) | Out-Null
        $tsv = "$log\s2-sta-props.tsv"
        Step 'sta props actions' python @('-I', "$repo\scratch\import\restructure-0804\make_sta_props.py", (Get-ChildItem $pf -Recurse -Filter properties.csv | Select-Object -First 1).FullName, 'APEX\SC08\LS-08X-04', $tsv) | Out-Null
        $dm = @('batch', $tsv, $V); if ($Real) { $dm += '--checked-out' }
        Step 'sta props' "$T\DmEdit\bin\DmEdit.exe" $dm | Out-Null
        foreach ($x in "A", "B", "C") {
            $top = "$Vb\APEX\SC08\LS-08X-04\LS-08$x-04+STRUCTURE.SLDASM"
            Step "swcheck $x" "$T\SwCheck\bin\SwCheck.exe" @($top, $V) @(0, 1) | Out-Null
            Step "suppdump $x" "$T\SwSuppDump\bin\SwSuppDump.exe" @($top) @(0, 1) | Out-Null
        }
        # Mass of each top (read-only), to compare with SW's: A 4263.99022, B 4761.54117, C 4870.2244 (Default, kg).
        Step 'lock mass' "$T\SwCmd\bin\SwCmd.exe" @('lock', '--as', 'dev', '--purpose', "0804 restructure ($where): mass of the tops (read-only)") | Out-Null
        try { foreach ($x in "A", "B", "C") { Step "mass $x" "$T\SwCmd\bin\SwCmd.exe" @('mass', "$Vb\APEX\SC08\LS-08X-04\LS-08$x-04+STRUCTURE.SLDASM", '--as', 'dev') | Out-Null } }
        finally { & "$T\SwCmd\bin\SwCmd.exe" close-all --exit --as dev | Out-Null; & "$T\SwCmd\bin\SwCmd.exe" unlock --as dev | Out-Null }
        Step 'probe after 04' "$T\DmProbe\bin\DmProbe.exe" @("$Vb\APEX\SC08\LS-08X-04", "$log\s2-probe-04") | Out-Null
        Step 'probe after 11' "$T\DmProbe\bin\DmProbe.exe" @("$Vb\APEX\SC08\LS-08X-11", "$log\s2-probe-11") | Out-Null
        Desk "0804 restructure ($where): stage 3 done (resave, gate, STA-01 properties, checks); comparing with the baseline" "working"
    }
    4 {
        PdmStep 'pdm load dry' $Pdm @('load', '--note', $Note, '--dry') | Out-Null
        PdmStep 'pdm load' $Pdm @('load', '--note', $Note) | Out-Null
        PdmStep 'pdm remove dry' $Pdm @('remove', $round, '--note', $RemoveNote, '--dry') | Out-Null
        PdmStep 'pdm remove' $Pdm @('remove', $round, '--note', $RemoveNote) | Out-Null
        Desk "0804 restructure ($where): stage 4 done (load, remove); next the sync check on a second copy" "waiting"
    }
    5 {
        if ($Real -or -not $Engineer) { "Stage 5 is for the rehearsal, with -Engineer <the engineer copy's pdm.exe>"; exit 2 }
        PdmStep 'engineer get' $Engineer @('get') | Out-Null
        PdmStep 'engineer get again' $Engineer @('get') | Out-Null
        Desk "0804 restructure ($where): stage 5 done (engineer PC sync); checking the logs" "working"
    }
}
"STAGE $Stage DONE"
