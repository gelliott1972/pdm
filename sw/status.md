# SW status

## Current brief: `docs\briefs\sw-prep-0806A.md` (LS-08A-06 → r4)

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

## Done

- Brief and decisions read.

## Next

- Grant chooses who repoints; then step 1 (bring in newer shared files).

## Open questions

- None yet.

### Repointing (21 Oct, done with DmEdit by Grant's choice, replacing decision 10's manual Replace Components)

- Backups: `E:\PDM-work\scratch\LS-08A-06-pre-repoint\`.
- `replace-ref`: LCMM → `08X06-LMW` (SF01-L/U, SF04-L/U, SF02_03 cantilever); tie → `GNRIC-BRP` (SF01-L/U, SF02-U, SF03-U, SF04-L/U); tie → `GNRIC-BRA` (08A06-STA-01); 2-size plate → 4-size (cantilever). SF03-UPPER's stale `C:\Users\grant\Dropbox\...` tie path replaced too.
- SwCheck on `08A06-STA-01`: loads the new files with the right configurations, no old files.
- Found and fixed: Referenced Documents had the live Dropbox `_Fasteners` again (Grant removed it; fasteners now load from the tidy copy). `MDL-ANCHOR-08X06` (Grant's renamed anchor kit: not a kit, parts promoted) was at `E:\PDM-work\_Shared`; copied into the tidy copy's `_Shared` and STA-01 repointed. Typo copy `E:\PDM-work\_Shared\MDL_ANCHOR-08X06.SLDASM` deleted (Grant's OK). `GROUT+ANCHOR_BOLTS` now unused.
- Left for the importer: part links to old inbox paths (skeleton, cantilever brace) and an equations `.txt` prompt (see questions.md).
- SwCheck leaves a hidden SOLIDWORKS behind; ended twice. Reported in questions.md.
- Next: Grant opens `LS-08A-06 SCREEN+STRUCTURE`, rebuilds, checks mates, Save All. Then Pack and Go to r4.

## r4 done (21 Oct 2026)

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

## Current brief: `docs\briefs\sw-split-tie-beam-post.md` (21 Oct 2026)

Split `LS-08X-X_TIE_BEAM_POST` in r4: `Default` → `08XXX-HBW-01` (15 uses in TIE_BEAM_1/2/3, one suppressed); `SHAFT_B_CONCRETE_BEAM` (`08BXX-HBW-01`) unused in A, dropped. Backup in `E:\PDM-work\scratch\LS-08A-06-pre-post-split\`. Grant made `_Shared\08XXX-HBW-01.SLDPRT` (13:56): only `Default` left, PartNo `08XXX-HBW-01`, Weight already names the new file. DmEdit replace-ref in TIE_BEAM_1/2/3 (also replaced stale `C:\Users\DELL\Dropbox\...` stored paths in 2 and 3). No references to the old post left; old post removed from r4 (identical copy in the backup). Next: Grant opens SCREEN+STRUCTURE, rebuilds, checks tie beams, Save All; then re-probe.
- Grant: SCREEN+STRUCTURE rebuilt with no errors, Save All (13:58); the three tie beams re-saved from SOLIDWORKS (14:01–14:02) so their stored component names update. **Re-probe of r4** (`scratch\inventory\LS-08A-06-r4`): 58 files, 108 references, all inside r4; `08XXX-HBW-01` used 15 times (1 suppressed); old post gone; no stale old names (post, LCMM, tie, 2-size plate) anywhere. No SOLIDWORKS running. Post split done; Admin and Dev told.

## Paused (21 Oct 2026, Admin)

Waiting for Dev's import of 0806 A. Keep SOLIDWORKS free for Dev's SwCheck. Next job (when Admin says): check the imported 0806 A in the test vault `E:\PDM-test\P` with Grant: top assemblies open, nothing loaded from outside the vault, BOM shows the right PartNos.

## Notes for B, C and 0811 prep

- **Second equations `.txt` link (Dev, 22 Oct):** the tie-beam end plate `_Shared\LS-08X-X_TIE_BEAM_END_PLATE.SLDPRT` (`08XXX-PLP-20` in the vault) links to `C:\Users\DELL\Dropbox\...\311v3\SC08\LS-08X-11\FRAME\ShaftA\LS_08A-11_Tie_Bar_equations.txt` (7 globals + 4 sketch equations). So the r4 fix on the top-level assembly was only half of it. Grant is fixing the vault copy (untick Link to external file, keep the equations). Still linked: the r4 copy, the live Dropbox copies and the 0811 copies. In B, C and 0811 prep, check equations in every file (SwCheck now lists them per file), not just the top level. The top-level `LS-08A-06+STRUCTURE` fix held (7 globals, no link).

## Vault clean-up (22 Oct 2026)

Grant agreed (via Admin): fix r4 and the importer, wipe `P:\`, re-import 0806 A. Vault probe in `scratch\inventory\vault-P`. My r4 list sent to Admin: end-plate equations link; interface-plate Weight reading `LS-08A-11-TIE_BEAM_2`; suppressed floor plate in SF04-LOWER stored in `E:\PDM-work`; 153 empty properties on `DL_APEX`; Hilti junk properties; question on unused `SF01`/`SF04` configs of the right-angle tie. Importer items (Dev): old file names in Weight/Material formulas, old stored component names. Waiting for Admin's agreed list before changing any file.

## Re-import brief: `docs\briefs\reimport-0806A.md`, steps 1–3 done (22 Oct 2026)

- Backups: `E:\PDM-work\scratch\LS-08A-06-r4-pre-reimport\`.
- Step 1: end plate `LS-08X-X_TIE_BEAM_END_PLATE`: Grant unticked Link to external file, kept 7 globals + 4 equations, no errors, saved 15:23.
- Step 2: interface plate Weight on 4 configs set to its own mass with DmEdit, read back.
- Step 3: suppressed floor plate deleted from `LS-08A-06_SF04-LOWER` (Grant, no errors).
- Re-probe `scratch\inventory\LS-08A-06-r4` (old one moved to `...-r4-pre-reimport`): 58 files, 107 references, all inside r4. Correction: the 21 Oct report "all 108 inside r4" missed the floor plate link to the tidy copy; now gone. The floor plate part is still in r4 but unreferenced.
- Admin and Dev told; SOLIDWORKS free for Dev. Items 4–5 (DL_APEX, Hilti properties) left for later; item 6 dropped.

## Bolt kits set to Promote in r4 (23 Oct 2026, decision 7–8)

Grant set Promote on 7 configurations (80X40_TIE_BOLTS Default; ROOF_SLEEVE_BOLT_ASSY Default, SHAFT_B_CONCRETE_BEAM; TIE_BEAM_ENDPLATE_BOLTS Default; TIE_BEAM_TO_SUBFRAME_BOLTS 130x90, 150x90, 120X90), saved 09:15–09:19. Read back with Document Manager: all Promote (MDL-ANCHOR-08X06 already was). Backup `E:\PDM-work\scratch\LS-08A-06-r4-pre-promote\`. Re-probe `scratch\inventory\LS-08A-06-r4` (previous: `...-r4-pre-promote`): 58 files, 107 references, all inside r4, otherwise unchanged. Admin and Dev told; r4 ready for the re-import.

## Current brief: `docs\briefs\import-0806BC.md` (23 Oct 2026)

Step 2 done: B and C copied from `PDM-inbox` to `E:\PDM-work\LS-08B-06` (59 files) and `E:\PDM-work\LS-08C-06` (57), probed (`scratch\inventory\LS-08B-06`, `...\LS-08C-06`). The list (new files, files already in the vault, known items, questions) is in `sw\0806BC-list.md` and was sent to Admin. No file changes until Admin and Grant agree the list. `P:\` is vault-tracked: never touch it.
