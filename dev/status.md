# Dev status

Updated 29 Oct 2026, on Grant's laptop.

## Now: J2 / J3 property update (James's notes 28 Oct, plus D17)

Admin's job, agreed with Grant: Finish once at file level, Description and Weight per configuration (as James's `08A11-SFA`, Weight `"SW-Mass@@<config>@<file>"`), every DrawingRef deleted, drawings left out, As Welded listed only. No overnight run: Grant goes through the sheet with James first. Nothing touches P:\ until Grant has checked out and Admin says go.

**Step 1, sheet: done.** `tools\importplan\props_sheet.py` (read-only, from a DmProbe of P:\APEX and P:\_Shared, plus the ledger for old names) wrote `scratch\import\vault-props-J2J3.xlsx` from a fresh probe `scratch\inventory\vault-P-2810` (identical to the post-import probe). 195 rows (0806 62, 0811 43, Shared 70, fasteners 20 incl. the 9 D17): complete 5, pre-filled 81 (54 with a guess marked to check), blank 109 (Finish 100, Description 22). Weight is pre-filled on every row (formula). DrawingRef: 31 rows, all to delete (sheet DrawingRef). As Welded: 70 configurations, listed only. Questions to Admin: file-level copies once moved to the configuration, configuration-level Finish once moved to file level, file-level-PartNo files, fasteners in or out, D17 count (doc lists 10, vault uses 9), M8x55 described as M8x50, "ASSYY" typo, 0806 SFA-1B copying "ASSY B".

Admin's working assumptions until Grant and James answer (28 Oct): leave file-level copies, keep configuration-level Finish, file-level PartNo stays at file level, 9 D17 PartNos. Fasteners' Description/Finish/Weight are in the sheet as pre-filled (Q4 still open).

**Step 2, writer: done and tested on scratch.**
- `DmEdit batch <actions.tsv> <root> [--checked-out]`. Pass 1 writes nothing unless every file is inside the root, exists, is writable, is outside Dropbox and `.pdm`, and every old value still matches the probe. Pass 2 goes file by file: log old -> new, apply, save, re-open, check that each change landed and nothing else changed; it stops at the first failure.
- `--checked-out` (Admin approved 28 Oct) is the only way to write under P:\, and then only for the files on the list, only when none is read-only, never `.pdm`. Single setprop/delprop still refuse P:\.
- `props_actions.py <sheet> <probe> <out.tsv>` turns the sheet into the change list: blank proposals are skipped, unchanged values left out, Finish must agree across a file's rows.
- `props_compare.py <before> <after> <tsv>`: every property difference must be on the list; files, configurations, components and references must be unchanged.
- Trial on a fresh scratch copy of P:\APEX + _Shared: 288 changes in 106 files (Description 97, Weight 110, Finish 41, PartNo 9, DrawingRef deleted 31), all read back. The compare is clean. A second run refused (old values no longer match). Read-only copies refused.
- Guard tests: P:\ without the flag refused. P:\ with the flag refused while read-only. `.pdm` refused. Outside the root refused. Single setprop on P:\ refused. All 106 P:\ targets were read-only during the tests, so nothing could be written there.
- Re-run after Grant and James edit the sheet: fresh DmProbe of P:\APEX and P:\_Shared, then `props_actions.py`, then batch, then probe, then compare.

**Step 3, rehearsal with SW's units change (29 Oct): done, all checks pass.** Weight: SW's read-only `SwUnits --weights` pass (`rehearsal-J2J3\weights-all.txt`) shows all 110 Weights set by the batch evaluate to a number in kg (props_weights: 110 / 0 wrong / 0 unchecked), and the 470 values read by both passes agree with SwKg's. MDL-HEX-BOLT-WASHERS-NUTS-GNRIC crashes SOLIDWORKS on a read-only open in the configuration loop; it does the same on P:\, and it has no Weight. SW's units recheck: only mass units changed (6 MMGS files also changed system).
Fresh scratch copy of P:\APEX + _Shared at 8:21, in `scratch\import\rehearsal-J2J3\vault` (181 files).
- Before the writes, the copy's probe matched vault-P-2810 exactly.
- DmEdit batch: 288 changes in 106 files, all read back. props_compare (probe0 vs probe1): CLEAN.
- SW's SwKg on the copy: 107 files switched to kg and saved, 74 already in kg (not saved), 0 failures.
- props_compare after SwKg (probe1 vs probe2): no property, configuration or reference changed.
  - Component rows changed only because SOLIDWORKS replaced DM's stale pre-import component names and paths with the vault names. Components not in their assembly's reference table went 127 -> 4, all 4 in 08X06-LMW, which was already kg and not saved.
  - props_compare now normalises component stored paths, as it does references.
- Weight (`props_weights.py` on SwKg's output): 94 of 110 evaluate to a number, 0 wrong. 16 are in files SwKg didn't save. SW is writing `weights-all.txt` (SwUnits --weights, read-only) for every file.
- SwCheck (root = the copy) and SwSuppDump of all 6 +STRUCTURE tops, in `rehearsal-J2J3\swcheck\`:
  - opened with no errors;
  - every loaded component is from the copy;
  - only part links (HB-SKEL, 08X06-SFP-03, 08BXX-MSP-01) load from P:\, read-only;
  - SwSuppDump's P:\ rows are all suppressed and unloaded;
  - asked SW about its "COULD NOT SHOW" lines.
- P:\ was not touched by the rehearsal.
- Scripts: the session scratchpad's props-rehearsal.ps1 and props-rehearsal-after-sw.ps1.

**P:\ changed on 29 Oct, 9:46–9:47, not by Dev or SW (Admin is asking Grant who).**
- 20 new drawings, plus 2 PDFs that aren't read-only (10:46).
- 44 property values changed on 15 existing files, mostly file-level Description, Finish, Weight and Material on 0806 A, 08X06 and 08A11-SFA.
- Four formulas point at another file: SFA-1B and SFA-2T Weight -> SFA-1T; HBW-03 Material and Weight -> HBW-02.
- 11 assemblies re-saved: DM component names refreshed. 08A06-STA-01 gained 6 MDL-SPLICE-JOINT-GNRIC [M14] instances.
- Probe: `scratch\inventory\vault-P-2910`.
- `props_actions --since <sheet's probe>` (68f72e6) now stops if any Description, Finish, Weight, PartNo or DrawingRef on a sheet file has changed since the sheet was made.
- New sheet (Admin, 29 Oct): `scratch\import\vault-props-J2J3-2910.xlsx` from `props_sheet.py vault-P-2910 data\rename-ledger.csv <out> --since vault-P-2810` (654d7e3).
  - Changed values are kept as current: 26 rows marked, 44 values on a "Changed since" sheet, the 4 above flagged "needs fixing by James", 16 file-level values flagged "left as is until Grant decides".
  - 274 actions, none touching a value changed on 29 Oct.
  - The 28 Oct sheet is unchanged; without --since it is reproduced exactly.
- Real run from the new sheet: fresh probe, then `props_actions <sheet> <fresh probe> <tsv> --since vault-P-2910`, then batch `--checked-out`, then probe, then compare.

## Earlier: 0811 import (brief `docs\briefs\import-0811.md`)

**Step 3 (tools read drawings): done for the Document Manager tools; the SOLIDWORKS-side check waits for SOLIDWORKS to be closed** (Grant had it open for the Pack and Go). Tools built on the laptop (`tools\build.ps1`: all six built).

Test set: the SOLIDWORKS 2024 `samples\introsw` files copied to `C:\PDM-work\drawtest\src` (stored paths point at an old `F:\samplefiles\...`, like APEX's old paths). Test vault `C:\PDM-work\drawtest\vault` (not `P:\`). Plan, report and ledger in `scratch\import\drawtest\` (not `data\`). Probes `scratch\inventory\drawtest-src`, `-vault`.

| Tool | Before | Now | Tested |
| --- | --- | --- | --- |
| DmProbe | Read drawings, but logged an E_FAIL line per drawing (configuration list) | Skips the configuration list on drawings | Yes: 4 drawings, all references read |
| Importer | **Crashed on drawings** in the formula step (configuration list, E_FAIL), after copying | Skips configurations on drawings; stops if a PartNo row is on a drawing; a left-out file a drawing reaches only through its model is a note, not a problem | Yes: 9 files incl. 2 drawings renamed and moved, 22 references repointed, re-read all inside the test vault; the PartNo-on-drawing stop tried |
| DmEdit `replace-ref` | Would crash on a drawing (configuration list) | Reads components only on assemblies | Yes, on a test-vault drawing |
| SwCheck | Opened a drawing as a part (fails) | Opens it as a drawing; the "documents in memory" list shows where each model loaded from | **No** (needs SOLIDWORKS closed) |
| SwFixRefs | No configuration use; should work on drawings | Unchanged | **No** (needs SOLIDWORKS closed) |
| Plan tool | Named a drawing after its alphabetically first reference | See below | Yes, on the samples |

**What I learned:** the Document Manager lists a drawing's references as the drawn model **plus everything inside it** (the bolt-assembly drawing lists the assembly, bolt, nut and washer). Reference status `2` is the normal value on every file. ReplaceReference and Save on a drawing work (App hadn't tried them).

**Plan tool and drawings:** no PartNo rows (unchanged). Model = the reference with the drawing's own name, else the only one not inside another; otherwise "decide" with the question. Named after the model's new name, in the model's folder (unchanged). New notes: "drawing also shows X" when it reaches other models; "its model is already in the vault: the drawing is a new file beside it". A drawing already on `P:\` is still caught by the Importer ("a file named ... is already in the vault"), because drawings added from the PDM page aren't in the ledger.

**For step 5 (Admin, 25 Oct):** only SOLIDWORKS files, plus files a model references by name (the end plate's `LS_08A-11_Tie_Bar_equations.txt`), go to `P:\`. Loose CSV, PDF, STEP, SAT, TXT are listed in the plan as left out (the probe lists only SOLIDWORKS files, so the plan needs a list of the other files in the Pack and Go). The equations .txt's home is a question in the plan, not a choice.

**Drawings named by the configuration they show (Admin's proposal to Grant, 25 Oct; not built, waiting for Grant):** the Document Manager can read it. `GetViews()` gives every view with its sheet, model file **name** (no folder) and configuration (`ISwDMView2.ReferencedDocument` / `ReferencedConfiguration`). Tried read-only on the 4 sample drawings and the 2 test-vault drawings (throwaway program in Dev's scratch folder). Findings:

| | |
| --- | --- |
| Several views per drawing, mixed | A drawing can show several models and configurations (the bolt-assembly drawing has assembly and part views; one sample has views on 3 sheets) |
| Odd configurations | An alternate-position view names its own configuration (`AltPosition_Default_1`), which isn't one of the model's real ones |
| Names not updated by repointing | After the test import, the vault drawings' views **still name the old files** (`bolt-assembly.sldasm`), though the references point at the new ones. To check in SOLIDWORKS (SwCheck) whether that matters |
| Weldments | Unknown until real data: a view may name `Default` while the PartNo sits on `Default<As Machined>` |

Proposed rule (for Admin, not decided): take the views' (model, configuration) pairs, dropping configurations the model doesn't have; find each pair's PartNo; the drawing's name already equal to one of them = confirmed; else exactly one PartNo = rename to it; else "decide" with the pairs listed. An old-named drawing whose PartNo another drawing already carries = left out and listed. Folder beside the model. DmProbe would write a `views.csv`.

**SOLIDWORKS checks (25 Oct, done):** test vault: SwFixRefs (list only) 0 stored references outside; SwCheck on both drawings: no errors or warnings, all inside the vault (the old names left in views don't matter). `08C11-SFA-3T` after SW's repoint: passes, no "not the same document", loads C's `SF03-UPPER`. **Bug fixed:** the laptop has SOLIDWORKS 2024 (`SOLIDWORKS (2)`) beside 2026; the plain `SldWorks.Application` started 2024, which refused 0811 files (swFutureVersion). SwCheck and SwFixRefs now start the newest install and print its version (932b66a).

**Plan tool for 0811 (built 25 Oct; decisions in `docs\imports\LS-08X-11-decisions.md`):**

| Decision | In the plan tool | Trial on today's work copies (`scratch\inventory\LS-08x-11-r0`, not `-r1`) |
| --- | --- | --- |
| D2 | DmProbe writes `views.csv`; drawing named by the PartNo its views show (kept / renamed / decide), in the model's folder. A view still naming a repointed model falls back to the drawing's own reference, with a note | A, B, C: every numbered drawing keeps its name beside its model; `08C11-SFA-3T` resolved to C's `SF03-UPPER` |
| D3 | A's 9 older drawings left out (listed) | 9 left out |
| D4 | Replaced by D20: C's `SF03-UPPER` and the `08C11-SFA-3T` drawing are left out | |
| D8, D9 | James's numbers, Provisional, per shaft (B's `SF01-LOWER` gets `08Z11`, D20) | D9 letters confirmed by SW |
| D12 | `PLATE_Bolting_Connector` configurations `GNRIC-MSP-02/03/11/14`, Provisional | |
| D13, D23 | Splice kit and hex bolt kit are modelling groups, named by the decision: `MDL-SPLICE-JOINT-GNRIC`, `MDL-HEX-BOLT-WASHERS-NUTS-GNRIC` in `APEX\GNRIC` | A imports both; B, C reuse |
| D17 | New "Vault changes" sheet: configurations of vault files that imported files refer to, with no PartNo in the ledger | A: exactly D17's 10. B, C: none (A brings the kit) |
| D19 | `--with <A probe> <A files.csv>` (repeatable): files A imports are **reuse** in B and C (their files must point at them), and each copy's configurations and PartNos are compared with A's | B, C: 8 files each, no differences |
| D20 | B's 5 identical subframes named `08Z11-SFA` (family, `-1A`/`-1B`), `-2B`, `-2T`, `-3B`, `-3T` in `LS-08X-11\ShaftBC`, Provisional; their drawings follow (D2). C keeps its own 5 files in `-r1`; the plan makes them **reuse of B's files** (`same_as`, needs `--with` B), so SwFixRefs repoints C's STA at B's. Any difference from B's file (configurations, PartNos with only the B/C shaft letter blanked, as D20 decides; everything else raw, or children per configuration) stops the plan (`decide`). C's drawings of them are left out and listed | B: 5 files, 6 drawings to `08Z11`. C: 5 files reuse, 6 drawings left out; on 2510 no differences. Mock with a changed PartNo, child count and child config: all 3 stopped |
| D21 | An equations `.txt` still linked in `-r1` is "decide" (unlink it; not a vault file) | none linked in today's probes |
| D22, D25 | `08x11-STA-01` PartNo at Default, Provisional; "decide" if the STA has any file-level property (D25: all 7 go, Description sits at Default) | No STA in today's probes (they predate D7), so tested on a mock: Description, Part Number and PartNo all flagged. (Earlier note here said A's STA was flagged: wrong, it isn't in the probe) |
| D14, D28 | Files named as the vault: `08X11-LMW` (01A/01B/02A = `-01..03`, A imports, B and C reuse), `08B11-LMW-01` (B), `08X11-DBP-01` (A imports, C reuses), `08B11-DBP-01` (B). The old `LCMM_40x80x8_SC08` and the 2-size plate `LS-08X-X_PLATE_TIE_BEAM_INTERFACE` still there = "decide". The 4-size plate `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` reuses vault `08XXX-PLP` from the ledger (D28); ledger reuse rows compare nothing (no mass, configurations or PartNos) | Mock -r1 of A: `08X11-LMW` and `08X11-DBP-01` named and filed right (`LS-08X-11\_Shared`), PartNos from the file; old plate stopped. 2510: the 4-size plate's 4 configurations and PartNos match the ledger (checked by hand) |
| Vault check (Admin, all screens) | `--vault <probe>` (fresh read-only DmProbe of `P:\APEX` and `P:\_Shared`, never `.pdm`): every ledger reuse row's vault file must be there with each configuration this screen uses, and the vault PartNo must equal the ledger's and any PartNo already in the copy; else "decide". No mass or geometry. Without `--vault`, one issue says the ledger rows weren't checked. No ledger PartNo and none in the vault = the Vault changes sheet (D17), not a stop | Probe `scratch\inventory\vault-P-2510d` (94 files). 0806 regression A/B/C: all reuse except B's expansion-joint bracket (copy has the live `08B11-MSW-01`, vault `08BXX-MSW-01`, decision 9); 0811 B: the same bracket (`PLATE ONLY`: copy `08B11-PLP-01`, vault `08BXX-PLP-01`). Admin chose (b): `replaced_partnos` for this one file in LS-08B-06 and LS-08B-11 makes the copy's old numbers a note tied to decision 9 (ledger and vault must still match). Now the regression is all reuse, and 0811 B's bracket is reuse with the note (2dae134). 0811 4-size plate passes (D28). Mock vault: a missing file, missing configurations and a changed PartNo all stopped |
| DM stale configuration (SW, 26 Oct) | After Replace Components, DM reports the replaced instance's old configuration. `--swcheck <SwCheck output>` (repeatable): where DM and SOLIDWORKS disagree on what an assembly configuration uses, SOLIDWORKS' list is used (for this screen and the `--with` screens) and the assembly gets a note; assembly configurations no SwCheck covered are listed in one issue. `tools\importplan\swcheck_used.py` also compares a probe with SwCheck output on its own (`--ledger` maps the old names DM keeps inside vault files) | 0811 work copies (2510d/e) vs SW's SwCheck of `08X11-LMW` and `08B11-LMW-01`: the plate is `PL_180x140-Perpendicular slots` in DM, `PL_200x140` in SOLIDWORKS, in all 3 LMW configurations. 0806 vault (`vault-P-2610` vs the B and C `P:\` SwChecks of 24 Oct): 28 / 26 assembly configurations compared, 2 differ, the same plate in `08X06-LMW` [02A] and `08X06-SFW-01` [Default]. 0806 A on `P:\` (SW's `swcheck-vault-LS-08A-06+STRUCTURE.txt`): 29 compared, the same 2 differ, all of A's own assemblies match. 0811 pre-r1 full trees vs 2510e: A 22 compared, 2 differ (the plate in `08X11-LMW` 01A, 02A); B 24 / 3 and C 22 / 3: the plate, plus `SF01-UPPER` [Default] where one tie is `TIE-460-WELDED` in DM, `TIE-385-WELDED` in SOLIDWORKS (SW confirmed: 3 x 385 + 11 x 460 in A, B and C; DM stale in B and C) |
| D10 | SW splits the brace in `-r1`: `08X11-DBP-01` (A, C) and `08B11-DBP-01` (B). The old `LS-08X-11_BRACE` still there = "decide"; a PartNo on an `<As Welded>` configuration of the new files = "decide" (prep deletes James's `-BRP-01`) | Mock (brace renamed in a copy of A's probe): both `-BRP-01` PartNos flagged |
| D26 (Weight formulas) | Flag (doesn't block): each Weight formula must read its own file; `-r1` reports any still foreign. New flag: an imported file's Weight formula reads another file's mass (the Importer only rewrites this plan's renames) | 10 flags: B `SF01-UPPER`, `SF02-UPPER` (the shared `08Z11-SFA-2T`) read A's; C's beams and `SF01-UPPER` read B's; A `TIE_BEAM_2.SLDPRT` reads its assembly; splice kit reads the plate |
| Other files | `others.csv` in the probe; loose non-SOLIDWORKS files listed as left out; an equations `.txt` is a "decide" question; a linked file of a reused part is left out | Hilti `.stp`: left out (its part is in the vault) |

Run order this needs: A dry run, A apply, then B (its reuse rows need A's files in the vault), then C.

Waiting on SW's prep before `-r1`: D7 (STA), D14 (LCMM split), repoints of ties, post and member to their vault names, the two interface-plate files sharing a new name, D16, D21, D24 (post repoint), D10 (brace split), D25 (all 7 file-level properties). Until the beams are saved in SOLIDWORKS, they still name the old post, so the plan sees `08XXX-HBW-01` as unused and leaves its drawing out; `-r1` should fix that. D15 (no old grout kits), D10 (no unsplit brace) and D14 (no old LCMM or 2-size plate) are "decide" rows if `-r1` still has them, so the export stops on them.

D29 settled (Admin, 26 Oct): shaft codes stay (08Z11 for D20, 08X11 for A+C), hold lifted. D28 done: Grant fixed the drain hole in vault `08XXX-PLP` by check-out. Fresh vault probe `scratch\inventory\vault-P-2610` (94 files): configurations and PartNos identical to `vault-P-2510d`; 0811 A on 2510 passes the vault check. Re-probe `P:\` again right before the `-r1` plans.

**-r1 A, first plan (26 Oct, no SwCheck yet, drawings not copied in):** `scratch\import\LS-08A-11-r1-import-plan.xlsx`, vault probe `vault-P-2610b` (unchanged). 40 files: 17 import, 17 reuse, 3 modelling groups, 1 keep name, 1 drawing; 9 Vault changes (D17 fastener PartNos). Clean: D10 (`08X11-DBP-01`, no As Welded PartNo), D14 (`08X11-LMW`, old LCMM and 2-size plate gone), D15, D21, D25 (STA has no file-level properties), D26 (splice kit now a note), vault check. Still stopping: `TIE_80x40x8` (D30: SW repoints every instance to the vault's `GNRIC-BRP`/`GNRIC-BRA` and redoes -r1 for all three; the plan stops on any left). D31 in: the tops become `LS-08x-11+STRUCTURE` in `APEX\SC08\LS-08X-11`, no PartNo (A's `-r1` top has no properties). App confirmed `pdm.exe` reads only the reference table. Pack and Go layout `PDM-work\LS-08A-11Év3\...` is the same as 0806 B/C: no move needed. Component paths into other work copies: the Importer and SwFixRefs match by file name, and the after-import gate reads the reference table, so no false stops.

**-r1 planned (26 Oct).** Vault probe `vault-P-2610c`; all three with `--swcheck` (the 3 `-r1` tops) and `--vault`; plans and exports in `scratch\import\LS-08x-11-r1-import-plan*`.

| Screen | Files | Import | Reuse (vault / earlier screen / D20) | Left out | PartNos (Provisional) | Decide | Dry run |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A | 55 | 36 | 19 / 0 / 0 | 0 | 25 (9) | 0 | 1 problem: `08A11-HBW-03` on TIE_BEAM_3 at both config (As Machined) and file level |
| B | 61 | 33 | 21 / 6 / 0 | 1 (`08X11-DBP-01`, carried along) | 21 (9) | 0 | only "reused file not in the vault" for A's 6 (expected before A is applied) |
| C | 49 | 18 | 19 / 7 / 5 | 0 | 11 (3) | 0 | only "not in the vault" for A's 7 and B's 5 (expected) |

**D32 (26 Oct):** plan writes the beams' numbers at the As Machined configuration, Provisional (beam 1 added, `D32`); a file-level PartNo still on any of the 9 beam parts = "decide". `DmEdit delprop <file> <config|@file> <name>` added (deletes, saves, re-opens and checks it's gone; DmEdit now refuses to write anything under `P:\` or `C:\PDM\`). Tested on a scratch copy of A's TIE_BEAM_3: only that property changed (probe before/after). D32 widened (Admin, 26 Oct): every new import with a file-level PartNo is "decide"; the beam assemblies `08x11-HBA-01..03` and `SF01-UPPER` move to Default, Provisional. Full delprop set for SW: `scratch\import\0811-r1-D32-delprop.csv` (24: A 7, B 10, C 7). Real -r1 now: 7 / 10 / 7 decide; mock with the 24 deleted: 0 decide, every James number at configuration level.

**Final -r1 run (26 Oct, after SW's 27 D32 deletes; D33 on):** plans A 55 / B 61 / C 49 files, 0 decide; PartNos 22 / 18 / 8, all Provisional. Plan fix: a decided number names the file before the register (TIE_BEAM_2/3 were about to be named HBP-01/HBP-02). Dry runs: A clean; B / C only "not in the vault yet" for A's / B's files. Rehearsal (scratch copy of P:\): A, B, C import, SwFixRefs (15 / 16 / 7 repointed) and gate all pass; probe 0 outside, 0 broken. **Stopped before P:\:** SwCheck of the rehearsal tops: errors=none, all paths inside, but 155 / 156 / 183 components NOT LOADED (state unreadable). They are exactly the components shown suppressed before import (A 173+155 = 328, etc.); every unsuppressed component loads (1381 in A, same as before). 0806 on P:\ had 0-1. 08X11-LMW opened on its own after import is clean in all 3 configurations. Reported to Admin; not applied.

**Cause and fix (26 Oct).** Bisect (A, scratch): -r1 top 0 not loaded; after Importer 163; after SwFixRefs 135; after a SOLIDWORKS resave of 08X11-LMW and 08A11-SFA, 0. The Importer's Document Manager renames leave stale instance names in the assemblies (NeedsRegen); on a configuration switch SOLIDWORKS loses the suppression of the renamed components (08A11-SFA PART 1 -> PART 2: 9 components in 08X11-LMW-9 active with no path). New read-only `tools\SwSuppDump` shows it (every configuration of every sub-assembly). Fix: SwResave (SW's) of every imported assembly after SwFixRefs. Full rehearsal with it (`rehearsal3`, legacy SwResave one file per start): every resave saved, gates 0, SwCheck A / B / C 0 not loaded (328 / 365 / 328 suppressed, as before import), SwSuppDump nothing missing in any configuration. Admin approved SwResave on this import's new files only; SW built `SwResave-import.exe --import <files.csv> <vaultRoot>` (allow-list, order from references). 0806 on a scratch copy of P:\: SwSuppDump of the 3 tops, every sub-assembly configuration: only the known `08BXX-MSW-01` in 08B06-STA-01 (Grant confirmed fine); no latent fault.

**0811 A, B, C imported into P:\ (26 Oct, 10:26-10:50, Grant's OK via Admin).** Rehearsal 4 first (SwResave-import, scratch): clean. P:\ unchanged since vault-P-2610d (re-probe 2610e identical). Per screen: Importer --apply (all checks passed), SwFixRefs --apply (15 / 16 / 7), SwResave-import (15 / 13 / 6 assemblies, all saved), gate 0. SwCheck of the three P:\ tops: errors none, 0 not loaded, 0 outside, 0 work/inbox paths, 328 / 365 / 328 suppressed (as before import); A and B report only the accepted ReadOnly warning (from read-only vault files they use). SwSuppDump: nothing missing in any configuration. DmProbe of P:\APEX after: 177 files all open, 438 references all inside P:\ (the fastener links resolve in P:\_Shared\_Fasteners). Ledger: 135 rows (A 58, B 51, C 26). Logs in the session scratchpad (import-P-0811*). Not done: D17 fastener PartNos (Grant's check-out); App's load.

Vault changes (D17), all from A: 9 fastener configurations need a PartNo by check-out (`FAST-HEX` M10x140, M10x60, M6x50, M8x55, M8x90; `FAST-NUT` M6, M8; `FAST-WSH` 6, 8); only M10x60 is reached by the tops. Open: James's beam numbers sit at file level, so the plan carries them onto the `<As Welded>` configurations too (0806: As Machined only); asked Admin.

Rehearsal (trial, not `P:\`): A, B, C applied in order into a scratch copy of `P:\APEX` + `P:\_Shared` with a copy of the ledger (A's TIE_BEAM_3 file-level row dropped): all checks passed; probe of the result: 181 files, all open, 438 references, 0 outside the vault, 0 broken; C's STA points at `ShaftBC\08Z11-*`. SwFixRefs not run (SOLIDWORKS open).

Gates made strict (Admin): the Importer's after-import check fails on any reference under `C:\PDM-work\` or the Pack and Go, even to a left-out file, and its pre-check now stops (was a warning) on a left-out file still named by a suppressed component; the SwFixRefs gate (no `--apply`) fails on any stored link outside the vault. Built; not yet seen failing on a real case.

Run order with D20: plan A, export; plan B `--with` A; export; plan C `--with` A `--with` B. Apply A, then B, then C (C's reuse rows need B's `08Z11` files in the vault).

**Next:** plan from SW's `-r1` probes, dry run, report to Admin.

## Earlier: 0806

Screen 06 (A, B, C) is imported and loaded in the PDM. Small tool fixes noted for later: SwCheck counts the ReadOnly warning on a PDM vault, and it misreads the `08BXX-MSW-01` component as not loaded (Grant confirmed it's fine).

## 0806 B and C: planning history (brief `docs\briefs\import-0806BC.md`)

**Step 4 (24 Oct):** planned from `-r1` (`E:\PDM-work\LS-08B-06-r1`, `-C-06-r1`) with the ledger and `data\` register. Reuse added to the tools (94b3465): `export_actions` writes `reuse` rows, the Importer checks each reused file is in the vault and points the new files at it (no copy, no change, no ledger rows), SwFixRefs maps old names of reused files. The plan tool no longer doubles the extension on reuse rows. Structure assemblies get `08B06-STA-01` / `08C06-STA-01`, Provisional (decision 1, "as in A").

| | B | C |
| --- | --- | --- |
| New files | 19 (17 + 2 expansion-joint brackets) | 17 |
| Reused from the vault | 37 | 37 |
| PartNos | 18 (15 Provisional) | 15 (15 Provisional) |
| Dry run | Passed as a **what-if** with the brackets as `08BXX-MSP-01` / `08BXX-MSW-01` in `APEX\SC08\_Shared` (Admin's proposal, not decided; `scratch\import\LS-08B-06-r1-whatif-08BXX-import-plan.xlsx`). The real plan stops on them: the concrete-beam bracket is a family mixing `08B11-MSW-01` (Default) and `08B11-PLP-01` (PLATE ONLY, not used here) | Passed |
| Changes to existing vault files | None: every configuration B/C use in a reused file exists and has its PartNo (checked against A's r4 probe and the ledger, not the live vault files) | None |
| SwCheck of the tops for equations links | Not yet: SOLIDWORKS was open | Not yet |

**Decision 9 applied (24 Oct):** brackets `08BXX-MSW-01` (PLATE ONLY = `08BXX-PLP-01`, same file) and `08BXX-MSP-01`, Provisional, `APEX\SC08\_Shared`. The Importer now accepts a name/PartNo mismatch whose source is a decision, as a note. B re-planned and dry run passed: 19 import, 37 reuse, 19 PartNos (all Provisional), 0 problems, 1 note (the decided mismatch). Reused files checked against `P:\` itself (read-only probe of `P:\APEX` and `P:\_Shared`, `scratch\inventory\vault-P-2026-10-24`): all 37 there, every configuration B/C use exists with its PartNo, and each reused file's configurations are identical in B's/C's copy and the vault. Next: SwCheck the r1 tops (SOLIDWORKS still open), then apply C, then B, gate, SwCheck on `P:\`, tell App.

Earlier: work copies (SW, 23 Oct) are now `E:\PDM-work\LS-08B-06\311v3\...` and `...\LS-08C-06\311v3\...` (`PANELS\` beside `311v3`), no "Dropbox" in the path; probes in `scratch\inventory\LS-08B-06`, `LS-08C-06`. Dev's SwCheck of the earlier copies (23 Oct, reported to SW): end-plate and both skeletons have linked equations files; part links into `PDM-inbox`. Dev's part is step 4: plan from `-r1` with the ledger (files already in the vault reused, not imported), list every change B/C needs to an existing vault file, dry run, report to Admin.
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

## Done: 0806 B and C imported (24 Oct)

SwCheck of the r1 tops: no equations links; C clean; B's concrete-beam bracket component reports no model although it is fully resolved, visible and loaded from inside the root (an API quirk in SwCheck's reading, seen in the work copy too). Applied C then B: C 17 files, B 19, 37 reused each; SwFixRefs repointed 9 in each (new files only); gate 0 outside. No existing vault file was written (none modified in the last 2 h except the 36 new). SwCheck on `P:\` (`LS-08B-06+STRUCTURE`, `08B06-STA-01`, `LS-08C-06+STRUCTURE`, `08C06-STA-01`): errors none, everything inside `P:\`, no equations links, no Dropbox. Each "problem" is the ReadOnly load warning (reused A files are read-only in the PDM, expected) plus B's bracket quirk. Ledger rows added to `data\rename-ledger.csv`. App told to load.
Grant opened `LS-08B-06+STRUCTURE` from `P:\`: the expansion-joint bracket shows correctly in `08B06-STA-01`, so the SwCheck "not loaded" is a tool quirk.
To fix later: SwCheck should not count the ReadOnly warning on a PDM vault; find why `GetModelDoc2` is null for the `08BXX-MSW-01` component.

## Moving to the laptop

What the next Dev session needs (everything in git comes with the clone):

| Item | Where / how |
| --- | --- |
| Repo | Clone; read `dev\CLAUDE.md`, this file, `docs\briefs\import-0806BC.md`, `docs\imports\LS-08BC-06-decisions.md` |
| Build | PowerShell: `cd <repo>\tools; .\build.ps1` ("All tools built.") |
| Document Manager key | User environment variable `SW_DM_KEY` (System Properties > Environment Variables; Grant has it) |
| Python | `python` on PATH with `pip install openpyxl` |
| Numbering register, ledger | In git: `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx`, `data\rename-ledger.csv` |
| Vault | `P:` mapped to the laptop's vault copy (App sets this up). Never touch `P:\.pdm`; vault files change only through `pdm.exe` |
| From `scratch\` (gitignored), only if more planning is needed | `scratch\inventory\<screen>` probes (B/C r1, A r4, `vault-P-2026-10-24`) and `scratch\import\` plans and reports. Not needed for routine work once B/C are imported; the reports are also summarised here |
| Pack and Go sources | `E:\PDM-work\...` on the desktop only. Re-planning needs them, or a fresh Pack and Go on the laptop |
| SOLIDWORKS | Closed before SwCheck / SwFixRefs. If it won't start from the tools, start it by hand once, clear any licence message, close it |

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
| For an import: inputs not in git | `scratch\` is gitignored, so copy the screen's probe (`scratch\inventory\<screen>`) across, and the Pack and Go source folder. The numbering register is in git (`data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx`), so it comes with the clone | |
| Plan command | `python -I tools\importplan\make_plan.py scratch\inventory\<screen> data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx scratch\import\<screen>-import-plan.xlsx data\rename-ledger.csv` | |
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
