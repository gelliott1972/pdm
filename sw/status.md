# SW status

## 28 Nov (morning): B rework (Desk A30 / APEX-A130) on copies, in progress

- Grant: the posts get 10 mm longer at the bottom only; the screen frame stays put. The first B dropped the SFA-2B/5B subframes 10 mm, so it was not accepted.
- Copies: `C:\PDM-work\weekend-2026-11-28\B2\LS-08A/B/C-04` (fresh Pack and Go; vault versions as in B's run, apart from FAST-HEX's E config).
- Script: `sw\weekend\b04r.ps1` = b04.ps1 + SwGrout movecomp, which moves GNRIC-LM-1 10 mm down in 08X04-LMW's 9 B configurations (fixed component, per configuration; the other configurations are unchanged).
- A-04 only: 08A04-SFA-2B Coincident26 (the LMW-3 leg bottom to the 08X04-HBW-04 underside) is replaced by Distance5 = 10 mm (SwGrout todist), so HBW-04 and GNRIC-BRP-16 stay put.
- A-04 proof (`B2\proof`):
  - Whole-top xforms (bdiff.py against B's before): only 10 GNRIC-PLP, the MSW-01 kit, the M12 kit and 11 legs moved (0,-10,0); the legs changed configuration with the top unchanged. Nothing else moved.
  - Every-bolt: the same 3 pre-existing problems.
- Next: B-04 and C-04 xforms (running), any re-mates there, the floor check (about 12 min each), the 04/07 foot, then check-out and check-in (APEX-A126). After that, D (APEX-A128).

## 28 Nov (night): weekend E checked in; square-edge exports done

- **E (APEX-A124, joint 8.2) checked in** at 2026-11-27T19:08:55Z, one pdm load, 19 files, 0 locks. New parts: 08X11-BRA-01 rail, 08X11-MSP-01 seat, and FAST-HEX-8.8-HDG config "ISO 4014 - M12 x 65-N" (FAST-HEX-M12x65-8.8-HDG).
  - Per screen A/B/C-11: 12 GNRIC-BRA rails, 24 MDL-80X40 kits and 24 GNRIC-MSW-01 seats out; 12 rails, 24 seats and 24 M12x65 sets in.
  - Proofs on P:\: every-bolt A 288, B 175, C 192, all 0 problems; 24 M12 through seat and rail on each; 0 other moves. Folder: `C:\PDM-work\weekend-2026-11-28\E\proof`.
  - Fixed on the way: one A-11 seat (08A11-SFA-3B) that sat 10 mm into its rail.
  - Pins: A/B/C-11 now at 19:08:55Z.
- **Square-edge exports for revit-fiona** (Admin told):
  - LS-08A-11: `runs\LS-08A-11 2026-11-27T2001Z`. New parts squared with --plain-shape; only the rail end caps keep R3 corners.
  - LS-08A-06: `runs\LS-08A-06 2026-11-27T1936Z`.
  - Both at pin 19:08:55Z. Run `LS-08A-11 2026-11-27T1916Z` is superseded.
- **For the morning:** the 11 STA and SFA drawings show the old rails and seats as missing until redrawn (Draw or James). B is held (Desk A30), D is held (Desk A31).
- SOLIDWORKS lock released.

## 25 Nov (evening): standing by

- LS-08C-11 square-edge export after C18: `runs\LS-08C-11 2026-11-25T0603Z`, OK, pin 05:52:27Z.
- James's 01:46Z compare: only 08X06-SFP-03 changed (2 new Ø10 drain holes, -9.8 g); the rest identical (`C:\PDM-work\c18\jcmp`).
- Plain 0807 exports for wireframe: `runs\LS-08A-07 plain 2026-11-25T0629Z` (pin 15:39:44Z), `runs\LS-08BC-07 plain 2026-11-25T0637Z` (15:39:48Z). Stairs: 65 tread tiles face up, 39 riser tiles +Z; "front" = risers.
- Squaring: one call per part, 75 min for parts with more than 4 configurations (08X07-SFP: 39 min). 08X07-SFP squared copy in the cache. Test `runs\LS-08A-07 test 2026-11-25T0744Z`: LED lines as plain; 18 moved = 3 floor plates (08A07-SFA-1X PLP-10, -9; SFA-4X PLP-9) and their anchor kits half-turned (plates not fully constrained). Desk A20 (later): fix just before the Revit stairs export. LS-08BC-07 square-edge waits for apex-admin.

## 25 Nov (afternoon): APEX-A93 renumber, DBW-01 kg, C18 LS-08C-11 braces checked in

| Job | Vault times | Notes |
| --- | --- | --- |
| Renumber 08A11 brace parts -> `_Shared\08X11-HBA-04/-HBW-04/-HBW-05/-DBW-01` (R1-R5; Admin's pdm move R4) | 02:43:41Z-02:44:16Z | One SOLIDWORKS session (SwGrout renumber); PartNo set, Weight/Material links follow; LS-08A-11 positions and masses identical; 08X11-HBA-04-24 is now -26 |
| 08X11-DBW-01 PartNo/Weight, then mass unit kg (SwKg) | 02:43:56Z, 03:33:39Z | Weight 18.80, mass unchanged |
| C18: LS-08C-11 gets A's two sleeve braces mirrored (same 08X11-HBW-05, Grant A19 = A) | 05:52:15Z-05:52:27Z | 08C11-STA-01, -HBA-01, -HBA-03; all 8 placed parts at A's mirror (0.000 mm), plate centres mirrored, 1396 -> 1400, nothing else moved, 0 errors, drawings clean |

HBW-05 and 08XXX-HBW-01 both have one 10 mm HDG hole on the -x wall only; nothing sits in it on LS-08A-11. LS-08A-07 square-edge export failed (08X07-SFP squaring > 30 min): plain 0807 exports next, squaring per configuration later. Scripts `sw\grout\renum0811.ps1` (first try, superseded by SwGrout renumber), `c18.ps1`, `c18_check.py`.

## 25 Nov: LS-08B-11 doubled components (C16, Desk apex-admin-21) checked in; stopped while Grant moves

Vault times: 08B11-HBA-01 01:22:45Z, 08B11-STA-01 01:22:52Z (new LS-08B-11 pin 01:22:52Z). Deleted spigot 08XXX-HBW-01<1>, sleeves 08XXX-HBA-01<23>/<16>, bolt kit MDL-ROOF-SLEEVE-BOLT-08XXX<17> (+16 fasteners). Kept the copies other things use (<19>: Interface_Plate_Layout_Sketch pattern; <24>: STA drawing balloon 6; spigot <2>: HBA drawing balloon 2 and D14); kit 12, sleeve 24 and kit 18 re-mated to them (SwGrout movemates); Roof_Plate_Layout_Sketch point re-pointed to <24> (fixrel). Checks: 1350 -> 1330, 0 other moved, kits 9 -> 8 all 3 mates, 0 errors, 0 sketch warnings, both drawings 0 dangling. New SwGrout commands: movemates, fixrel, delinst; SwNoteAt (what an annotation is attached to). Scripts `sw\grout\c16.ps1`, `c16_check.py`; data `C:\PDM-work\grout\c16`.

Queue (Admin): 0807 square-edge exports (skipped for time), renumber of the 4 brace parts (3a), C18 LS-08C-11 braces (3b): none started; wait for Admin's word.

## 24 Nov: 0807 floor anchors (APEX-A84) checked in

Admin's brief (Desk apex-admin-13, -15). pdm get, check-out 3, rehearsal on Pack and Go copies (`C:\PDM-work\grout\anc07`), real, dry load exactly 3 changed, `pdm checkin`, dry load 0. Vault times: MDL-ANCHOR-GNRIC 15:39:39Z, 08A07-STA-01 15:39:44Z, 08Z07-STA-01 15:39:48Z.

| Check | Result |
| --- | --- |
| Kit | New FLOOR_150X200_M12x185 (D1 60 -> 35: anchors x ±35, z ±60); FLOOR_200X200 and both ROOF configurations unchanged |
| Each STA | 28 kits (22 FLOOR_200X200 + 6 FLOOR_150X200), one per GNRIC-PLP floor plate, 4 x M12x185 each, 3/3 mates, 4/4 anchors on holes, at plate top Y -884.237 |
| Positions | +STRUCTURE tops 322 -> 462; 0 earlier components moved; 0 errors |
| LS-08A-11, LS-08A-06 | 120 kit and anchor components unchanged after the kit change; 0 errors |
| Half turn | 3 kits in 08A07 settled half a turn about the anchor axis; same holes (SwGrout now accepts and reports it) |

Scripts `sw\grout\anc07.ps1`, `sw\grout\anc07_check.py`.

## 24 Nov: square-edge exports (SESTP) of LS-08B-11, LS-08C-11, LS-08B-06, LS-08C-06 for SAP2000, all OK

Admin's go. Before each export, a floor-plate check against its A screen (new read-only `SwGrout floor`, summary `sw\sestp\floorsum.py`; data `C:\PDM-work\sestp\floor-1124`). Nothing in the vault changed. Pin 2026-11-24T13:33:43Z for all four.

| Screen | Floor plates (as its A) | Run | END STEEL any / members |
| --- | --- | --- | --- |
| LS-08B-11 | 12 at -5590, gap 0, nothing under | `runs\LS-08B-11 2026-11-24T1356Z` | -25/-25, -55/-55 |
| LS-08C-11 | 12 at -5590, gap 0, nothing under | `runs\LS-08C-11 2026-11-24T1418Z` | -25/-25, -55/-55 |
| LS-08B-06 | 10 at -4910, gap 0, nothing under | `runs\LS-08B-06 2026-11-24T1435Z` | -25/-200, -55/-230 (mirror of A: expected, apex-admin) |
| LS-08C-06 | 10 at -4910, gap 0, nothing under | `runs\LS-08C-06 2026-11-24T1454Z` | -25/-200, -55/-230 |

Sandbox copies partly left ("in use"): run `sestp.py tidy` later.

## 24 Nov: APEX-A86 GNRIC-PLP FP-150X200 anchor holes 15 -> 14 mm, checked in

Admin's brief (Grant, Desk apex-admin-14). pdm get, check-out (2), rehearsal on copies, real change (SwGrout setdims, FP-150X200 only), drawing GNRIC-PLP-01 rebuilt and saved, dry load exactly 2 changed, `pdm checkin` with the note, dry load 0 changed. Vault times: part 2026-11-24T13:33:43Z, drawing 13:33:51Z.

| Check | Result |
| --- | --- |
| FP-150X200 | Thru Hole Dia. 15 -> 14; 4 holes at 14 mm; mass 2.2849 -> 2.2920 kg |
| Other 8 configurations | Unchanged (FP-200x200 still 14, FP-150X200-CHAMFERED_CORNERS still 15); masses identical |
| GNRIC-PLP-01 | Hole callout "4 x Ø 14 THRU" (linked); only that dimension changed; 10 dims, 0 dangling, 0 errors |

Not touched: 08X04-LMW and the 18 SFA subframes (they pick it up at their next rebuild). Data and backups `C:\PDM-work\grout\plp-a86`; checks `sw\plp-a86.py`.

## 20 Nov: the 27 parts + 08X04-SFP-03 (Grant's go) done; white balloons looked at on one drawing

**Run (data `scratch\profiles\real-1120\`):** pdm get, inventory (28 parts used by 203 drawings and 100 assemblies), before checks with values, check-out, backups, SwFix, SwRedim, after checks, dry load exactly 86, `pdm checkin` with 3 notes, dry load 0 changed.

| What | Result |
| --- | --- |
| Parts | 26 of 27 moved to the vault profiles and checked in; 08X04-SFP-03 too (only the accepted 93436.1589 -> 93436.1427 mm³ body, SwFix `--accept-vol`) |
| 08X04-HBW-02 | **Not changed, released.** Its volume reads wobble about 0.014 mm³ between runs, so SwFix refused it; waiting on Grant (leave as is) |
| Drawings | 59 redone with SwRedim and checked in. All 303 users pass (same values and counts, no new dangling or errors); 25 show new cut-list labels |
| Decimals | 08X04-DBP-01, 08X06-DBP-01/-02 print as before; 08XXX-HBA-01 also needed H_leg = 1 decimal (original "120.0") besides the 6.3 |
| 08A04-SFA-01.SLDDRW | New since the rehearsal (V_leg of 08A04-HBW-01). First pass took GNRIC-LMW's V_leg; restored from backup, SwRedim now matches name and part, redone, passes |
| 08X04-DBP-01 | A hidden V_leg (not printed or counted) was not put back, as in the rehearsal |

Scripts `sw\profiles\the27-{jobs,users,real-redim,redim-list,real-compare,decimals,words,checkin}.py`; refcheck.py `--detail`.

**White balloons (Grant, high priority):** one drawing only, 08A04-SFA-01 (James's): all 12 balloons and 20 of 25 dimensions have their own colour set to white (255,255,255), overriding the default. No layer or template involved. Grant's system colours are black. Most likely James sees black because his paper is white (Grant's is beige R230 G230 B219); not tested. Fix (vault change): set those annotations' colour back to default. Tool `tools\sw-macros\SwAnnColor.cs` (read-only). Waiting on Grant via Admin: fix how, and whether to look wider.

## Weldment profiles into the vault (brief sw-profiles, 16-17 Nov 2026): night run done, 63 parts checked in 03:30

Result table and the 27 parts left unchanged: `sw\profiles\survey.md` section 3. In short: rehearsal 90/92 passed; real run 90 saved; drawing checks failed on 59 drawings (3 profile-sketch dimensions each became dangling), so their 27 parts were restored from backup and released unchanged; 63 parts checked in (repoint to `P:\_Shared\_Profiles`, labels, end caps, plates, Material, 08B04-BKP-01 weldment). 13 released 0A drawings (LS-08X-06) now show changed BOM text; draft PDFs of 9 unreleased drawings in `C:\PDM-work\profiles-pdfs`. File Locations steps for Grant and James: `sw\profiles\file-locations.md`. Open for Grant (via Admin): the 27 parts, 08B04-KBP-01, 08C04-SFP-04, the 08X04-BKP plate label, re-issue of the 13 released drawings.

17 Nov morning (Admin/Grant):
- **08X04-BKP plate label**, tested on scratch copies only, steps sent to James via Admin. A typed cut-list Description is shared by all configurations. The expression `PLATE, 10 x 140 x "D2@Sketch3@08X04-BKP.SLDPRT"`, with D2's unit precision set to None, reads 150 or 180 per configuration and in drawings 08X04-BKP-02..05. Tool: `tools\sw-macros\SwDescTest.cs`.
- **FAST-HST3-R-M12x125-A4**: Material set to AISI 316 Annealed Stainless Steel Bar (SS), plus the file Material property. Shape and assemblies unchanged; checked in.
- **The 3 PANELS parts** need no Material (Grant).
- **08BXX-MSW-01 weldment** (Grant yes, via Admin): rehearsed on a copy (`scratch\profiles\msw01\`), **stopped before the real run**; vault file untouched, not checked out.
  - Copy: Weldment added. Default becomes Default<As Machined>/<As Welded>; PLATE ONLY keeps its name; PartNos carried over. Volumes, box and mass are the same (8.491 / 6.195 kg).
  - Labels on the copy: "PLATE, 10 x 200 x 400"; "EA100x100x8" with LENGTH 100. The angle MATERIAL text didn't set: same rewrite as 08B04-BKP-01.
  - Users (12): every assembly has 0 errors; DW16 has 24 dims, 0 dangling; 08BXX-PLP-01 has 10 dims, 0 dangling. Fails on one: released **08BXX-MSA-01.SLDDRW (0A)** parts-list row 1 PART NUMBER "08BXX-MSW-01" goes blank. 08B11-MSA-01.SLDDRW, same layout, keeps it.
  - ~~Cause: crossed columns~~ **Corrected by Draw (18 Nov):** the columns aren't crossed. PART NUMBER is a hidden column; every printed cell stays the same. SwBomDiag now lists hidden columns.

## Overnight 17-18 Nov (Admin's queue)

**Incident (about 00:10, 18 Nov): I ran an accidental real `pdm load`.**
- I typed `pdm load --help`; pdm treats the unknown flag as a plain load, with no --dry and no note.
- It uploaded 25 stale local files (older than James's check-ins) before I killed it. List: `scratch\profiles\msw01\loaded-by-accident.txt`.
- Reported to Admin and App at once; App is restoring them.
- Rule from Admin: never run pdm without --dry, except for an agreed load or check-in.

Also: my first scratch run started while Draw held the SOLIDWORKS lock (my lock request was refused; the script didn't check). Killed within a minute; nothing saved. The scripts now stop unless the lock is ours.

| # | Job | State |
| --- | --- | --- |
| 1 | 08BXX-MSW-01 weldment (Grant B; Admin: allowed difference = 08BXX-MSA-01 hidden PART NUMBER row 1 blank) | **Done and checked in** (18 Nov, by App on Admin's instruction, after the vault repair; note "Made a weldment so the cut list forms (Grant 17 Nov)"). Draw told for DW16. Allowed difference only. Backup `scratch\profiles\msw01\backup.SLDPRT` |
| 2 | 08X04-BKP plate label | **Done and checked in 18 Nov** (Admin's go; pdm get, check-out, rehearsal on copies, real, dry load 2 changed, `pdm checkin`, dry load 0 changed). Cut-list Description `PLATE, 10 x 140 x "D2@Sketch3@08X04-BKP.SLDPRT"`, D2 no decimals: 150/180 right in all 8 configurations; 08X04-BKP-02..05 plate row blank -> the label, nothing else changed (14 dims, 0 dangling); masses same; 08X04-BKA 0 errors. The 13 KBP parts built from it: Descriptions and masses same; they rebuild on next open (inserted body names change). Data `scratch\profiles\bkp-1118\`, scripts `sw\profiles\bkp-1118*.py` |
| 3 | 08X04-BKP-07 named view "PLATE FRONT" | **Done and checked in 18 Nov** with item 2. Square to the 400x300 plate, model +Y up (SwNamedView fixed: first rehearsal was upside down). Mass and bodies same; 08A04-KBA-06, 08B04-KBA-02, 08C04-KBA-02 0 errors. Draw told (DW19) |
| 4 | Prep for the 27 and 08X04-SFP-03 | Done, scratch only: `sw\profiles\the-27.md`. All 27 auto-fixable (59/59 drawings back to the same values, 0 dangling). On paper: labels change; 4 drawings need decimals set; the profile dims print black, not grey. 52 of the 59 drawings are released 0A. SFP-03: older profile copy differs by 0.016 mm3 only |

### Earlier (16 Nov): step 0 survey

Profile library surveyed (`sw\profiles\survey.md`): rhs.sldlfp's size "RHS150x100x8" draws 80x40x8 and there is no RHS80x40x8 size (08A04-HBW-05 asks for it); EA50x50x6 and SHS100x100x12.5 have wrong descriptions. Parts survey (87 parts, needs SOLIDWORKS) stopped for Draw; runs when the lock is free. Next: parts survey, then scripts and rehearsal. Nothing written to `P:\` or `M:\`.

Steps 9-11 added by Admin (Material on every part; "PLATE, t x w x l" on blank cut-list Descriptions; 08B04-BKP-01 made a weldment). The survey tool now reads materials and measures plates (`sw\profiles\analyse.py` makes the tables). Tools written, not yet tried (need SOLIDWORKS): `SwRepoint` (step 6), `SwWeldment` (step 11). Rehearsal copy of 08B04-BKP-01 in `scratch\profiles\rehearsal\bkp01` (used only in 08B04-STA-01.SLDASM; no drawing yet).

## 08X04-PLP-01/-02/-03/-05/-06 Material (15 Nov 2026, Grant via Draw): done, checked in 20:32

File-level Material = "SW-Material@<own file>" (as 08X04-PLP.SLDPRT), DmEdit, read back. SOLIDWORKS material read first (SwMaterial): PLP-01/-02/-05 Plain Carbon Steel; PLP-03 and PLP-06 had none, so Plain Carbon Steel set (Grant: steel like the others): PLP-03 0.635 -> 4.956 kg (Default), 0.589 -> 4.597 kg (PLATE ONLY); PLP-06 0.023 -> 0.179 kg. Load dry run and load exactly 5 files. Odd, for Draw: PLP-06 "FLOOR PLATE EA BOLT TO STAIRS" is only 23 cm3. Logs `sw\plp-material`.

## Bracket materials and GNRIC-LMW SHS (14-15 Nov 2026, Admin's jobs): done, checked in 15 Nov 17:37-17:39

| File | Change | Check |
| --- | --- | --- |
| `08X04-BKP-01.SLDPRT` | Description L=150 -> L=100 (both configurations); Plain Carbon Steel | 0.147 -> 1.149 kg |
| `08B04-BKP-02.SLDPRT` | Plain Carbon Steel (Desk SW8, Grant: A) | 0.38 -> 2.964 kg |
| `08X04-BKP.SLDPRT` | Plain Carbon Steel, all 8 configurations | 0.31-0.36 -> 2.45-2.79 kg |
| `GNRIC-LMW.SLDPRT` | Description RHS -> SHS 40x40x4mm (file level + 20 configurations, lengths kept); cut-list label RHS40x40x4 -> SHS40x40x4 (one shared value; SOLIDWORKS renamed the folder) | second label run finds nothing left |

How: SwCmd set-material / set-cutlist-prop (Dev) with a look pass first, DmEdit batch for the Descriptions, re-inventory (exactly 23 property changes, nothing else), load dry run 4 files, load. Profile and end cap left as they are (Draw: 32 inside; Grant D9: cap stays 36x36x5). Logs `sw\bkp01`, `sw\lmw`; later clean-up list in `sw\lmw\notes.md`.

## plates-0804 (brief `docs\briefs\plates-0804.md`, 13 Nov 2026): done in the vault

Details and logs: `sw\plates-0804.md`, `sw\plates-0804-run.ps1`, `sw\plates-0804-real`. Six weldments split into assembly (today's number) + weldment-only part (new number) + plates fixed (08X04-PLP-32 x10, -PLP-33 x2, GNRIC-LPP DOUBLE_BOTTOM x1). Rehearsed on a full copy, then run on P:\: masses equal, 45/45 SFA mates put back, six old parts removed, PartNos as P4. 08X04-HBW-05.SLDASM fixed after (Weight reads the assembly, Material removed; checked in 15:33). Open: Grant's check of the six SFAs.

## K-beam packer kit PartNo (13 Nov 2026, Admin's job): done

`MDL-LS-08X-04-KBEAM-PACKERS-BOLTS-08X04.SLDASM` had PartNo "." on 180x140 and 150x140 (the load CHECK). None of the 13 MDL- kit assemblies in the vault carry a PartNo (one has it empty), so both deleted and checked in (15:39); the load CHECK for it is gone. Logs `sw\kbeam-packers`. Load still lists one older CHECK: `08X06-SFW-04.SLDPRT` (not this job).

## 0A white re-issue (10 Nov 2026, Grant's go, overnight): done and checked

Details in `sw\0A-white-pdfs.md`. Dev's SwCmd export-pdf now recolours white annotations in memory (6a60bb0). App re-issued 35 of the 41 drawings as 0A: 63 PDFs, both ZIPs, both lists and the register. SW checked: no white left except the 6 STA-01 drawings (left out by Admin: James's 10 Nov check-ins of shared parts) and the zone ticks on GNRIC-BRW-16 and 08BXX-HBW-01 (invisible before too; accepted). Open: the 6 STA-01 drawings.

## Now: import 0804 (`docs\briefs\import-0804.md`, 4 Nov 2026)

**Step 1 done:** read-only probe of the three v4 tops straight from `M:\` (Document Manager walk through the used configurations). List for Admin: `sw\0804-list.md`.

**Step 2 done (4 Nov):** Grant's Pack and Go per top, Include suppressed components, into `C:\PDM-work\inbox\<top>`. Each pack probed (`scratch\inventory\LS-08x-04-0411`) and compared with the step 1 list:

| Top | Files | Every file the top uses there? | References |
| --- | --- | --- | --- |
| `LS-08A-04_WITH_STRUCTUREv4` | 125 (124 + Hilti `.stp`) | yes; plus 30 screen SKUs/panels from other configurations | all inside, apart from the old `LS-08A-04_SF02.SLDASM` (broken on `M:\` too; not to be imported) |
| `LS-08B-04_WITH_STRUCTUREv4` | 133 | yes; plus 27 SKUs/panels | same |
| `LS-08C-04_WITH_STRUCTUREv4` | 126 | yes; plus 30 SKUs/panels | same |

**Incident (4 Nov 20:06), cleared:** the first A Pack and Go went to the default folder (the top's own folder on `M:\`) and created two new folders of copies in the live Dropbox (`M:\311v3\SC08\LS-08X-04\311v3`, `...\PANELS`, 123 files). No original was changed (checked every `M:\` file's date). Grant deleted both folders; checked gone. The A pack made straight after had picked up those copies and was moved aside (`C:\PDM-work\scratch\0804-A-pack-bad-2019`); A was redone cleanly. A first C attempt packed the A top by mistake (moved aside to `C:\PDM-work\scratch\0804-C-folder-held-A-pack`); C redone. Lesson: in Pack and Go, set **Save to folder** first and check the path before Save. Overwrite prompts for the hex nut are harmless: the models name it with `.SLDPRT` and `.sldprt`.

**Step 4 prep (5 Nov), in work copies `C:\PDM-work\LS-08A-04`, `-08B-04`, `-08C-04` (copies of the inbox packs; the packs moved to `C:\PDM-work\scratch\0804-inbox` so nothing loads from the inbox):**

| Item | State |
| --- | --- |
| D8: file-level PartNo deleted | done (DmEdit batch, read back): A 16 files, B 5, C 5 (the shared `FRAME\_Shared` parts sit in each copy). Backup `C:\PDM-work\scratch\0804-pre-D6-D8` |
| D6: Weight and Material read their own file | done (DmEdit batch, read back): A 5 files (11 values), B and C 2 files each. The vault's `08XXX-PLP` copies left alone (reused, not imported) |
| D12: in-context links to `LS-08A-04_SF02.SLDASM` | done: Grant broke the links in `SF02_TIE_CORNER_RIGHTANGLE` and `TIE_SF02_CORNER_80x40x8_WELDED` (A), then re-tied the plane `RHS_CORNER_MIDPLANE` to Front Plane at 980 mm (measured before/after; mass unchanged). Document Manager: no references left. RIGHTANGLE copied from A into B and C (properties identical; old B/C copies in `C:\PDM-work\scratch\0804-pre-D12-copy`) |
| D11: old grout kits out | done on A, B and C (checked: no grout reference left in any assembly). A: 11 from the top, 14 from `KBEAM_ASSY_SHAFTAv2`. B: 26 from the top (7 Nov). C: 11 from the top incl. the 2 sketch-pattern seed kits (sketches kept), 14 from `KBEAM_ASSY_SHAFTC v2` (7 Nov). Counts to add back after import (Admin): **A 14, B 15, C 14** K-beam grout kits (A and C inside `KBEAM_ASSY_SHAFTAv2` / `KBEAM_ASSY_SHAFTC v2` at `K-BEAM_400X300_BIM`; B's at top level), plus the top-level floor/SF02 kits: A 11, B 11, C 11 (6 FLOOR_150X200_BIM, 3 FLOOR_200X200_BIM, 1 LS-08X-04+07200X300_SHARED, 1 LS-08X-04_SF02_350x200_BIM each) |
| D5: save out A's and C's SF01 virtual parts | done on A and C. A: saved out by Grant, renamed by me (DmEdit) to the PartNo each carried: LOWER_BRACE x2 = `08A04-HBP-02`, HORIZONTAL_TOP_BAR x1 = `08A04-HBW-01`, VERTICALS x4 = `08A04-HBP-01`; SF01 repointed, file-level PartNo deleted, Weight/Material on own file; Grant re-saved SF01 (7 Nov); Dev re-probe clean. Backup `C:\PDM-work\scratch\0804-pre-D5-rename-A`. C (7 Nov): saved out with plain names; they carried no properties at all, so Admin numbered them to match A: `08C04-HBP-02`, `-HBW-01`, `-HBP-01` (Dev: free); renamed, SF01 repointed, Weight/Material added; Grant re-saved SF01; Dev re-probe clean. Their in-context links to SF01 are broken (`->x`, DM broken=1); Dev: the importer repoints them anyway. Backup `C:\PDM-work\scratch\0804-pre-D5-rename-C`. B's waits for D13 |
| D14: loose M12 screw/nut/washers in the A and B tops | **keep** (Grant, 7 Nov: they join this structure to another). Nothing removed |
| D26 and after: B's lost end-cap ties | done 7 Nov. Three mates in `LS-08B-04_WITH_SCREEN` failed: B was modelled against the 0806-style `311v3\_Shared\TIE_80x40x8` (end-cap configurations), but resolves to James's `SC08\_Shared` one, so the instances fell back to TIE-460-WELDED / TIE-710-WELDED. Fixed in `LS-08B-04_SF02-UPPER`: 4 expansion-joint ties -> new `08B04-BRP-01` (from the 19 Sep copy's TIE-145-ENDCAP), 1 top tie -> new `08B04-BRP-02` (TIE-474-WELDED_WITH_END_CAPS), 4 top ties -> a work-copy copy of the vault's `GNRIC-BRP` (TIE-184-1XEC; `311v3\SC08\_Shared\GNRIC-BRP.SLDPRT`, byte-identical to P:\, Dev's copy route as 0806 D24 / 0811 D30). Both new files: one configuration (plus its As Welded child), no PartNo, Weight/Material on own file, no references. Mate errors gone; B saved 10:44. Source copies: `C:\PDM-work\scratch\tie-19sep\` (`TIE_80x40x8.SLDPRT` untouched, `TIE_19SEP.SLDPRT` renamed copy). Left as is: 08B04-BRP-01's child is named `TIE-152-ENDCAP<As Machined><As Machined><As Welded>` (cosmetic, no PartNo) |
| D18: `TIE_80x40x8` -> vault `GNRIC-BRP` / `GNRIC-BRA` | done 7 Nov, DmEdit only (no assembly mixes welded and bolted): replace-ref in 21 assemblies (18 subframes -> `GNRIC-BRP`; A's top, B's and C's `WITH_SCREEN` -> `GNRIC-BRA`) to work-copy copies in each `311v3\SC08\_Shared` (byte-identical to P:\). Old unused `LS-08A-04_SF02` skipped. Backup `C:\PDM-work\scratch\0804-pre-D18`. Dev re-probe OK; B's doubled GNRIC-BRP reference entry and its 4 suppressed TIE-104-EDNCAP are fine for the import (Dev) |
| R2/R4/R5/BRW renames | done and then **undone** 7 Nov: the importer renames these files itself (Dev), so the work copies keep the old names. Restored from `C:\PDM-work\scratch\0804-pre-R2-renames`. Kept: `08B04-BRP-02` got own-file Weight/Material |
| SOLIDWORKS round, 7 Nov (Dev's SwCmd, work copies only; backup `C:\PDM-work\scratch\0804-pre-round`) | done. R3/D15: `08X04-LMW` (20 configurations, from LCMM_40x80x8_SC08) and `08X04-LMW-40X40` (13: 9 shared + A's 2 + C's 2, Grant's SW1 answer A) in each `311v3\_Shared`, identical in A, B, C; all parents repointed (DmEdit). R9: unused configurations deleted (SF04 A 2, B 5, C 5; 4 MEMBER_SHS_40x40x4 lengths; 180 interface plates; bolt kit SINGLE; side walls v2) and always-suppressed components deleted (B's SF01, ARUP beam, inner corner tie, B SF02-UPPER dead items, K-beam bracket packer and bolts, 0811 brace and plates in 08X04-LMW, plus Admin's extras: SF02-LOWER cantilever and tie, SF02-UPPER and SF05-LOWER LCMMs, B's 4 EXP bracket bolt kits, by-others 150 brackets); each tree diff showed only the named item. R1: SwCmd move refused; Grant dissolved B's `SF02-UPPERv2` (Desk SW2), then the plate wasn't needed, so deleted with SwCmd; B saved. Check-open after all: A 2329, B 1953, C 2377 components, 0 missing, 0 outside the work copy, What's Wrong empty (C: 2 harmless foot-plate sketch warnings). **Next:** R6 (Desk SW3, bracket 180 slots), then `-r1` with SwCmd pack-and-go after Dev's scratch test, one top at a time |
| R6 bracket (Desk SW3) | done: Grant: part names right (LONG = slots parallel to 180). Uncrossed the two 180 configurations in `LS-08X-04_KBEAM_BRACKET.SLDASM` (A, B, C, SwCmd set-config per configuration) |
| Fixes after Dev's first `-r1` probe | done 7 Nov: LMW Weight/Material on own file (DmEdit, 31 per copy; backup `0804-pre-lmw-props`); K-beam interface bolt kit nuts and washers M10 (Grant SW4: A); M12x200: Grant added `ISO 4017 - M12 x 200 x 60-N` to the vault's `FAST-HEX-8.8-HDG` (SW5), Dev wrote PartNos (FAST-HEX-M12x200-8.8-HDG, M16 nut and washer), checked in; vault copy in each work copy's `311v3\_Fasteners\hex bolts and screws`, the 2 custom bolts per shaft replaced (SwCmd). A's 7 parts (Skeleton, KBEAM_S1..S6) unlinked from James's equations file, values kept (SwCmd unlink-equations; backup `0804-backup-A-equations`) |
| `-r1` packs (SwCmd pack-and-go, under the lock) | current: `C:\PDM-work\LS-08A-04-r1` (92 files), `-08B-04-r1` (96), `-08C-04-r1` (93); Dev's plans clean. Earlier packs in `C:\PDM-work\scratch\0804-r1-first`, `0804-r1-second`. Dev wrote D10 properties. SwKg (kg units) done 7 Nov on all 281 files: 223 saved, 58 already kg, all exit 0, save errors 0; list in `sw\0804-swkg-r1.txt`. Dev's re-probe: properties unchanged. Dry-run fix (R9): deleted the always-suppressed `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` from pack A's `08X04-LMW.SLDASM` (SwCmd, 15:14). Dev: A's dry run passes; B/C keep the plate (they reuse A's 08X04-LMW from the vault, their copies never reach P:\). **Next:** the import's resave step in SOLIDWORKS, when Grant gives the go |
| 0804 restructure (brief `restructure-0804.md`, 7 Nov) | done on a scratch copy of P:\APEX + _Shared (`C:\PDM-work\restructure-0804\vault`, since recycled): each top = `08x04-STA-01` + its screen; B and C `_WITH_SCREEN` dissolved (SwCmd form-sub / dissolve, Dev's). Components identical (A 2301, B 1922, C 2348: file, configuration, suppression, position), mass the same, SwCheck nothing new, SwSuppDump identical. B drops two hanging mates (Coincident70, Coincident42; `--accept-gone`, Admin's call), Settled without Grant (SW7 had pointed at the changed copy, my mistake): in the vault original, Coincident42 is in the top's Mates and Coincident70 in `LS-08B-04_WITH_SCREEN`'s Mates; each has one face missing (no reference), so they hold nothing. Coincident275 (grey after) = the screen's Coincident72 before, to a suppressed plate (SFA-2T/LMW-2/GNRIC-LPP-4), grey before and after. Checks kept in `scratch\import\_kept\restructure-0804-baseline` and `restructure-0804-after`. Dev's rehearsal (8 Nov, logs kept in `scratch\import\_kept\rehearsal-restructure-logs`) checked by SW: components A 164+2137, B 165+1757, C 164+2184 unchanged; only B's two accepted mates lost, no mate new or changed; masses identical; SwSuppDump differs only by the planned folder moves (walls/roofs to Screen_Subassemblies, 08Z04-SFP-01 to _Shared); SwCheck = baseline + one ReadOnly open warning per top (read-only references). Note: my scratch copy had ~150 referenced files silently resaved by form-sub (Dev fixed in SwCmd daf174b). Real run on P:\ (Dev, logs `scratch\import\restructure-0804\real`) checked by SW: components and mates as in the rehearsal (only B's two accepted mates lost), masses identical, SwSuppDump identical to the rehearsal (and to the baseline apart from the planned folder moves), SwCheck 1 problem per top (the ReadOnly open flag only). Only the 3 tops and 3 STA-01s saved. **Done.** |

**0804: done for SW** (in the vault and restructured, after-checks clean). Nothing open; waiting on Admin for the next job.

## Drawing revisions: read-only survey for the release design (31 Oct 2026)

- `tools\sw-macros\SwRev.cs` over all 58 P:\ drawings (`scratch\import\swrev-vault.txt`), no saves: every title block shows A from `$PRP:"Revision-SheetN"` on the drawing. 55 have a revision table with the one row A / ORIGINAL DESIGN / J MCKILLOP / G ELLIOTT; the 3 0811 STA drawings have no table. `Revision` itself is `?` in all 58.
- Test on one drawing (08A06-HBA-01), scratch copies in `C:\PDM-work\scratch\rev-test`: delete the row, then set the `Revision*` properties to P1. After save and reopen: empty table, title block P1. Deleting the row alone leaves A. Not looked at on screen; no tool built; waiting on Admin.

## Now: J4 mass units g to kg (28–29 Oct 2026)

- Step 1 survey done (`tools\sw-macros\SwUnits.cs`, read-only; `scratch\import\swunits-vault-APEX.tsv`): 181 files in P:\APEX + P:\_Shared, all opened. 175 Custom, 6 MMGS; 74 already kg, 107 in g; length mm everywhere. Templates: `C:\ProgramData\SolidWorks\SOLIDWORKS 2026\templates\` MMGS_Part.prtdot / Assembly.asmdot / Drawing.drwdot ("always use default templates" on). Reported to Admin, with an open question on kg decimals.
- Step 2 `tools\sw-macros\SwKg.cs`: switches to Custom only if needed, checks 22 other unit settings are unchanged, sets kg, rebuilds, saves, reports the evaluated Weight per config; `--vault` needed for P:\; refuses Dropbox/.pdm.
- Step 3 rehearsal on Dev's copy `scratch\import\rehearsal-J2J3\vault` (after Dev's property writes): SwKg 107 saved, 74 already kg, 0 failures; all 6 MMGS switched cleanly; Weight 0.001–1750.42 kg, all resolved (`rehearsal-J2J3\swkg.txt`). Dev: no properties or references changed, all components load from the copy. My read-only recheck: units differ from the P:\ survey only in mass (101) or system + mass (6); `SwUnits --weights` gives 226 Weight values, all resolved, in kg (`rehearsal-J2J3\weights-all.txt`). Reported to Admin.
- **Existing problem (not from J4):** `P:\APEX\GNRIC\MDL-HEX-BOLT-WASHERS-NUTS-GNRIC.SLDASM` crashes SOLIDWORKS (RPC 0x800706BE) when opened read-only and stepped through its 5 configurations; the P:\ original does it too. SwKg (read-write) saved it fine in the rehearsal. It has no Weight property.
- New on P:\ since the survey (29 Oct, 09:47): 20 drawings (0806 HBA/SFA, 08X06 DBP/LMW/SFW, 08X11-LMW-01..03) plus 2 PDFs. Surveyed read-only (`scratch\import\swunits-vault-new20.tsv`): all Custom/kg already (mass dp 1, length dp 3), so SwKg would skip them. Re-survey before the real run (James still checking in).
- Open: kg decimals (waiting on Grant; SwKg keeps each file's current decimals until then).

## Earlier brief: `docs\briefs\import-0811.md` (25 Oct 2026, laptop)

SW's part: steps 1, 2 and 4. No file changes until Admin agrees the step 2 list with Grant.

**25 Oct, start:** Pack and Go targets made: `C:\PDM-work\inbox\LS-08A-11`, `-08B-11`, `-08C-11`. Referenced Documents is empty (checked in the registry). Live folder `M:\311v3\SC08\LS-08X-11` has the three tops, `LS-08X-11.SLDASM`, three STA drawings, `FRAME\` and two conflicted copies of the A top (not imported). Step 1: guiding Grant through the Pack and Go per top.

| Top | Pack and Go | Probe |
| --- | --- | --- |
| `LS-08A-11 - SCREEN + STRUCTURE` | done 25 Oct: 44 files (24 parts, 19 assemblies, 1 Hilti `.stp`), **no drawings** | `scratch\inventory\LS-08A-11-2510`: 43 opened, 83 references |
| `LS-08B-11 - SCREEN + STRUCTURE` | done: 47 files (adds `08B11-MSA-01` and the two expansion-joint brackets) | `...\LS-08B-11-2510`: 46 opened, 85 references |
| `LS-08C-11 - SCREEN + STRUCTURE` | done: 44 files | `...\LS-08C-11-2510`: 43 opened, 81 references |

Work copies `C:\PDM-work\LS-08A-11`, `-08B-11`, `-08C-11` (copies of the inbox). Every reference resolves inside its own work copy, except the old virtual part `Assem1^GROUT+ANCHOR_BOLTS` (stored in James's temp folder), as in 0806. (The older `scratch\inventory\LS-08x-11` folders are the desktop probes, kept for comparison.)

**Step 2 done (25 Oct):** the 52 live drawings were copied into the work copies with Grant's OK (A 24, B 15, C 15; `08XXX-HBW-01` sits in each), and the work copies were re-probed (67 / 61 / 58 files, all opened). Vault probed read-only to `scratch\inventory\vault-P-2510` (94 files). The list is in `sw\0811-list.md` (11 questions) and was sent to Admin. No file changes until Admin and Grant agree it. Next: step 4 (fixes, then `-r1` per top).

Decisions are now in `docs\imports\LS-08X-11-decisions.md` (D1–D17). Questions go to Admin, not Grant; Grant gets only hands-on SOLIDWORKS steps.

**D1 and D4 done (25 Oct, work copies only):** backup `C:\PDM-work\scratch\0811-pre-drw-repoint\` (54 drawings + C `SF03-UPPER`). DmEdit `replace-ref` on the 45 numbered drawings (A 15, B 15, C 15): each now stores its model's work-copy path (checked by probing two repointed drawings on their own: they keep the `C:\PDM-work` path; an original keeps James's `C:\Users\DELL\...`). Per D3, A's 9 older drawings were left as they are. D4: `08C11-SFA-3T` now draws C's `LS-08C-11_SF03-UPPER`; that file has PartNo `08C11-SFA-3T` and PartNoStatus `Provisional` on `Default` (read back). Its Description, Weight and Finish are still empty (B's `SF03-UPPER` has them): reported to Admin. Re-probe: 67 / 61 / 58 files opened, all references inside each work copy (apart from the old grout kit's virtual part). Opening `08C11-SFA-3T` in SOLIDWORKS is still unchecked (it was made from B's subframe).

Dev (25 Oct): SwCheck of `08C11-SFA-3T` passes (no "not the same document" warning; it loads C's `SF03-UPPER`). The laptop has two SOLIDWORKS installs; SwCheck now starts 2026.

**Skeleton SwCheck (25 Oct, outputs in `scratch\import\swcheck-08x11-skeleton.txt`):** all three skeletons open with no errors, but the equations are still **linked to a file** (link on). A links to the old path `D:\Dropbox\...\FRAME\ShaftA\LS_08A-11_Tie_Bar_equations.txt` (doesn't exist here); B and C link to the live `M:\311v3\SC08\LS-08X-11\FRAME\ShaftA\LS_08A-11_Tie_Bar_equations.txt` (17 Aug). Identical equations in all three: 7 globals + 8 `D*@Tie_Beam_Centerline`. No dialog blocked SwCheck, and no SOLIDWORKS was left running. The fix as in 0806 (untick Link to external file, keep the equations) waits for Admin's decision.

**Grant's answers (25 Oct, via Admin):** D7, D8, D9 (check P/W letters), D11–D17 settled; D10, D18, D20, D21 open.

**D9 and D20 checks (25 Oct, read-only, new `tools\sw-macros\SwMeasure.exe`; output `scratch\import\swmeasure-0811.txt`):**

| Part (each shaft) | Weldment | Solid bodies | James's letter |
| --- | --- | --- | --- |
| `_TIE_BEAM_1_MAIN_MEMBERS` | yes | 5 (B, C As Welded: 6) | W |
| `_TIE_BEAM_2` | yes | 1 | P |
| `_TIE_BEAM_3` | yes | 2 | W |

| B/C pair | Mass B / C (kg) | Box B / C | Same? |
| --- | --- | --- | --- |
| SF01-LOWER PART 1 / PART 2 | 96.089 / 96.089; 99.821 / 99.821 | equal | yes |
| SF02-LOWER | 216.636 / 216.636 | 1700 x 3250 x 200 both | yes |
| SF02-UPPER | 159.352 / 159.352 | 1640 x 3405 x 200 both | yes |
| SF03-LOWER | 212.904 / 212.904 | 1625 x 3250 x 200 both | yes |
| SF03-UPPER | 156.546 / 156.546 | 1565 x 3405 x 200 both | yes |
| SF01-UPPER (for contrast) | 273.671 / 272.720 | 840 / 800 deep | no |

(The tool's component count printed 0 for every assembly, which is wrong; the probe's component lists are the reliable count.) SOLIDWORKS exited after the run.

Admin (25 Oct): D22, so the new STA gets PartNo `08x11-STA-01` and PartNoStatus `Provisional` at Default, and the top has no PartNo. D15 means as 0806 B and C: the old `GROUT+ANCHOR_BOLTS` kits are deleted from the tops before `-r1` (not carried into the STA); report any mates or patterns that break. D20: don't merge yet. Backup of the three tops: `C:\PDM-work\scratch\0811-pre-D7-D16\`.

**D16 done (25 Oct):** Grant deleted the 2 loose M6x12 screws and 2 washers from the A top (Default), no errors; saved 15:11.

**D7 A done and checked (25 Oct):** `08A11-STA-01.SLDASM` = Save As copy of the A top (15:13); Grant deleted the screen and all 22 grout kits (10 roof instances, the floor seed and its LocalSketchPattern of 11; pattern sketch kept); no errors, saved 15:25. D22 via DmEdit (backup `C:\PDM-work\scratch\0811-pre-D22\`): PartNo `08A11-STA-01`, PartNoStatus `Provisional` at Default, read back; no screen or grout references left. Leftover file-level Description, Part Number and PartNo: D25 (Admin, 25 Oct) says Grant deletes all three by hand (File > Properties > Custom) in the A, B and C STAs; not blanked with DmEdit. New A top built from scratch by Grant (15:33) = `08A11-STA-01` (Default, at origin) + `LS-08X-11` (LS-08A-11), screen mated to frame; old top replaced. Drawing `08A11-STA-01.SLDDRW` repointed to the STA with DmEdit, read back. SwCheck (`scratch\import\swcheck-08A11-top-D7.txt`, `swcheck-08A11-STA-01-drw.txt`): both open with no errors, all files from the work copy, drawing loads the STA.

**Found by that check:** the 9 tie-beam parts (TIE_BEAM_1_MAIN_MEMBERS, TIE_BEAM_2, TIE_BEAM_3 in A, B, C) stored their skeleton part link as `C:\PDM-work\inbox\...`; repointed to each work copy's skeleton with DmEdit (backup `C:\PDM-work\scratch\0811-pre-skel-repoint\`), checked on an isolated copy. A's MAIN_MEMBERS also links its equations to James's `C:\Users\DELL\...\LS_08A-11_Tie_Bar_equations.txt`; Admin: D21 now covers the three MAIN_MEMBERS parts too (check B and C in SOLIDWORKS).

Admin 25 Oct: D20 yes (send Admin the plan first), D21 yes (skeletons + MAIN_MEMBERS), D23 GNRIC, D18 dropped, D25 yes, D10 open (show Grant the brace). SOLIDWORKS order: D25 on A STA, D21, D24 `08BXX-HBW-01`, D10 brace look, B D7, C D20 then D7, D25 on B and C STAs.

Admin 25 Oct: D26, each Weight formula reads its own file. Dev listed 10 that read another file (plan tool flag, d003cd6): B SF02-UPPER and SF01-UPPER read A's; C beams 1-3, C TIE_BEAM_2/3, C SF01-UPPER read B's; A TIE_BEAM_2.SLDPRT reads its assembly; splice kit reads the plate. Re-check after SOLIDWORKS saves, then DmEdit any left before -r1; send deliberate-looking ones to Admin. D10: brace configs carry -DBP (As Machined: 08X11-DBP-01, 08B11-DBP-01) and -BRP (As Welded); Grant's answer names the category kept plus (a)/(b)/(c); do early (blocks all three exports).

Brace (probe): used only in `LCMM_40x80x8_SC08`, config `LS-08X-11-LCMM40X80-02A` uses `Default<As Machined>` (3x in A and C SF01-UPPER), `-02A_SHAFT_B` uses `SHAFT_B<As Machined>` (3x in B SF01-UPPER); suppressed in all other LCMM configs. As Welded configs unused. Drawn on `LS-08A-11_SF01-UPPER` sheet 11 (Default<As Machined> DrawingRef).

**Next:** hands-on with Grant, A first: save the A top, then D7 (form `08A11-STA-01`), then B and C D7, then D14 (LCMM split and plate swap).

**Drawings don't come with Pack and Go.** Probed read-only copies of all 52 live drawings (in scratch): every drawing stores its model as `C:\Users\DELL\Dropbox\Models\Screen Models\...` (James's PC), and most are named by James's numbers (`08A11-SFA-1T` draws `LS-08A-11_SF01-UPPER`), so Pack and Go can't pair them by name. Plan: copy the drawings into each work copy in step 2 (same folders) and repoint them there. Seen so far: `08C11-SFA-3T` draws **ShaftB** `LS-08B-11_SF03-UPPER` (looks wrong); some models have two or three drawings (`08A11-SFA-1A`, `-1B` and old `LS-08A-11_SF01-LOWER.SLDDRW` all draw SF01-LOWER; `08A11-HBA-01` and old `LS-08A-11-TIE_BEAM_1.SLDDRW`); `FRAME\_Shared\08XXX-HBW-01.SLDDRW` draws the old `LS-08X-X_TIE_BEAM_POST` (vault part `08XXX-HBW-01`).

A's Find References showed 5 "Not Found" (floor plate, interface plate, brace, KBEAM plate, bolted-tie bracket in SF01-LOWER); Pack and Go found and copied all of them from `M:\`.

## Done: 0806 A, B and C (desktop, 20–24 Oct 2026)

Paths below are the desktop's (`E:\`).


### Brief: `docs\briefs\sw-prep-0806A.md` (LS-08A-06 → r4)

**State (20 Oct 2026):** Grant repoints in SOLIDWORKS (decision 10). **Step 1 done:** member, packer, bracket, sleeve and James's tie copied from the screen 11 set into the tidy copy (byte-checked); originals backed up to `E:\PDM-work\scratch\LS-08A-06-pre-r4\`. LCMM kept as 0806's (Grant: only opened for Pack and Go, no geometry changes; James works on 0811). Probe after copy: 124 references, all inside the tidy copy, none to Dropbox. Next: step 2 (`TIE-184-1XEC`).

**Step 2 notes:** old 184 tie = 184 mm, End cap1 on, End cap2/3 and clearance holes suppressed, no HDG drain holes. James's file has End cap1, End cap2, M10 clearance hole, two HDG drainage holes (no End cap3, no M12 hole) and a `Tables` folder in ConfigurationManager (checking for a design table). **Decision (Grant, 20 Oct):** tie PartNos change STOCK → GNRIC in the file now (tie file only); 184 gets `GNRIC-BRP-16`, Provisional. Admin told.

**Step 2 progress (21 Oct):** `TIE-184-1XEC` added in James's file (184 mm, End cap1 on, End cap2 and M10 hole suppressed, drain holes on, `<As Welded>` partner created; Grant checked holes in both states). SOLIDWORKS copied `TIE-385-WELDED`'s properties into it. Fixed with `tools\DmEdit setprop`: 184 tie PartNo `GNRIC-BRP-16`, PartNoStatus `Provisional`, Description `RHS 80x40x8mm x184mm TIE`; 9 tie PartNos STOCK → GNRIC on the `<As Machined>` configurations of the work copy `E:\PDM-work\LS-08A-06\_Shared\TIE_80x40x8.SLDPRT`: TIE-104/210/385/460/710/960-WELDED → `GNRIC-BRP-10..15`; TIE-460/710/960-BOLTED → `GNRIC-BRA-10..12` (1890 left as `STOCK-BRW-10`, dropped in split). Probe confirms. Grant deleted the copied properties by hand (Admin: no delete tool) and saved 21 Oct 10:23: DrawingRef on `TIE-184-1XEC<As Machined>`; PartNo, Weight, DrawingRef, Description on `TIE-184-1XEC<As Welded>`. **Probe confirms step 2 done:** 184 tie has PartNo `GNRIC-BRP-16`, PartNoStatus `Provisional`, Description, Weight (own mass); `<As Welded>` clean; 11 PartNos as listed. Next: step 3a, split the tie file.

**Step 3a done (21 Oct):** `_Shared\GNRIC-BRP.SLDPRT` = 7 welded ties (104, 184-1XEC, 210, 385, 460, 710, 960), `GNRIC-BRP-10..16`, probe-checked. Weight formulas kept the old file name after Save As; SOLIDWORKS corrects them on open (Grant checked TIE-210), saved on next save. Stray typo copy `GNRICI-BRP.SLDPRT` deleted (Grant's OK). `_Shared\GNRIC-BRA.SLDPRT` done 11:42: 3 bolted ties (460, 710, 960), `GNRIC-BRA-10..12`, probe-checked. Weight formulas still naming `TIE_80x40x8` (BRA all 3; BRP 104/460/710/960): fix with DmEdit if Grant agrees. Weight formulas fixed with DmEdit (all 10 name their own file). Grant + James: type letters stay P and W, LCMM stays `08X06-LMW`. LCMM split done 12:52: `_Shared\08X06-LMW.SLDASM`, 9 configs, Weight fixed with DmEdit, 2-size plate replaced by 4-size (Grant mapped 5 mate faces; all configs rebuild without errors), duplicate plate in 04A suppressed, unused brace and KBEAM plate deleted. Probe-checked. Next: repoint assemblies (Replace Components) to `08X06-LMW`, `GNRIC-BRP`, `GNRIC-BRA` and the 4-size plate.

### Step 1 findings

- Tidy copy was re-saved 19 Oct 18:58 (after r3 at 18:09): main assemblies, LCMM, member. References all still inside the tidy copy. Asked Grant what that save was.
- `MEMBER_SHS_40x80x8`: tidy 18:58 vs screen 11 18:53; same 48 configs and properties. Take screen 11's unless Grant edited the tidy copy.
- `LCMM_40x80x8_SC08`: screen 11 copies (19:05–19:39) differ only in screen 11 configs and a stale suppressed plate config (`140x120x10`). Suggest keeping 0806's, pending Grant confirming no 08X06 geometry change.
- `PACKER`, `BRACKET_80X40_BOLTED_TIE`, `SLEEVE_TIE_BEAM` (`08XXX-HBA-01`), James's `TIE_80x40x8`: identical in all three screen 11 sets; all 0806 configs present except `TIE-184-1XEC` (step 2).

### Repoint count (from `scratch\inventory\LS-08A-06-r3\components.csv`)

| Change | Assemblies | Instances |
| --- | --- | --- |
| `LCMM_40x80x8_SC08` → `08X06-LMW` | SF01-LOWER, SF01-UPPER, SF02_03 cantilever, SF04-LOWER, SF04-UPPER (5) | 13 |
| `TIE_80x40x8` → `GNRIC-BRP` (welded) | SF01-LOWER, SF01-UPPER, SF02-UPPER, SF03-UPPER, SF04-LOWER, SF04-UPPER (6) | 14 |
| `TIE_80x40x8` → `GNRIC-BRA` (bolted) | 08A06-STA-01 (1) | 4 |
| 2-size plate → 4-size plate | LCMM (the new `08X06-LMW` file), SF02_03 cantilever (2) | 3 |

About 14 Replace Components operations across 9 files. No drawings in the set.

### Done

- Brief and decisions read.

### Next

- Grant chooses who repoints; then step 1 (bring in newer shared files).

### Open questions

- None yet.

### Repointing (21 Oct, done with DmEdit by Grant's choice, replacing decision 10's manual Replace Components)

- Backups: `E:\PDM-work\scratch\LS-08A-06-pre-repoint\`.
- `replace-ref`: LCMM → `08X06-LMW` (SF01-L/U, SF04-L/U, SF02_03 cantilever); tie → `GNRIC-BRP` (SF01-L/U, SF02-U, SF03-U, SF04-L/U); tie → `GNRIC-BRA` (08A06-STA-01); 2-size plate → 4-size (cantilever). SF03-UPPER's stale `C:\Users\grant\Dropbox\...` tie path replaced too.
- SwCheck on `08A06-STA-01`: loads the new files with the right configurations, no old files.
- Found and fixed: Referenced Documents had the live Dropbox `_Fasteners` again (Grant removed it; fasteners now load from the tidy copy). `MDL-ANCHOR-08X06` (Grant's renamed anchor kit: not a kit, parts promoted) was at `E:\PDM-work\_Shared`; copied into the tidy copy's `_Shared` and STA-01 repointed. Typo copy `E:\PDM-work\_Shared\MDL_ANCHOR-08X06.SLDASM` deleted (Grant's OK). `GROUT+ANCHOR_BOLTS` now unused.
- Left for the importer: part links to old inbox paths (skeleton, cantilever brace) and an equations `.txt` prompt (see questions.md).
- SwCheck leaves a hidden SOLIDWORKS behind; ended twice. Reported in questions.md.
- Next: Grant opens `LS-08A-06 SCREEN+STRUCTURE`, rebuilds, checks mates, Save All. Then Pack and Go to r4.

### r4 done (21 Oct 2026)

Grant: opened `LS-08A-06 SCREEN+STRUCTURE`, rebuilt with no errors, Find References showed all 134 files loaded from inside the tidy copy (fasteners included), Save All, then Pack and Go to `E:\PDM-work\LS-08A-06-r4` (Include suppressed components). Toolbox "missing size M12x45" warning is harmless (size exists in the tidy screw file; flag cleared). Equations `.txt` prompt still appears (source unknown).

Probe: `scratch\inventory\LS-08A-06-r4` (58 files, all opened).

| Check | Result |
| --- | --- |
| References | 108, all resolve inside r4; none to Dropbox, `C:\`, the old inbox or another `PDM-work` set |
| Part links (skeleton ×3, cantilever brace) | now resolve inside r4 (Pack and Go rewrote them) |
| Added vs r3 | `08X06-LMW`, `GNRIC-BRP`, `GNRIC-BRA` |
| Dropped vs r3 | `LCMM_40x80x8_SC08`, `TIE_80x40x8`, `LS-08X-X_PLATE_TIE_BEAM_INTERFACE`, `LS-08X-11_BRACE`, `LS-08X-04_PLATE_SUBFRAME_TO_KBEAM_INTERFACE` |
| PartNos | `08X06-LMW-01..09`, `GNRIC-BRP-10..16` (16 Provisional), `GNRIC-BRA-10..12` |
| `MDL-ANCHOR-08X06` | already in r3 (it was made before r3); `GROUT+ANCHOR_BOLTS` was not in r3 either |

No SOLIDWORKS left running. Next: Dev imports r4.

### r4 fixes after Pack and Go (21 Oct, made directly in r4; the tidy copy is now one step behind and isn't used again for 0806)

- Equations `.txt`: the link was on the top-level `LS-08A-06 SCREEN+STRUCTURE` (not the skeleton). Grant unticked Link to external file and deleted the 8 broken top-level equations driving `D*@Tie_Beam_Centerline` (sketch no longer in the assembly); the 7 global variables are kept. Saved 13:38.
- Toolbox "missing size M12 x 45" prompt: came from `_Shared\TIE_BEAM_ENDPLATE_BOLTS`; opening it alone and re-saving cleared it. Saved 13:41.
- Both now open with no prompts. Re-probe: 58 files, 108 references, all resolve inside r4. No SOLIDWORKS running. Dev told to go ahead.

### Brief: `docs\briefs\sw-split-tie-beam-post.md` (21 Oct 2026)

Split `LS-08X-X_TIE_BEAM_POST` in r4: `Default` → `08XXX-HBW-01` (15 uses in TIE_BEAM_1/2/3, one suppressed); `SHAFT_B_CONCRETE_BEAM` (`08BXX-HBW-01`) unused in A, dropped. Backup in `E:\PDM-work\scratch\LS-08A-06-pre-post-split\`. Grant made `_Shared\08XXX-HBW-01.SLDPRT` (13:56): only `Default` left, PartNo `08XXX-HBW-01`, Weight already names the new file. DmEdit replace-ref in TIE_BEAM_1/2/3 (also replaced stale `C:\Users\DELL\Dropbox\...` stored paths in 2 and 3). No references to the old post left; old post removed from r4 (identical copy in the backup). Next: Grant opens SCREEN+STRUCTURE, rebuilds, checks tie beams, Save All; then re-probe.
- Grant: SCREEN+STRUCTURE rebuilt with no errors, Save All (13:58); the three tie beams re-saved from SOLIDWORKS (14:01–14:02) so their stored component names update. **Re-probe of r4** (`scratch\inventory\LS-08A-06-r4`): 58 files, 108 references, all inside r4; `08XXX-HBW-01` used 15 times (1 suppressed); old post gone; no stale old names (post, LCMM, tie, 2-size plate) anywhere. No SOLIDWORKS running. Post split done; Admin and Dev told.

### Paused (21 Oct 2026, Admin)

Waiting for Dev's import of 0806 A. Keep SOLIDWORKS free for Dev's SwCheck. Next job (when Admin says): check the imported 0806 A in the test vault `E:\PDM-test\P` with Grant: top assemblies open, nothing loaded from outside the vault, BOM shows the right PartNos.

### Notes for B, C and 0811 prep

- **Second equations `.txt` link (Dev, 22 Oct):** the tie-beam end plate `_Shared\LS-08X-X_TIE_BEAM_END_PLATE.SLDPRT` (`08XXX-PLP-20` in the vault) links to `C:\Users\DELL\Dropbox\...\311v3\SC08\LS-08X-11\FRAME\ShaftA\LS_08A-11_Tie_Bar_equations.txt` (7 globals + 4 sketch equations). So the r4 fix on the top-level assembly was only half of it. Grant is fixing the vault copy (untick Link to external file, keep the equations). Still linked: the r4 copy, the live Dropbox copies and the 0811 copies. In B, C and 0811 prep, check equations in every file (SwCheck now lists them per file), not just the top level. The top-level `LS-08A-06+STRUCTURE` fix held (7 globals, no link).

### Vault clean-up (22 Oct 2026)

Grant agreed (via Admin): fix r4 and the importer, wipe `P:\`, re-import 0806 A. Vault probe in `scratch\inventory\vault-P`. My r4 list sent to Admin: end-plate equations link; interface-plate Weight reading `LS-08A-11-TIE_BEAM_2`; suppressed floor plate in SF04-LOWER stored in `E:\PDM-work`; 153 empty properties on `DL_APEX`; Hilti junk properties; question on unused `SF01`/`SF04` configs of the right-angle tie. Importer items (Dev): old file names in Weight/Material formulas, old stored component names. Waiting for Admin's agreed list before changing any file.

### Re-import brief: `docs\briefs\reimport-0806A.md`, steps 1–3 done (22 Oct 2026)

- Backups: `E:\PDM-work\scratch\LS-08A-06-r4-pre-reimport\`.
- Step 1: end plate `LS-08X-X_TIE_BEAM_END_PLATE`: Grant unticked Link to external file, kept 7 globals + 4 equations, no errors, saved 15:23.
- Step 2: interface plate Weight on 4 configs set to its own mass with DmEdit, read back.
- Step 3: suppressed floor plate deleted from `LS-08A-06_SF04-LOWER` (Grant, no errors).
- Re-probe `scratch\inventory\LS-08A-06-r4` (old one moved to `...-r4-pre-reimport`): 58 files, 107 references, all inside r4. Correction: the 21 Oct report "all 108 inside r4" missed the floor plate link to the tidy copy; now gone. The floor plate part is still in r4 but unreferenced.
- Admin and Dev told; SOLIDWORKS free for Dev. Items 4–5 (DL_APEX, Hilti properties) left for later; item 6 dropped.

### Bolt kits set to Promote in r4 (23 Oct 2026, decision 7–8)

Grant set Promote on 7 configurations (80X40_TIE_BOLTS Default; ROOF_SLEEVE_BOLT_ASSY Default, SHAFT_B_CONCRETE_BEAM; TIE_BEAM_ENDPLATE_BOLTS Default; TIE_BEAM_TO_SUBFRAME_BOLTS 130x90, 150x90, 120X90), saved 09:15–09:19. Read back with Document Manager: all Promote (MDL-ANCHOR-08X06 already was). Backup `E:\PDM-work\scratch\LS-08A-06-r4-pre-promote\`. Re-probe `scratch\inventory\LS-08A-06-r4` (previous: `...-r4-pre-promote`): 58 files, 107 references, all inside r4, otherwise unchanged. Admin and Dev told; r4 ready for the re-import.

### Brief: `docs\briefs\import-0806BC.md` (23 Oct 2026)

Step 2 done: B and C copied from `PDM-inbox` to `E:\PDM-work\LS-08B-06` (59 files) and `E:\PDM-work\LS-08C-06` (57), probed (`scratch\inventory\LS-08B-06`, `...\LS-08C-06`). The list (new files, files already in the vault, known items, questions) is in `sw\0806BC-list.md` and was sent to Admin. No file changes until Admin and Grant agree the list. `P:\` is vault-tracked: never touch it.

### Step 3 prep (23 Oct, no SOLIDWORKS)

- Backups `E:\PDM-work\scratch\LS-08B-06-pre-fix`, `...\LS-08C-06-pre-fix`. Work copies moved to `E:\PDM-work\LS-08x-06\311v3\...` + `PANELS\` (DmEdit refuses "Dropbox" paths).
- r4 copies of every shared file already in the vault copied over B/C's; added `08X06-LMW`, `GNRIC-BRP/BRA`, `08XXX-HBW-01`, `MDL-ANCHOR-08X06` (+ Hilti), `NONPART-GROUT-08X06`, `LS-08X-06`; their links repointed into each work copy (DmEdit).
- DmEdit repoints: LCMM → `08X06-LMW`; tie → `GNRIC-BRP` (subframes) / `GNRIC-BRA` (top); post → `08XXX-HBW-01`; `LS-08ABC-06v2` → `LS-08X-06` (config to set in SOLIDWORKS).
- Probe: all resolves inside each work copy except old GROUT+ANCHOR and the unused old LCMM.
- B concrete-beam anchor: Admin says add the config in the vault via `pdm.exe` check-out (first real one), then copy the checked-in file into the B work copy.
- Next with Grant: skeletons, floor plates, screen config, grout/anchor + STA.

### 23 Oct 19:15: skeleton fix landed in the live files

Grant's skeleton fix (link unticked, 7 globals + 4 equations each; B had one equation deleted) was saved to the **live Dropbox** skeletons (`...\Screen Models\311v3\SC08\LS-08X-06\Shaft B\LS-08B-06 Skeleton.SLDPRT` 19:12, `Shaft C\LS-08C-06 Tie Beam Skeleton.SLDPRT` 19:11), not the work copies. Work copies still 19 Sep (unfixed). Grant to redo in `E:\PDM-work\LS-08B-06|C-06\311v3\...`; whether to keep the live change is Grant's call. Grout/anchor in A was replaced by hand (Grant).

### 24 Oct: grout/anchor method (Grant)

- C's 24 old GROUT+ANCHOR kits deleted in the top (Replace Components gave wrong mates). C has no grout pattern sketches.
- Grant's choice, **for all screens**: grout and anchors placed by a sketch pattern (one sketch of points per type, pattern one seed), as B's top does. For Admin to record.
- Order (Grant): form `08B06-STA-01` / `08C06-STA-01` first; Grant adds anchors in the STA, then grout in the top.
- Vault A: grout in top looks patterned (seeds -1, -15); anchors in `08A06-STA-01` are individual instances (old kit numbers).
- 24 Oct 10:54: C separated. `Shaft C\08C06-STA-01.SLDASM` = Save As copy of the top with the screen deleted (111 refs, all in the work copy). Grant rebuilt the C top from scratch: `08C06-STA-01` (first, Default) + `LS-08X-06` (LS-08BC-06). Opens without errors. Next: B the same way, then Grant adds anchors (STA) and grout (top) by sketch pattern.
- Live Dropbox skeletons: Grant leaves the fix in place.
- 24 Oct 11:18: B separated the same way. `Shaft B\08B06-STA-01.SLDASM` (no screen, no grout, all refs in work copy); B top rebuilt = `08B06-STA-01` + `LS-08X-06` (LS-08BC-06). No errors. Next: Grant adds anchors (STA) and grout (top) by sketch pattern in C, then B (B's concrete beam later, in the PDM).

### 24 Oct: 0806 B/C -r1 ready for Dev (step 3 done)

Grant's choice: anchors and grout are added **in the PDM** (laptop check-out of both STAs, both tops and `MDL-ANCHOR-08X06`), not before import. So `-r1` has no grout/anchor files.

| Pack | Files | References | Outside the pack | Probe |
| --- | --- | --- | --- | --- |
| `E:\PDM-work\LS-08B-06-r1` | 56 | 108 (5 part links) | 0 | `scratch\inventory\LS-08B-06-r1` |
| `E:\PDM-work\LS-08C-06-r1` | 54 | 104 (4 part links) | 0 | `scratch\inventory\LS-08C-06-r1` |

Done in the work copies: skeleton equations links off (B: 5th equation deleted by Grant), suppressed floor plate deleted from both SF04-LOWERs, repoints (LCMM, tie, post, screen `LS-08BC-06`), old GROUT+ANCHOR kits removed, `08B06-STA-01` / `08C06-STA-01` formed (tops = STA + `LS-08X-06`).

Still open: expansion-joint brackets' place tag (08B11 PartNos); PACKER / TIE_80x40x8 live saves (James); members' As Welded lengths; B's concrete-beam anchor config (in the PDM session).

### 24 Oct: open items resolved (Grant)

| Item | Decision |
| --- | --- |
| Expansion-joint brackets | `08BXX` (shaft B, two screens): `SHAFTB_EXP_JOINT_BRACKET_FOR_CONCRETE_BEAM` -> `08BXX-MSW-01` (PLATE ONLY = `08BXX-PLP-01`), `SHAFTB_EXP_JOINT_100x100_EA_BRACKET` -> `08BXX-MSP-01`, folder `SC08\_Shared`. Replaces the live 08B11 PartNos |
| PACKER 23 Oct save | PartNo prefix only (STOCK -> GNRIC). Keep vault `GNRIC-PKP` as is |
| TIE_80x40x8 23 Oct save | Only "b" markers on `<As Welded>` configs (to spot use; all should be As Machined). Not wanted in the vault; `GNRIC-BRP`/`BRA` unchanged |
| Members' As Welded lengths | Leave `GNRIC-LM` as is; check out and change if needed |

Checked: B and C -r1 use only `<As Machined>` configs of the ties and members. James works mainly on 08X11; all his files are closed now.

### 24 Oct: handover to the laptop

0806 B and C imported and loaded (Admin: vault holds 93 files). This desktop SW session ends here; the next SW session starts on Grant's laptop (P: = C:\PDM).

**Next for SW (laptop):** guide Grant's first real check-out session:
- check out `08B06-STA-01`, `08C06-STA-01`, `LS-08B-06 SCREEN+STRUCTURE`, `LS-08C-06 SCREEN+STRUCTURE` and `MDL-ANCHOR-08X06`
- add config `200x400_SHAFTB_CONCRETE_BEAM` (or Grant's name) to `MDL-ANCHOR-08X06` (4 Hiltis, as the old GROUT+ANCHOR config)
- in each STA: `MDL-ANCHOR-08X06` 14x ROOF_220X220 + 10x FLOOR_200X200 (+ B: 1x concrete beam), placed by sketch pattern (C has no pattern sketches; B's came across from its old top)
- in each top: `NONPART-GROUT-08X06` 14x SC08_ROOF_220X220 + 10x SC08_FLOOR_200X200 (+ B: 1x `200x300_SHAFTB_CONCRETE_BEAM`), sketch pattern
- check in with notes; SwCheck the tops; tell App
- Close All between vault work and any work-copy work (same file names)

Nothing open otherwise. Desktop-only: backups `E:\PDM-work\scratch\LS-08B-06-pre-fix`, `LS-08C-06-pre-fix`, live copies `E:\PDM-work\scratch\live-2410`.

**D24 (25 Oct, work copies only):** backup `C:\PDM-work\scratch\0811-pre-D24\` (9 beam assemblies, 3 post drawings, and the old post from A and C). The vault's `08XXX-HBW-01.SLDPRT` was copied into each work copy's `311v3\SC08\_Shared` (read-only flag cleared). DmEdit `replace-ref` post to `08XXX-HBW-01` in A and C beams 1–3 and B beam 2, and in all three `FRAME\_Shared\08XXX-HBW-01.SLDDRW` (the probe confirms each drawing now draws `08XXX-HBW-01`). The old post was moved out of A and C, as in 0806. **B beams 1 and 3 are left as they are:** they also use `SHAFT_B_CONCRETE_BEAM`, so they wait until `08BXX-HBW-01` exists (made in SOLIDWORKS). As in 0806, the beams' stored component names still say the old post until SOLIDWORKS re-saves them (Save All during D7), so check with SwCheck afterwards.

Admin 25 Oct (b8920e6): **D10 decided: split the brace** as the LCMM split. `08X11-DBP-01` (Default, A and C) and `08B11-DBP-01` (SHAFT_B, B), each file with its own As Machined/As Welded pair; As Welded configs get no PartNo (James's -BRP-01 go). Repoint `08X11-LMW` / `08B11-LMW` to their brace (after D14). **D25 on hold:** Grant thinks the STA drawing title block reads the STA's properties; likely fix: delete file-level Part Number and PartNo, correct Description (e.g. "LS-08A-11 Structure") rather than delete. Waiting on what the title block fields link to.

**D25 A done (25 Oct):** title block links are `$PRPSHEET:"PartNo"`, `$PRPSHEET:"SW-File Name(File Name)"`, `$PRPSHEET:"Description"` (Grant). Grant deleted the three file-level properties (saved 16:22); DmEdit set Description `LS-08A-11 Structure` at Default (backup `scratch\0811-pre-D22\08A11-STA-01.pre-D25-desc.SLDASM`). Read back: Default has PartNo, PartNoStatus, Description; no file-level properties. Grant's screenshot: title block shows DWG NO and PART NO `08A11-STA-01`, description `LS-08A-11 Structure`. Next: D21 (3 skeletons + 3 MAIN_MEMBERS).

**D21 done (25 Oct, unchecked in SOLIDWORKS):** Grant unticked the equations link, equations kept, in the 3 skeletons and 3 MAIN_MEMBERS (16:35-16:38). SwCheck next time SOLIDWORKS is closed.

**D24 B done (25 Oct):** Grant made `B _Shared\08BXX-HBW-01.SLDPRT` (Save As copy of the old post; only SHAFT_B_CONCRETE_BEAM<As Machined> PartNo 08BXX-HBW-01 + SHAFT_B<As Welded>); DmEdit fixed its Weight to name itself (D26; backup `scratch\0811-D24-B\`). B beams 1 and 3: Grant Replace Components (Default -> 08XXX-HBW-01, SHAFT_B -> 08BXX-HBW-01), no mate errors; beam 3's suppressed post repointed with DmEdit. B old post moved to `scratch\0811-pre-D24\B\`. Grant re-saved all 9 tie beams (17:00-17:03). Fresh probes (`scratch\inventory\LS-08x-11-2510b`): no old post anywhere except A's old `LS-08A-11-TIE_BEAM_1.SLDDRW` (D3); only other outside ref is the grout kit's virtual part.

**D25 correction:** A STA also kept Definition, Revision, Source, Nomenclature at file level (missed earlier); asked Admin whether Grant deletes them too. D20 plan sent to Admin (after D14/D10/D26 in B; copy B's five into C's work copy, repoint C, move C's five + 6 drawings out); Q1 importer handling of the copies, Q2 who sets 08Z11 PartNos.

Admin 25 Oct (f9dfe25): D25, all 7 file-level properties go (also Definition, Revision, Source, Nomenclature): A now, B and C after D7; everything at Default. **D20: no SW work.** C's five and their 6 drawings stay put; Dev's plan handles it at import (reuse of B's 08Z11 files, Importer writes the 08Z11 PartNos). C needs its own D7/D15/D25 and the SF01-UPPER Weight fix (D26). D14 and D10 in B before B's -r1.

**B D7/D15/D22/D25 done and checked (25 Oct):** `08B11-STA-01` = B top minus screen and all 21 grout kits (17:22); no file-level properties; DmEdit at Default: PartNo `08B11-STA-01`, PartNoStatus Provisional, Description `LS-08B-11 Structure` (backup `scratch\0811-pre-D22\`). New B top (17:24) = STA + `LS-08X-11` (LS-08BC-11). `08B11-STA-01.SLDDRW` repointed to the STA. SwCheck (`scratch\import\swcheck-08B11-top-D7.txt`, `-STA-01-drw.txt`): no open errors, all from the work copy. A STA: all 7 file-level properties gone (probe).

**D21 check:** skeletons clean (15 equations, no link). MAIN_MEMBERS A/B/C clean in ASSY<As Machined> but link still on in PART<As Welded> (link seems per configuration): Grant to untick again with As Welded active. Work-copy `_Shared\LS-08X-X_TIE_BEAM_END_PLATE.SLDPRT` (vault `08XXX-PLP-20`) also links to the DELL .txt; vault copy has no link (checked on a scratch copy). Asked Admin.

**D27 (end plate) checked:** work-copy `_Shared\LS-08X-X_TIE_BEAM_END_PLATE.SLDPRT` is byte-identical in A, B, C; vs a scratch copy of vault `08XXX-PLP-20`: Default only in both, all 11 equations identical, mass 1.509 kg, box 200 x 100 x 10 mm, 1 body. Same part: reuse, no untick.

**D21 MAIN_MEMBERS:** link is per configuration (new read-only tool `tools\sw-macros\SwEqLink.cs`): off with As Machined active, on with As Welded active (DELL .txt), still after Grant's 17:41-42 re-save. Grant to look again with As Welded really active; fallback (Grant's OK needed): API set LinkToFile off in As Welded and save.

**D21 done and checked (25 Oct):** Grant unticked the link with As Welded active in the three MAIN_MEMBERS. SwEqLink: link off in every configuration of all 3 MAIN_MEMBERS and all 3 skeletons, equations kept (MAIN_MEMBERS A 10, B/C 8; skeletons 15).

**C D7/D15/D22/D25 done and checked (25 Oct):** `08C11-STA-01` = C top minus screen and 22 grout kits (18:00); no file-level properties; DmEdit at Default: PartNo `08C11-STA-01`, Provisional, Description `LS-08C-11 Structure`. New C top (18:02) = STA + `LS-08X-11` (LS-08BC-11). Drawing repointed. SwCheck: no open errors, all from the work copy; only flag the end plate link (D27, reuse).

**D26:** fresh probes `scratch\inventory\LS-08x-11-2510c`. Fixed with DmEdit (backups `C:\PDM-work\scratch\0811-pre-D26\`, read back): B SF01-UPPER, SF02-UPPER Weight, B TIE_BEAM_2.SLDPRT Material; C beams 1-3 asm Weight, C SF01-UPPER Weight, C TIE_BEAM_2/3.SLDPRT Weight + Material. Left: C SF02-LOWER/UPPER (left out, D20). Held for Admin: A TIE_BEAM_2.SLDPRT reads its assembly; 2-size interface plate (4 configs) reads A's TIE_BEAM_2 assembly; splice kit reads the plate.

**Next:** D14 (LCMM split, plate swap) and D10 (brace split), then -r1 Pack and Go per top.

**D28 (4-size plate) found and checked (read-only):** work-copy `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` (byte-identical A/B/C; = vault `08XXX-PLP`) vs vault: only difference is the "Ø10.0 Diameter Hole for HDG drainage" in `PL_200x140-Perpendicular slots` (the config all 0811 tie beams use): suppressed in the vault, active in 0811 (2.082 vs 2.076 kg). Tool `tools\sw-macros\SwFeatures.cs`. Admin: proposed reuse vault, James decides. A TIE_BEAM_2.SLDPRT Weight fixed (D26, Admin OK). Splice kit Weight left (Admin; list for James).

**D14/D10 plan agreed (Admin):** Grant in B's work copy: brace -> `08X11-DBP-01.SLDPRT` (Default pair) and `08B11-DBP-01.SLDPRT` (SHAFT_B pair), As Welded PartNos deleted; LCMM -> `08X11-LMW.SLDASM` (01A/01B/02A) and `08B11-LMW-01.SLDASM` (02A_SHAFT_B); in each: 2-size plate -> work-copy 4-size `LS-08X-X_TIE_BEAM_INTERFACE_PLATE`, brace -> its DBP file, KBEAM plate deleted; B SF01-UPPER 02A_SHAFT_B instances -> 08B11-LMW-01. Then SW: formulas, replace-ref, copy to A and C, probe, move old files out.

**D10/D14 done in the work copies (25 Oct), re-save pending:**
- Grant (B work copy): `SC08\_Shared\08X11-DBP-01.SLDPRT` (Default pair) and `08B11-DBP-01.SLDPRT` (SHAFT_B pair), As Welded PartNos deleted (probe). `_Shared\08X11-LMW.SLDASM` (01A/01B/02A = LMW-01..03) and `_Shared\08B11-LMW-01.SLDASM` (02A_SHAFT_B): 2-size plate -> 4-size `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` PL_200x140 (first try picked PL_180x140-Perpendicular slots in 02A and the B brace; Grant fixed both), brace -> DBP file, KBEAM plate deleted. SwCheck both: no errors (`scratch\import\swcheck-08X11-LMW.txt`, `-08B11-LMW-01.txt`). B SF01-UPPER: Grant Replace Components (4x -01A -> 08X11-LMW, 3x -02A_SHAFT_B -> 08B11-LMW-01).
- Me (DmEdit, backups `C:\PDM-work\scratch\0811-pre-D14\` and `0811-pre-D26\`): Weight formulas of both LMWs name their own file; 08X11-LMW + 08X11-DBP-01 copied into A and C, children repointed; LCMM -> 08X11-LMW in the other 17 subframes (A 6, B 5, C 6).
- Probe `scratch\inventory\LS-08x-11-2510d`: no references to old LCMM, brace or 2-size plate except A's D3 drawings (`LS-08A-11_SF01-LOWER/UPPER.SLDDRW`, not imported). Old files moved to `scratch\0811-pre-D14\moved-A/B/C\`.
- To do: SOLIDWORKS re-save of the 17 repointed subframes (stored component names). Note for -r1: Pack and Go "Include drawings" may pick up A's D3 drawings named like the models; exclude them.

Admin 25 Oct: delete the dead suppressed 4-size plate instance (config 140x120x10) in both LMWs (Grant, same visit as the re-saves; then re-copy 08X11-LMW to A and C). A's 9 D3 drawings moved out of A's work copy to `C:\PDM-work\scratch\0811-D3-drawings-A\` so -r1 can't pick them up.

**26 Oct, pre-r1:** Grant deleted the dead 140x120x10 plate instance in both LMWs (SwCheck: active plates PL_200x140); 08X11-LMW re-copied to A and C. 17 subframes re-saved by new `tools\sw-macros\SwResave.cs` (no errors, `scratch\import\swresave-0811-D14.txt`). D28 verified on a scratch copy of the vault 08XXX-PLP (drain hole unsuppressed, 2.076 kg). End plate .txt link unticked in all three work copies by Grant (it stalled the checks; vault copy reused anyway). Probes `scratch\inventory\LS-08x-11-2510e`: 0 stale old names. SwCheck of each top (`scratch\import\swcheck-08x11-top-pre-r1.txt`): no errors, all from the work copy, full component lists for Dev. Read-only SwCheck of vault `LS-08A-06+STRUCTURE` for Dev (`swcheck-vault-LS-08A-06+STRUCTURE.txt`), P:\APEX unchanged (time and size of 90 files). Caveat sent: DM shows stale referenced configs for Replace Components instances. Next: -r1 Pack and Go per top into `C:\PDM-work\inbox\LS-08x-11-r1`, then I copy and repoint the numbered drawings.

**D30 done and checked (26 Oct):** vault `P:\APEX\GNRIC\GNRIC-BRP.SLDPRT` / `GNRIC-BRA.SLDPRT` copied into each work copy's `311v3\SC08\_Shared\` (byte-identical to the vault, configs/PartNos match the vault probe). DmEdit replace-ref `TIE_80x40x8` -> GNRIC-BRP in the 6 subframes, -> GNRIC-BRA in the STA, per work copy (21 files; backups `C:\PDM-work\scratch\0811-pre-D30\`); old tie file moved to `0811-pre-D30\moved-x\`. SwResave of the 21: all saved (`scratch\import\swresave-0811-D30.txt`). SwCheck of each top (`swcheck-08x11-top-pre-r1.txt`): no errors, TIE_80x40x8 loads nowhere; A 12 BRA TIE-460-BOLTED, BRP 12 x 385 + 44 x 460; B and C 12 BRA, BRP 11 x 385 + 45 x 460. The first -r1 for A, B, C (before D30) are to be redone; Pack and Go adds a `PDM-work\LS-08x-11\` level (asked Dev).

**-r1 built and checked (26 Oct):** Grant Pack and Go'd A, B, C (after D30) into `C:\PDM-work\inbox\LS-08x-11-r1` (layout `PDM-work\LS-08x-11\311v3\...`, Dev OK). Earlier pre-D30 -r1 moved to `C:\PDM-work\scratch\0811-r1-before-D30\`. Numbered drawings copied in at the same relative paths and DmEdit replace-ref to the -r1 models (A 14 + STA, B 14 + STA, C 8 + STA; D3 drawings and C's six D20 drawings left out). Probe `scratch\inventory\LS-08x-11-r1`: A 55 files (15 drawings), B 61 (15), C 49 (9); all opened, every reference resolves inside its -r1. SwCheck of the -r1 tops (`scratch\import\swcheck-08x11-top-r1.txt`, for Dev): no errors, all loaded from inside -r1, nothing missing, TIE_80x40x8 nowhere; components A 1709, B 1719, C 1727. SwCheck of 4 sample -r1 drawings (A HBW-01, A 08XXX-HBW-01, B SFA-1T, C STA): all load their models from inside -r1. Note: DM on an isolated copy of a -r1 file reports work-copy paths (also for Pack and Go's own files), so it isn't a reliable stored-path test here; SOLIDWORKS is.

**-r1 proven self-contained (26 Oct):** work copies renamed aside to `C:\PDM-work\LS-08A-11-hold`, `-08B-11-hold`, `-08C-11-hold` (Admin: keep them aside until the import and the P:\ SwCheck are done). SwCheck of the three -r1 tops (`scratch\import\swcheck-08x11-top-r1-hold.txt`) and the 4 sample drawings: no errors, everything loads from inside -r1, nothing missing; component counts unchanged (A 1709, B 1719, C 1727).

**D32 done (26 Oct):** Dev's list (`scratch\import\0811-r1-D32-delprop.csv`): file-level PartNo deleted with DmEdit delprop in 24 -r1 files (A 7, B 10, C 7), each read back gone; values checked first; backups `C:\PDM-work\scratch\0811-r1-pre-D32\`. Re-probe `scratch\inventory\LS-08x-11-r1`: all opened, references inside -r1, no file-level PartNo left on the 24. SwCheck of -r1 A `LS-08A-11_TIE_BEAM_3.SLDPRT`: no errors, PartNo 08A11-HBW-03 at Default<As Machined> only. SOLIDWORKS closed for Dev's rehearsal. Work copies still aside (`-hold`).
D32 addition (Dev): file-level PartNo also deleted in -r1 C `LS-08C-11_SF02-LOWER`, `SF02-UPPER`, `SF03-LOWER` (D20 copies; must match B's), read back gone; C re-probed (49 files, references inside -r1).

**Import hold (26 Oct):** Dev's scratch-vault rehearsal showed suppressed components inside the LMWs as NOT LOADED; cause (Dev): the Importer's DM renames leave stale component names, and a configuration switch then loses suppression; SwResave (rebuild + save) fixes it. Built `SwResave --import <plan files.csv> <vaultRoot> [--dry]` (6b5400f; exe `tools\sw-macros\bin\SwResave-import.exe` while Dev's job holds SwResave.exe): plan "import" assemblies only, refuses others, .pdm and Dropbox, children first by DM references, one SOLIDWORKS start per file with one retry, logs, non-zero on failure. Dry run on Dev's rehearsal3: A 15 assemblies in order. Dev tests it and runs it in the import; I don't run it on P:\ (my rules: no P:\ changes outside check-out).

**0811 imported (26 Oct, Dev):** A, B, C imported into P:\. SwResave-import (4e0151a) saved all 34 new assemblies (15/13/6), no failures; tops check clean (0 not loaded, nothing missing in any configuration). Work copies still aside as `C:\PDM-work\LS-08x-11-hold`: Admin: keep them aside as the pre-import record; tidy later with Grant.

## 26 Nov (evening): WDI1-02/03 checks and every-bolt check (all read-only)

| Job | Result |
| --- | --- |
| "Extra" roof-sleeve slots (WDI1-02/03) | No spare slots: 8 per sleeve, every one bolted (40 M20 on LS-08A-11); STEP the same. Grant: no change without talking to him. |
| Two sleeves turned in Revit | SOLIDWORKS and the 22 Nov STEP are right; the fault was in Revit (fixed there). |
| Every-bolt check (sestp\boltholes.py) | 0 problems on A-06, A-11, B-06, C-06, B-11, C-11 (both C-11 runs). Results in C:\PDM-work\wdi-slots. |

Open: 2 spare M10 holes in 08X11-DBW-01 (braced kit, A-11 and C-11); B-11's 24 Nov STEP predates the C16 fix (doubled kit), so re-export before any Revit use. Standing by; Admin [5b674b] is the only PDM admin.

## 27 Nov: LS-08X-07 stair foot at x 890 with no floor plate (read-only, for calc-08x-07)

Not missing and not an export fault: the foot is the LS-08X-04 / LS-08X-07 shared foot. 07's `08X07-SFP` there uses configuration `Floor Mount-500_04_07_SHARED` (back leg ends in an end cap, M12 clearance hole). It stands on the 04's shared plate `08X04-MSW-01` (`08X04-PLP-03` 10 mm plate + 2 `08X04-PLP-06` angle cleats, M12 x 80 through the leg), used in `08A04-SFA-5B`, `08B04-SFA-5B`, `08C04-SFA-5B`. The 10 mm gap is that plate's thickness. A-07 vault `GNRIC-PLP-1` at that foot is suppressed, as in James's M:\ original (`PLATE_FLOOR-1` and `GROUT+ANCHOR_BOLTS-75` both suppressed); BC never had one. Evidence: DM component lists, both plain-run STR.STEPs, and M:\311v3\SC08\08A-04+08A-07_TEMP.STEP (18 Sep: 200x300 shared grout + 6 HST3 M12x125). Files in C:\PDM-work\0807-feet.

## 27 Nov (later): combined 04/07 shared-foot check (read-only, Desk apex-admin-57 = A)

Result: the 07 shared leg is 10 mm too low for the current 04 plate on all three shafts. The 04 floor plates (the shared 08X04-MSW-01 too) still sit 10 mm up on the grout allowance (0804 D11); 0807 decision 2 lengthened every 07 leg by 10 mm, this one too. So the leg end is 10 mm inside the plate and the M12 holes are 10 mm apart vertically (0.0 across). A plan position: plate centred under the leg (0.0 across, 56 along, as designed). The M12x80 grip is fine. B/C plan position not checkable: the current BC-07 (mirror of A) has no building placement; SC08 and the TEMP assemblies place the old B/C design. C-04 has no M12 bolt kit at its MSW-01. Corrects my earlier note (the 10 mm gap equals the plate only if the plate sat on the floor). This PC's P:\ is behind the vault for all 0807 files (James, 26 Nov 04:50Z; A-07 top origin moved 215.133, -5.763, 67.023); latest copies read from the bucket into C:\PDM-work\0807-feet\vault-latest. Details: C:\PDM-work\0807-feet\combined\RESULT 27 Nov.txt.

## 27 Nov (evening): James's 27 Nov 07:01Z stair and 0811 check-in (read-only)

Read from S3 copies (C:\PDM-work\0807-feet\vault-27nov, compare27, p27). A-07 origin unchanged; shared leg (SFP Floor Mount-500_04_07_SHARED) unchanged. Changes: density fixed on more BRP, BRP-SHS and DBP configurations; dia 10 dowel holes added (-628 mm3) to several BRP NO_HOLES and all DBP configurations; new unused configurations (BRP 380/1285/2110, SFP Cantilever-500b/_2); BC assemblies unchanged. A-07 SFA-1X gains one 08X07-BRP-SHS 180 tube at (-350, -800, 1190), which by the boxes sits inside the existing front leg of SFP-2 (Floor Mount-500): looks like a doubled member, for James. 08X11-DBW-01 / -HBA-04: geometry unchanged; Material and Weight now read their own file; HBA-04 PartNo back at configuration level; both still also carry a file-level PartNo.

## 28 Nov (night): weekend run APEX-A125/A126 (work copies C:\PDM-work\weekend-2026-11-28)

| Change | Result |
| --- | --- |
| A (A116) | Not needed: rebuilt SOLIDWORKS shows all A-06 and C-06 floor plates on the floor (-4910); my 27 Nov "10 mm up" came from Document Manager's stale stored positions. Nothing changed. |
| B (A115) | Proven on copies (33 plates -5450, legs +10 via LMP-14/-19 and new GNRIC-LM SPL-250+8M+930-FP1 = GNRIC-LMP-20, C-04 M12 kit added fixed, shared foot 0 mm, bolts no new problems). Held: SFA-2B/5B ties also drop 10 (as 0806 A55); Desk A30 for Grant. Apply with sw\weekend\b04.ps1 -CheckedOut. |
| C (A95) | Checked in 2026-11-27T17:54:03Z / 17:54:08Z: slots 22x40, same centres; every-bolt 0 problems on all 6 STAs from P:\. |
| D (A97) | Stopped for Grant: post/packer/header stack 10+5+10; 15 mm plates need posts 5 or 10 shorter (packer call), shared GNRIC-LM (overlaps B), splice cuts. |
| E (A124) | Not started (needs new parts and part numbers; too long to prove overnight). |

Lesson: heights and positions for decisions only from rebuilt SOLIDWORKS or a fresh STEP. New SwGrout commands: addfixed, wzdlen. Pack and Go copies of the 06s resolve the M12 post bolts to old-named fastener files (copy artefact).
