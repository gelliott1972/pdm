# Brief for SW: prepare LS-08A-06 for its first import

From Admin, 20 Oct 2026. Report progress in `sw\status.md`; questions in `sw\questions.md`.

## Goal

Produce a clean Pack and Go, `E:\PDM-work\LS-08A-06-r4`, that the importer (Dev) brings into the test vault. It's the current good set (`-r3`) plus the changes below. Decisions behind them: `docs\imports\LS-08A-06-decisions.md`.

## Steps (Grant does them in SOLIDWORKS; you guide and check)

Work in the tidy copy `E:\PDM-work\LS-08A-06`, which r3 was made from. Close all in SOLIDWORKS first.

1. **Bring in the newer shared files.** 0806's copies of these are older than screen 11's (same names and numbers, newer content). The screen 11 Pack and Go sets in `E:\PDM-work\LS-08x-11` hold the latest copies, and Admin has checked that every configuration 0806 uses exists in them, except `TIE-184-1XEC` (step 2).
   - `MEMBER_SHS_40x80x8`, `PACKER`, `BRACKET_80X40_BOLTED_TIE`, `SLEEVE_TIE_BEAM` (its number changes to `08XXX-HBA-01`, James's latest)
   - `TIE_80x40x8`: use James's version (from `311v3\SC08\_Shared`, the one with `STOCK-BRP-10..15` etc.), not 0806's
   - Check whether `LCMM_40x80x8_SC08` is also newer in screen 11 (it's an assembly, so compare dates, not bytes).
2. **Add `TIE-184-1XEC` to James's tie file** (184 mm tie, end cap one end; used once in `SF02-UPPER`). Number: next in James's welded series, expected `GNRIC-BRP-16`, marked provisional (`PartNoStatus = Provisional`).
3. **Split two families** (by hand: Save As copy, delete the configurations not wanted). Configuration names stay as they are.
   - `LCMM_40x80x8_SC08` → `08X06-LMW`, keeping only the `08X06-LMW` configurations (one file for shafts A, B and C).
   - `TIE_80x40x8` → `GNRIC-BRP` (welded) and `GNRIC-BRA` (bolted). `TIE-1890-...` (0804 only) is dropped.
4. **Retire the 2-size interface plate** (`LS-08X-X_PLATE_TIE_BEAM_INTERFACE`): uses in `LCMM` and `LS-08A-06_SF02_03-CANTILEVERED_SUB_ASSSY` go to `LS-08X-X_TIE_BEAM_INTERFACE_PLATE`, same configuration names.
5. **Pack and Go** to `E:\PDM-work\LS-08A-06-r4`, with **Include suppressed components** ticked.
6. **Check:** probe r4 into `scratch\inventory\LS-08A-06-r4`. No broken references, nothing pointing at Dropbox or another `PDM-work` set, the split files present. Write the result in `sw\status.md`.

## Agree before starting

- **Who repoints the assemblies** for steps 3 and 4: Grant with Replace Components in SOLIDWORKS, or the importer (Dev) by configuration name. Count the uses from the r3 inventory (`scratch\inventory\LS-08A-06-r3\components.csv`) so Grant can choose. Doing it in SOLIDWORKS is simpler for the first run, if the count is small.

## Not in this brief

- The cantilever brace fix (decision 2) happens after import, as a first check-out/check-in test.
- Screen 11.
