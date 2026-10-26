# SW status

## Current brief: `docs\briefs\import-0811.md` (25 Oct 2026, laptop)

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
