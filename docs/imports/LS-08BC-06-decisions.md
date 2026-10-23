# LS-08B-06 and LS-08C-06 import: decisions

Same pattern as A (`LS-08A-06-decisions.md`). SW's list: `sw\0806BC-list.md`. Brief: `docs\briefs\import-0806BC.md`.

## 1. Structure assemblies (23 Oct 2026)

Make `08B06-STA-01` and `08C06-STA-01` as in A.

## 2. Screen configuration (23 Oct 2026)

B and C point at `LS-08X-06` configuration `LS-08BC-06`. Their `SS-08BC-06` was a wrong name.

## 3. Right-angle tie SF01 and SF04 (23 Oct 2026)

Not used any more (Grant). They stay unnumbered.

## 4. Member "b" PartNos (23 Oct 2026, James via Grant)

James put them on the `<As Welded>` configurations of the live members file to mark lengths that differ from `<As Machined>`; machined is correct. Not wanted in the vault (the vault's `GNRIC-LM` has none).

## 5. Skeleton equations (23 Oct 2026)

B's and C's skeletons: untick the link to `M:Év3\...\LS_08A-06_Tie_Bar_equations.txt` and keep the equations, as in A.

## 6. Grout and anchors (23 Oct 2026)

Same method as A: repoint to `MDL-ANCHOR-08X06` and `NONPART-GROUT-08X06` (in each top, the old GROUT+ANCHOR becomes `MDL-ANCHOR-08X06` ROOF_220X220 / FLOOR_200X200 plus `NONPART-GROUT-08X06` in its matching configuration; all exist, including the grout for B's concrete beam).

## 7. Grout and anchors placed by sketch pattern (24 Oct 2026, Grant: for all screens)

Grout and anchors are placed by a sketch pattern: one sketch of points per type, one seed patterned, as B's top already does. Anchors go in the structure assembly (`STA`), grout in the top. Replaces decision 6's repointing for B and C (Replace Components gave wrong mates). **Grout and anchors for B and C are added after import, in the PDM, on the laptop** (check-out of `08B06-STA-01`, `08C06-STA-01`, both tops and `MDL-ANCHOR-08X06`, including B's concrete-beam configuration), so the `-r1` packs hold no grout/anchor. A's anchors in `08A06-STA-01` are still individual instances.

## 8. Structure assemblies formed (24 Oct 2026)

`08B06-STA-01` and `08C06-STA-01` made by Save As of each top with the screen deleted; each top rebuilt as STA + `LS-08X-06` (`LS-08BC-06`). Both open without errors.

## Live files changed by mistake (23 Oct 2026)

The B and C skeleton equation fix was saved to the live Dropbox skeletons instead of the work copies. Grant keeps the live change. The work copies were fixed too (B's 5th equation deleted: 7 globals + 4 equations, same as C).

## Step 3 done (24 Oct 2026)

`E:\PDM-work\LS-08B-06-r1` (56 files, 108 refs, 0 outside) and `LS-08C-06-r1` (54 files, 104 refs, 0 outside); probes in `scratch\inventory\LS-08B-06-r1` and `-C-06-r1`.

## Step 3 agreed (SW with Grant, in the B and C work copies)

Fix both skeletons; delete the suppressed floor plate in both SF04-LOWERs; repoint LCMM, tie, post, screen (configuration `LS-08BC-06`) and grout/anchor; make `08B06-STA-01` and `08C06-STA-01`; then a clean Pack and Go `-r1` per screen into `E:\PDM-work`.

## 9. Open items closed (24 Oct 2026, Grant)

- **Expansion-joint brackets:** place tag `08BXX` (used by 0806 B and 0811 B), folder `APEX\SC08\_Shared`. `SHAFTB_EXP_JOINT_BRACKET_FOR_CONCRETE_BEAM` becomes `08BXX-MSW-01` (configuration PLATE ONLY = `08BXX-PLP-01`, same file); `SHAFTB_EXP_JOINT_100x100_EA_BRACKET` becomes `08BXX-MSP-01`. Replaces James's `08B11` numbers.
- **Structure assemblies:** `08B06-STA-01`, `08C06-STA-01`, as A.
- **Live saves on 23 Oct:** `PACKER` only changed its PartNo prefix (STOCK to GNRIC), and `TIE_80x40x8` only had "b" markers on `<As Welded>`. Vault `GNRIC-PKP`, `GNRIC-BRP` and `GNRIC-BRA` stay unchanged; B and C use only `<As Machined>`.
- **Members' `<As Welded>` lengths:** leave `GNRIC-LM` as is; check out and change it if ever needed.
- James is mainly on 08X11 and has closed all his files.
