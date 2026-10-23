# Dev status

Updated 22 Oct 2026.

## Next: 0806 B and C (brief `docs\briefs\import-0806BC.md`)

Waiting for SW's `-r1` sets. Dev's part is step 4: plan from `-r1` with the ledger (files already in the vault reused, not imported), list every change B/C needs to an existing vault file, dry run, report to Admin.
**Rule from 23 Oct:** `P:\` is vault-tracked (0806 A loaded into the PDM). Never change a file already on `P:\` (re-import, repoint, property writes, clearing read-only) without telling App first. Changes go through `pdm.exe checkout` / `checkin`. Never touch `P:\.pdm`. If the plan needs any existing vault file changed: stop and tell App and Admin. After apply and SwCheck pass: tell App (it runs `pdm load --dry`, then loads).
Tools brought into line (23 Oct): SwFixRefs now checks and repoints only the files the import adds (was every file under `P:\`) and skips `P:\.pdm`; the Importer's name-clash search skips `P:\.pdm`; `Importer --existing` refuses a vault with `.pdm` (tested on `P:\`). SwFixRefs change built but not run (needs SOLIDWORKS).

## Done: 0806 A import into the test vault

**Re-import (brief `docs\briefs\reimport-0806A.md`):** steps 4–7 done (fc5a0e8): `SwFixRefs` and `run-import.ps1` (gate), formula rewrite (tested on the current `P:\`: 48 rewritten), BOM report (4 bolt kits are "show", not Promote; reported only), plan for `08X06-SFW` and `-SFW-04`. r4 end plate fixed and checked; plan leaves out the SF04 floor plate (6a24250); dry run 0 problems. **23 Oct:** Grant said yes to Promote on the 4 bolt kits; SW is setting it in r4 (7 configurations). When SW reports: re-plan from the new probe, check the BOM report shows Promote on all 4 kits, then step 9 (wipe `E:\PDM-test\P`, run, SwCheck) without waiting for Admin, and report to Admin.
**23 Oct 09:30, step 9 under way:** SW's promote probe re-planned (57 import, 1 left out, 107 PartNos, 24 Provisional). Vault wiped; Importer `--apply` passed (57 copied, 178 refs repointed, 79 PartNos written, 52 formulas rewritten, 0 problems; 164 ledger rows in `data\rename-ledger.csv`). SOLIDWORKS failed to start from the API twice (`CO_E_SERVER_EXEC_FAILURE`); started on Grant's retry. **Step 9 done:** SwFixRefs repointed 26, 0 problems; gate 0 outside the vault; BOM report: Promote on all 9 MDL configurations, nothing differs; SwCheck on `LS-08A-06+STRUCTURE` and `08A06-STA-01`: no errors, all loaded from inside `P:\`, no linked equations or Dropbox folders. No stray SOLIDWORKS left. Reported to Admin.
Grant opened `LS-08A-06+STRUCTURE` from `P:\`: no prompts but a rebuild; saved. Admin pushed; 0806 A done.
**After (23 Oct):** `run-import.ps1` now shows each tool's own error text (PowerShell 5.1 had turned it into a bare "NativeCommandError"); tested with the key blanked ("SW_DM_KEY is not set." shown). SwCheck and SwFixRefs say plainly when SOLIDWORKS won't start. New `tools\build.ps1` builds every tool on a fresh PC. **Mistake:** a test dry run overwrote the real import report; rebuilt from the recorded results (marked as rebuilt; NONPART rows not recorded). Dry runs now write `...-dryrun-report.md`.

**State (22 Oct, earlier):** App found 4 SKUs storing r4 paths. Cause: Document Manager `ReplaceReference` doesn't update every path SOLIDWORKS keeps (worst for files that never load). New `tools\SwFixRefs` (SOLIDWORKS `ReplaceReferencedDocument` on closed files) found 27 and repointed 26 in the current `P:\`; 1 left (SF04 floor plate, left out). SwCheck still passes. **On hold:** Grant agreed to fix r4 and the importer, then wipe `P:\` and re-import. Admin is agreeing the list with Dev and SW; no file changes until then. Dev's proposals were sent to Admin (SOLIDWORKS repoint step and gate, property formulas naming old files, floor plate decision, MDL/NONPART BOM check (not yet implemented, contrary to what I told Grant), end-plate fix into r4, ledger rows before re-planning).

| Step | State |
| --- | --- |
| Plan from r4 with the decisions applied (`make_plan.py`, `scratch\import\LS-08A-06-r4-import-plan.xlsx`) | Done |
| Export actions (`export_actions.py` -> `...-files.csv`, `...-partnos.csv`) | Done: 57 files, 1 left out, 107 PartNos (24 Provisional) |
| Importer dry run (`tools\Importer`) | Passed |
| Importer real run into `P:\` (`subst P: E:\PDM-test\P`) | Passed: 57 files, every reference inside `P:\`, every PartNo reads back. Report `scratch\import\LS-08A-06-r4-import-report.md` |
| SwCheck on `LS-08A-06+STRUCTURE` | Everything loads from inside `P:\`. One problem: `08XXX-PLP-20` (tie-beam end plate) has its equations linked to screen 11's `LS_08A-11_Tie_Bar_equations.txt` on James's Dropbox. Grant unticked the link in the vault copy and saved. Re-check: `LS-08A-06+STRUCTURE` and `08A06-STA-01` both pass, no prompts (`scratch\import\LS-08A-06-r4-swcheck-*.txt`). SW told (r4 and 0811 copies still have the link). |
| Ledger | Rows in `scratch\import\LS-08A-06-r4-ledger-rows.csv` (164), not yet in `data
ename-ledger.csv` (outside Dev's write area; for Admin after review) |

Decisions from Grant on 21 Oct: cross brace `08X06-DBP` (-01 = 319X319, -02 = 319X394), tie right angle `08X06-SFP` (-01 = SF01_DEEP, -04 = SF04_DEEP), all Provisional. MDL-/NONPART- BOM setting: checked and reported only (Dev's default, not yet confirmed by Grant). Grant allowed clearing and re-running the test vault until the import passes.

Bugs found and fixed on the way: `P:\` became drive-relative `P:` (paths written as `P:APEX\...`); the Document Manager reports stored paths re-rooted to the file's current folder, so the importer now reads stored paths from the vault copy before `ReplaceReference`.

Known, cosmetic: component instance names in the tree keep their old names (e.g. `LS-08A-06_TIE_BEAM_1-1` for `08A06-HBA-01`). SOLIDWORKS only renames instances when the rename is done in SOLIDWORKS.

## Setup for a new PC

For Dev's tools (importer, plan tool, probes, checks). Tested 23 Oct by building from a clean copy of the tracked source on Grant's PC; not yet tried on another PC.

| Need | How | Check |
| --- | --- | --- |
| Windows 64-bit with .NET Framework 4.x | Built into Windows 10/11. The tools use its C# compiler (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`); no Visual Studio or .NET SDK needed | `build.ps1` says if it's missing |
| SOLIDWORKS (2026 here) | Normal install. The interop DLLs come from `<SOLIDWORKS folder>\api\redist`, found via the registry | `build.ps1` prints the folder it found; `-SwFolder` to override |
| Document Manager key | User environment variable `SW_DM_KEY`, set with System Properties > Environment Variables > User variables > New (not on a command line, so it stays out of shell history). Grant has the key | `build.ps1` notes if unset |
| Python 3 on PATH as `python`, with openpyxl | python.org installer ("Add to PATH"), then `pip install openpyxl`. Tested with Python 3.14.2, openpyxl 3.1.5 | `build.ps1` checks |
| PowerShell allowed to run scripts | `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` (once) | `.ps1` refuses to run otherwise |
| The repo | `git clone` | |
| Build | In PowerShell: `cd <repo>\tools; .\build.ps1`. Builds all six tools into each `bin\` (gitignored) and copies the DLLs | Ends with "All tools built." |
| For an import: `P:` mapped to the vault | `subst P: <vault folder>` (lost on restart until the permanent mapping exists) | `subst` lists it |
| For an import: inputs not in git | `scratch\` is gitignored, so copy the numbering register (`scratch\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx`) and the screen's probe (`scratch\inventory\<screen>`) across, and the Pack and Go source folder | |
| For SwCheck / SwFixRefs | SOLIDWORKS licensed and activated, and closed before running (they refuse otherwise). If it won't start ("SOLIDWORKS didn't start ..."), start it by hand once, clear any licence message, close it | |

## Earlier: proving tests for the importer (brief: `docs\briefs\importer.md`)

Test set: the cantilever sub-assembly from r3 and what it uses, copied to `E:\PDM-work\scratch\prove\`.
`src\` is untouched (for the Pack and Go comparison); `dm\APEX\` is renamed into a vault-style layout and edited with the Document Manager.

| Test | Result so far |
| --- | --- |
| Write `PartNo` / `PartNoStatus` with the Document Manager (file level, assembly config, weldment `<As Machined>` config, change an existing value) | **Proven.** SOLIDWORKS shows every value at the right level. |
| `ReplaceReference` for renamed/moved files (sub-assembly components, nested LCMM components, return -> brace part link) | **Proven.** SOLIDWORKS loads every component from its new name and folder, with the right configuration; the part link points at the renamed brace. |
| Retired 2-size plate -> 4-size plate with `ReplaceReference` (same config names), in the sub-assembly and in LCMM | **Proven.** Loads `08XXX-PLP` [PL_200x140], PartNo `08XXX-PLP-32`. |
| `Component.Replace` (retired plate in LCMM, per configuration) | **Doesn't work**: returns True, but SOLIDWORKS still loaded the old plate (found by name in an old working copy). Not used. |
| SOLIDWORKS Pack and Go API, same renames | Not started; recommendation to Grant pending (see below). |

SOLIDWORKS check run 20 Oct with `tools\SwCheck` on `dm\APEX\SC08\LS-08X-06\08X06-SFW-01.SLDASM`: no errors, only the expected "needs rebuild" warning, all 7 documents loaded from inside the test folder.

## Tools added

| Tool | What it does |
| --- | --- |
| `tools\DmEdit` | Document Manager: list references, read/write a custom property, ReplaceReference, Component.Replace. Refuses Dropbox paths. |
| `tools\SwCheck` | Opens a file in SOLIDWORKS, lists every component (file, config, PartNo) and anything loaded from outside the given folder. Never saves. Refuses to run if SOLIDWORKS is already open. |

## Learned

- Weldment parts have no `Default` configuration: it is `Default<As Machined>` (and `<As Welded>`). PartNo goes at `<As Machined>`, as James does.
- With no search folder, the Document Manager resolves references to an older working copy (`E:\PDM-work\LS-08A-06\`) by stored path. The import must check every reference ends up inside the vault.
- A reference that isn't repointed is silently found by name in old working copies by SOLIDWORKS too. The importer's last step should be a SOLIDWORKS check (as `SwCheck`): nothing loaded from outside the vault.
- After `ReplaceReference`, the Document Manager still reports each component's old stored path, but SOLIDWORKS uses the new one. Check results in SOLIDWORKS, not the Document Manager.
- Components suppressed in every configuration the screen uses can still point at left-out files (in LCMM: floor plate, 08X11 brace, K-beam plate). They don't load, but stay as references outside the vault. To raise when the LCMM split is done.
- `SwCheck` must run with no other SOLIDWORKS open (same-named files in memory would stand in). A stray windowless `SLDWORKS.exe` can be left behind; it doesn't show in Task Manager's main list.

## Decided with Grant

- 20 Oct: drawings are out of scope for 0806 (there are none yet); handled with 0811. The engine choice gets re-checked for drawings then.
- 20 Oct: develop against r3 until r4 is ready.

- 20 Oct: test vault at `E:\PDM-test\P\`.
- 20 Oct: engine = Document Manager (no Pack and Go comparison built; re-check for drawings with 0811).
- 21 Oct: importer proposal in `dev\importer-proposal.md`. Grant: map `P:` to the test vault with `subst` before importing, so files store `P:\` paths from day one.
- 21 Oct (from SW's notes and decision 10): the family splits, ties and retired plate are repointed during prep (SW used `DmEdit`), so r4 arrives final and they are plain renames to the importer. Tie PartNos already `GNRIC`; importer renames other `STOCK` PartNos to `GNRIC`.

## Fixed

- 21 Oct: `SwCheck` left a hidden SOLIDWORKS running after `ExitApp` (reported by SW; it stopped one of Grant's saves landing). Now releases COM, waits 30 s for the copy it started to exit, and ends only that one if not. **Not yet re-tested** (needs SOLIDWORKS closed).

## Open

- **r4 ready and accepted by Admin (21 Oct):** `E:\PDM-work\LS-08A-06-r4` is the source for 0806 A. Plan written to `scratch\import\LS-08A-06-r4-import-plan.xlsx` (58 files) with the current plan tool. It doesn't yet apply decisions 1, 2, 3 (STOCK -> GNRIC for MEMBER, PACKER, PLATE_FLOOR, PLATE_LED_CABINET), 5 and 6, or the ST category for `08A06-STA-01`: build step 1.
- **r4 final (SW, 21 Oct):** the equations `.txt` link was on the top-level `LS-08A-06 SCREEN+STRUCTURE` (unlinked; 8 broken equations deleted, 7 global variables kept). The Toolbox "missing size" prompt came from `TIE_BEAM_ENDPLATE_BOLTS` (cleared by re-saving). Fresh probe: 58 files, 108 references, all inside r4. The r4 plan was re-run from it. The tidy copy `E:\PDM-work\LS-08A-06` is now behind r4: don't use it for 0806.
- **Tie-beam post split in r4 (SW, 21 Oct):** `_Shared\08XXX-HBW-01.SLDPRT` (Default only) replaces `LS-08X-X_TIE_BEAM_POST`; `08BXX-HBW-01` comes with B. Re-probed and the plan re-run: the plan imports it as named, in `APEX\SC08\_Shared`. No old names left in r4's stored component paths.
- **Asked Grant (21 Oct):** should the importer check or set Promote / Exclude from BOM on `MDL-` / `NONPART-` files? And OK to build steps 1 and 2?
- (Earlier) r4 (SW, 21 Oct 13:20): waiting on Grant's open, rebuild and Save All, then Pack and Go to `E:\PDM-work\LS-08A-06-r4` and SW's probe. Expected this afternoon. SW messages when probed. New in r4: `GNRIC-BRP`, `GNRIC-BRA`, `08X06-LMW`, `MDL-ANCHOR-08X06`. Dropped: LCMM, tie, 2-size plate, `GROUT+ANCHOR_BOLTS`, 08X11 brace, K-beam plate. Newer content, same name: `MEMBER_SHS_40x80x8` (still that name; `GNRIC-LM` is the importer's job), `PACKER`, `BRACKET_80X40_BOLTED_TIE`, `SLEEVE_TIE_BEAM`.
- Equations `.txt`: nobody knows which file links it yet (seen opening `08A06-STA-01`; probably the skeleton chain). Grant to check Tools > Equations.
- `SwCheck` now also warns if Referenced Documents lists a Dropbox folder (SW's suggestion). Not yet re-tested.
- Part links to old inbox paths (skeleton, cantilever brace) in r3/r4: the importer repoints them (proven for the brace).
- `SwCheck` blocks on a "missing linked equations file" dialog until someone clicks Cancel. Look for a way to suppress it.
