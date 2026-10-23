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

## Step 3 agreed (SW with Grant, in the B and C work copies)

Fix both skeletons; delete the suppressed floor plate in both SF04-LOWERs; repoint LCMM, tie, post, screen (configuration `LS-08BC-06`) and grout/anchor; make `08B06-STA-01` and `08C06-STA-01`; then a clean Pack and Go `-r1` per screen into `E:\PDM-work`.

## Open

- B's concrete-beam anchor (`200x400_SHAFTB_CONCRETE_BEAM`): new configuration in `MDL-ANCHOR-08X06` (check-out), or an existing one.
- Expansion-joint brackets' place tag.
- Members' `<As Welded>` lengths: fix in the vault file or leave.
- `PACKER` and `TIE_80x40x8` live saves on 23 Oct: waiting on James.
