# Brief: import 0811 (LS-08X-11, shafts A, B and C)

From Admin, 25 Oct 2026. Same pattern as 0806 (`docs\imports\LS-08A-06-decisions.md`, `docs\imports\LS-08BC-06-decisions.md`, `docs\briefs\import-0806BC.md`). Everything runs on Grant's laptop; part of it on a flight with slow wifi, so keep downloads and uploads small. Report in your own `status.md`.

## Where things are

| Thing | Where |
| --- | --- |
| Live 0811 models (read-only) | `M:\311v3\SC08\LS-08X-11` (`M:` = `C:\Users\grant\Dropbox\Unilumin-Private\Projects\DL APEX\Models\Screen Models`, available offline). James isn't editing on 25 Oct. |
| Pack and Go target | `C:\PDM-work\inbox\<top>` (not Dropbox: same machine now) |
| Work copies | `C:\PDM-work\<screen>` |
| Vault | `P:\` = `C:\PDM`, 94 files (0806 A, B, C and `08A06-HBA-01.SLDDRW`). Vault-tracked: see the rules in your `CLAUDE.md`. |
| Ledger, register | `data\rename-ledger.csv`, `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx` |
| Earlier 0811 probes (desktop) | `C:\Users\grant\Dropbox\PDM-inbox\scratch\inventory\LS-08A-11`, `-08B-11`, `-08C-11` (old, for comparison only) |

## What's new compared with 0806

1. **Drawings.** The live folder holds 52 `.SLDDRW`: one per structure (`08A11-STA-01`) and per part or subassembly (`FRAME\08A11-HBA-01`, `FRAME\ShaftA\08A11-SFA-1T`, ...), many already named with James's numbers. This is the first import with drawings. A drawing carries its model's number and has no PartNo of its own (App 0.1.5). The app's Document Manager read failed on drawings until 0.1.6 (it asked a drawing for configurations); Dev's tools probably have the same gap. Dev checks the probe, Importer and SwFixRefs on a drawing first (App can say exactly what it changed).
2. **James's numbers are already in many file names** (`08A11-...`, `08B11-...`, `08C11-...`). Check them against the register and the ledger; don't renumber without asking.
3. **Reuse.** Many 0811 parts are 0806's shared files already in the vault (ties `GNRIC-BRP`/`GNRIC-BRA`, end plate, bolt kits, fasteners, `08BXX` brackets). As with B and C: reuse, don't import. Any change 0811 needs to an existing vault file (new configuration, new PartNo, new reference) is listed first, then done by check-out and check-in.
4. **LCMM:** 0811 takes `08X11-LMW` and `08B11-LMW` (A decisions: split by place tag).
5. **Screen file:** one `LS-08X-11` with configurations `LS-08A-11` and `LS-08BC-11` (A decisions, "Order and 0811").
6. **Grout and anchors:** decision 7 (B/C decisions) is for all screens: sketch pattern, anchors in the `STA`, grout in the top. Report how 0811 does it now; no change without Grant's OK.
7. **Known loose ends:** the end plate's equations link to `LS_08A-11_Tie_Bar_equations.txt` (0811 is that file's home); two conflicted copies of `LS-08A-11 - SCREEN + STRUCTURE` in the live folder, which aren't imported, but check nothing was lost; CSV, PDF, STEP and TXT files aren't vault files.

## Steps

| # | Who | Step |
|---|---|---|
| 1 | Grant (SW guides) | Open each top from `M:\` (`LS-08A-11 - SCREEN + STRUCTURE`, then B, then C, closing all between), then Pack and Go with **Include suppressed components** and **Include drawings** ticked into `C:\PDM-work\inbox\<top>`. |
| 2 | SW | Copy each into `C:\PDM-work\<screen>`, probe, and list for Admin: new files; files already in the vault (by ledger) and whether 0811's copy differs; the drawings and what each references; the items above. No file changes until Admin agrees the list with Grant. |
| 3 | Dev, in parallel with 1–2 | Make the tools read drawings (test on a SOLIDWORKS sample drawing, not APEX). Get ready to plan from SW's probe. |
| 4 | Grant, SW | The agreed fixes, then a clean Pack and Go `-r1` per top. |
| 5 | Dev | Plan from `-r1` with the ledger. List every change to an existing vault file. Dry run. Report to Admin. |
| 6 | Dev, Grant, App | Import new files. Changes to existing vault files: Grant checks out, Dev applies, Grant checks in. SwCheck on the tops. App runs `pdm.exe load --dry`, then loads (upload when the wifi is good enough). |

Proposal, open: A, B and C in one pass rather than A first, since the pattern is proven. SW says if the probe gives a reason to split.
