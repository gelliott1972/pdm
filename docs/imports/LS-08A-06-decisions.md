# LS-08A-06 import: decisions

Decisions made with Grant while reviewing `scratch\import\LS-08A-06-r3-import-plan.xlsx` (source set `E:\PDM-work\LS-08A-06-r3`). They apply to this import; anything that changes the standard also goes in `docs\pending-spec-changes.md`.

## 1. Header interface plates and end plate (19 Oct 2026)

- `LS-08X-X_PLATE_TIE_BEAM_INTERFACE` (2 sizes) is an earlier version of `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` (4 sizes). PL_180x140 and PL_200x140 are the same plates in both. The 2-size file is retired; its uses (in `LCMM_40x80x8_SC08` and `LS-08A-06_SF02_03-CANTILEVERED_SUB_ASSSY`) are repointed to the 4-size file, configuration for configuration (same names).
- The plate connects the header to the subframes and is shared by 08X06 and 08X11, all shafts. Category **PLP** (a connection plate under the standard's PL definition), not HBP.
- The end plate is a different part and stays its own file.

| File | Becomes | PartNo |
| --- | --- | --- |
| `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` | `08XXX-PLP` (family) | PL_180x140 `-30`, PL_180x140-Perpendicular slots `-31`, PL_200x140 `-32`, PL_200x140-Perpendicular slots `-33` (James's numbers in the files) |
| `LS-08X-X_TIE_BEAM_END_PLATE` | `08XXX-PLP-20` | `08XXX-PLP-20` (James's file-level PartNo; the plan tool had missed it and shown the sample `-02`) |
| `LS-08X-X_PLATE_TIE_BEAM_INTERFACE` | not imported | its `-01` and `-02` are retired |

The file PartNos are James's and are the authority; the Rev 3 register numbers were a sample (Grant, 19 Oct 2026). Closes open item 17.

## 2. Cantilever brace and return (19 Oct 2026)

- The brace is the diagonal RHS 80x40x8 of the cantilever; it is a real piece of steel. Today it exists only as a body inserted into the return part (a three-body weldment: return RHS, end cap, brace), so the BOM can't show it separately.
- `LS-10X` in both file names is a typo for `LS-08X`; the import renames them anyway.
- Provisional numbers, for James: `LS-08A-06_SF02_03-CANTILEVERED_SUB_ASSSY` = `08X06-SFW-01` (the welded assembly: LCMM front, return, brace); `..._CANTILEVER_RETURN` = `08X06-SFP-02`; `..._CANTILEVER_BRACE` = `08X06-SFP-03` (next free).
- Model fix after import (a first check-out/check-in test): delete the inserted brace body from the return, place the brace part in the cantilever sub-assembly, so its BOM lists LCMM, `-SFP-02` and `-SFP-03` separately.

## Live project findings (19 Oct 2026, read-only scan of `311v3`)

- **Two different files named `TIE_80x40x8.SLDPRT`** (not a Dropbox conflict): `311v3\_Shared` (20 Sep) is used by 0806 and has `TIE-184-1XEC`, `TIE-474-...`, `TIE-104-EDNCAP`, `TIE-145-ENDCAP`; `311v3\SC08\_Shared` (16 Oct) is used by 0811, has James's numbers (`STOCK-BRP-10..15`, `STOCK-BRA-10..12`, `STOCK-BRW-10`) and `TIE-1890-...`. They must be merged into one file before import. Decision 3: `TIE-184-1XEC` takes the next number in James's series (expected `GNRIC-BRP-16`), allocated by James.
- Other same-name duplicates among files 0806/0811 use: `MEMBER_SHS_40x80x8` (both screens use `311v3\_Shared`; a stale Aug copy sits in `SC08\_Shared`), `LS-08A-06_LOWER_STRUCTURE_MEMBERS` (0806 uses `SC08\_Shared`; another copy in `LS-08X-06\Shaft A`). Same-name copies let SOLIDWORKS pick the wrong one when both are in memory.
- `MEMBER_SHS_40x80x8` and `PACKER` in `311v3\_Shared` were modified on 19 Oct, after the 0806 Pack and Go: the r3 copies of shared files are stale. Shared files are imported once, from the latest version.
- 26 Dropbox conflicted copies exist (Sep–Oct). Relevant to 0806/0811: `LS-08X-X_TIE_BEAM_END_PLATE`, `TIE_BEAM_ENDPLATE_BOLTS`, `TIE_BEAM_TO_SUBFRAME_BOLTS`, `GROUT+ANCHOR_BOLTS`, `LCMM_40x80x8_SC08`, `MEMBER_SHS_40x80x8` (x2), and several LS-08A-11 assemblies. Pack and Go never picks them up; each needs checking that nothing was lost, then deleting.

## 3. Ties (`TIE_80x40x8`) (19 Oct 2026)

- Use James's version (`311v3\SC08\_Shared\TIE_80x40x8`, his numbers) as the one tie file. The `311v3\_Shared` copy is retired; 0806's assemblies are repointed to James's file (same name, path change only).
- 0806 needs one configuration James's file lacks: `TIE-184-1XEC` (184 mm tie, end cap at one end; used once in SF02-UPPER). Grant adds it when needed; it takes the next number in James's welded series (expected `GNRIC-BRP-16`). The other configurations missing from James's file (474, 104-EDNCAP, 145) aren't used by 0806 and are dropped.
- Split by type: `GNRIC-BRP` (welded ties, `-10`..`-16`) and `GNRIC-BRA` (bolted ties, `-10`..`-12`). `STOCK` becomes `GNRIC`.
- **STOCK → GNRIC is done in the tie file during prep** (Grant, 20 Oct 2026), not by the importer: r4 carries `GNRIC-BRP-10..16` and `GNRIC-BRA-10..12`, with `TIE-184-1XEC` = `GNRIC-BRP-16` (Provisional). Tie file only; other STOCK numbers (e.g. `PACKER` `STOCK-PKP-01..04`) are still renamed by the importer.
- `TIE-1890-LS-08X-04-SF03_TIEBEAM` (`STOCK-BRW-10`) is 0804 only, if used at all: not imported with 0806/0811; it becomes `08X04-BRW-..` when 0804 comes in.
- **Configuration names stay descriptive** (e.g. `TIE-460-WELDED`); `PartNo` carries the number. Departs from the standard's "configuration name = nn" (see pending spec changes).

## 6. Fasteners (19 Oct 2026)

- Screw, nut and washer: grade **8.8**, finish **HDG**. Names: `FAST-HEX-8.8-HDG`, `FAST-NUT-8.8-HDG`, `FAST-WSH-8.8-HDG`, sizes as configurations.

## 5. Family splits (20 Oct 2026, part decided)

- **LCMM (`LCMM_40x80x8_SC08`):** split into separate files rather than one shared file: the risk of breaking another screen outweighs the odd duplicate. James's numbers already split it by place tag (`08X04-LMW`, `08X06-LMW`, `08X11-LMW`, `08B11-LMW`). 0806 takes the `08X06-LMW` configurations. For 0806: one file `08X06-LMW`, shared by shafts A, B and C (Grant, 20 Oct 2026).
- **Members (`MEMBER_SHS_40x80x8`):** not split (Grant, 20 Oct 2026). The only W members are the ones with a welded end cap; otherwise identical, so splitting would mean two models to keep in step and repointing assemblies. One file named `GNRIC-LM` (category only, as it holds both types); James's numbers kept (`GNRIC-LMP-01..19`, `GNRIC-LMW-20..24`); configuration names (end types, end lengths, LED module count) kept. Import the latest copy (changed 19 Oct), not r3. The PDM name check accepts a category-only family name holding several types.

## 7–8. Bolt kits (20 Oct 2026)

- Bolt kits are modelling aids, not kitted parts: `MDL-`, Promote, no PartNo (confirms the MDL rule).

## Order and 0811 (20 Oct 2026)

- Import order: 0806 A, then B and C, so James can start drawings in the PDM; then 0811.
- 0811 follows the 0806 pattern: one `LS-08X-11` screen file with configurations `LS-08A-11` and `LS-08BC-11`; B and C share the screen but have different structures.

## 9. 0806 numbers not yet set by James (20 Oct 2026)

- Import with the sample register numbers, marked provisional; James corrects them in the PDM. Proposed marking: a `PartNoStatus` custom property set to `Provisional` next to `PartNo` (same level, file or configuration), and the same status in the number ledger, so the PDM can list what's still provisional and James clears it when he confirms or renumbers.
- `80X40_TIE_BOLTS` is job-wide: `MDL-80X40-TIE-BOLTS-GNRIC` in `APEX\GNRIC`, alongside the tie file (Grant, 20 Oct 2026). The other three kits are scene 08 only: `APEX\SC08\_Shared`, `-08XXX`.

## 10. Repointing after the family splits (20 Oct 2026)

- Grant repoints in SOLIDWORKS (Replace Components), not the importer: about 14 operations across 9 files (LCMM → `08X06-LMW` in 5 subframes; ties → `GNRIC-BRP` in 6 subframes and `GNRIC-BRA` in `08A06-STA-01`; the 2-size plate → the 4-size plate in `08X06-LMW` and the cantilever). r4 therefore arrives already pointing at the split files, and the retired files (`LCMM_40x80x8_SC08`, `TIE_80x40x8`, `LS-08X-X_PLATE_TIE_BEAM_INTERFACE`) drop out of the Pack and Go. The importer sees these as plain renames.
