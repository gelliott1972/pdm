# Dev status

Updated 22 Oct 2026.

## Now: 0806 A import into the test vault (deadline: James draws from it 22 Oct)

**State:** import done and SOLIDWORKS-checked (22 Oct). Waiting for Grant to open it himself, then Admin's review and the ledger rows into `dataename-ledger.csv`.

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
