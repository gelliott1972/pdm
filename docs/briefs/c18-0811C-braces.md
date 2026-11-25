# Brief: LS-08C-11 sleeve braces (APEX-A91, conflict C18)

From Admin, 25 Nov 2026. Grant, on the Desk, ticket apex-admin-23 = A: LS-08C-11 gets shaft A's two sleeve braces, mirrored. Order for SOLIDWORKS: (1) the LS-08B-11 C16 fix, (2) the 0807 exports, (3) this job.

## What A has and C lacks (from the export position lists)

Read-only, from `C:\PDM-work\sestp\runs\LS-08A-11 2026-11-24T0528Z\positions.txt` and `LS-08C-11 2026-11-24T1418Z\positions.txt`, plus a Document Manager probe (`scratch\c18\out`).

C is A mirrored along the header: **x_C = -9881.174 - x_A** (all 10 sleeve positions match this; y and z unchanged).

| Item | A (x_A) | C today (x_C) | C after |
| --- | --- | --- | --- |
| Spigot in header 1 | 08A11-HBA-01: 08A11-HBW-05 at -6886.174 | 08C11-HBA-01: 08XXX-HBW-01-6 at -2995 | 08A11-HBW-05 at -2995 |
| Spigot in header 3 | 08A11-HBA-03: 08A11-HBW-05 at -3136.174 | 08C11-HBA-03: 08XXX-HBW-01-1 at -6745 | 08A11-HBW-05 at -6745 |
| Sleeve | 08A11-STA-01: 08A11-HBA-04 (post 08A11-HBW-04 + brace 08A11-DBW-01) at -6886.174 and -3136.174 | 08C11-STA-01: plain sleeves 08XXX-HBA-01-19 at -2995 and -1 at -6745 | 08A11-HBA-04 at -2995 and -6745 |
| Bolt kit | MDL-ROOF-SLEEVE-BOLT-08XXX config BRACED_ROOF_SLEEVE (y -230) at both | Default (y -250): kit -5 at -2995, kit -1 at -6745 | BRACED_ROOF_SLEEVE, placed as on A |
| Brace roof anchors | MDL-ANCHOR-GNRIC ROOF_220X300_M12x125 x 2 | C has no anchors yet | Not now: with C's anchors later |

In A every one of these parts sits with identity rotation and its origin on the sleeve centre line (x), so if each part is symmetric about its own YZ plane, the mirror is the same part at x_C with identity rotation.

Roof plates stay 10 mm (APEX-A82, final: "Keep 10 mm"); DBW-01's 300 x 220 plate is used as it is.

## Steps (SW)

1. **Symmetry check, read-only:** 08A11-HBW-05, 08A11-HBW-04, 08A11-DBW-01 each symmetric about the part's x = 0 plane (vertex and hole fingerprints, mirrored set equal, as `scratch\0807\fp2.py`). Also confirm HBA-04's brace points the same way (front/back) as needed on C: on A the brace runs from the sleeve towards -z/-y; on C it must be the same, since the mirror is only in x. If any part is not symmetric, stop and tell Admin (an opposite-hand part needs a number from Grant).
2. **Rehearse on copies** under `C:\PDM-work\c18\` (08C11-STA-01, 08C11-HBA-01, 08C11-HBA-03, LS-08C-11+STRUCTURE and the A parts as references):
   - Replace the two spigots with 08A11-HBW-05 (Replace Components or delete + insert, keeping mates as A's HBW-05 is mated in 08A11-HBA-01/-03).
   - Replace the two plain sleeves with 08A11-HBA-04, mated as A mates HBA-04 (to the HBW-05 spigot), not fixed.
   - Bolt kits -5 and -1 to configuration BRACED_ROOF_SLEEVE, mated as A's kits -4 and -8.
   - Checks: rebuild 0 errors; positions before/after: only the changed components differ, and each new part's position equals A's mirrored (x_C = -9881.174 - x_A, same y, z, identity rotation) to 0.1 mm; no other component moved; kit count unchanged (10); C's header and sleeve counts unchanged except the swaps.
3. **Live:** check out 08C11-STA-01, 08C11-HBA-01, 08C11-HBA-03 (and their drawings only if they need a re-save), make the same change on P:\, same checks, check in. Close All.
4. **Square-edge export of LS-08C-11** at the new pin (run folder under `C:\PDM-work\sestp\runs\`).
5. Report to Admin: check-in times (new LS-08C-11 pin), symmetry results, check results, run folder. Commit scripts and status by path; Admin pushes.

## Open (Admin to apex-admin, for Grant if needed)

- **Part numbers:** C will use A's parts 08A11-HBA-04, -HBW-04, -HBW-05, -DBW-01. Under APEX-A87's precedent, parts shared across shafts are numbered 08X..; renaming now would move A's pin and the part keys wireframe and Revit use. Admin's recommendation: keep the 08A11 numbers for now; settle at C's shop drawings.
- **Concrete above C:** the brace roof plate needs concrete soffit at the same place as on A. The PDM model holds no building; whether C's soffit matches is for Grant / James.

## Renumber first (APEX-A93, Grant on the Desk, apex-admin-26 = B, 25 Nov)

"The Revit drawings won't change with a part number change as they are the same. Let's get it right." The four parts become shared 08X11 parts, in `_Shared` (folder = place), **before** the C change, so C is built with the new files. Suffixes kept so the map is one-to-one and nothing clashes with 08A11-HBW-01/-03 (the headers):

| Old | New | Parents (index refs, whole vault) |
| --- | --- | --- |
| `ShaftA\08A11-HBA-04.SLDASM` | `_Shared\08X11-HBA-04.SLDASM` | 08A11-STA-01 |
| `ShaftA\08A11-HBW-04.SLDPRT` | `_Shared\08X11-HBW-04.SLDPRT` | 08A11-HBA-04 |
| `ShaftA\08A11-HBW-05.SLDPRT` | `_Shared\08X11-HBW-05.SLDPRT` | 08A11-HBA-01, 08A11-HBA-03 |
| `ShaftA\08A11-DBW-01.SLDPRT` | `_Shared\08X11-DBW-01.SLDPRT` | 08A11-HBA-04 |

No vault drawing references the four directly (DM probe of all 44 LS-08X-11 drawings); none of them has its own drawing. New PartNos are free in `pdm-partnos`. Index today: 08A11-HBA-04 (Default), 08A11-HBW-04 (no config), 08A11-HBW-05 (Default<As Machined>); 08A11-DBW-01 has no PartNo row: check its property.

Steps:

| # | Who | Step |
| --- | --- | --- |
| R1 | SW | Before: xforms of `LS-08A-11+STRUCTURE` (Default) and mass of each of the four |
| R2 | SW | Check out the 7 files: the four, 08A11-HBA-01, 08A11-HBA-03, 08A11-STA-01 |
| R3 | SW | Open 08A11-STA-01 (all loaded). Save As (not copy) each part, then HBA-04, to the new name in `P:\APEX\SC08\LS-08X-11\_Shared\`, so the open parents follow. PartNo on every configuration that carries one = the new number (status unchanged); Weight formulas name the new file (DmEdit can't write P:\, so in SOLIDWORKS). Save the parents. Close All. DM probe: no file references an old name |
| R4 | Admin | `pdm move` (list of the 4 lines), dry run, then real: keeps each fileId and history; the old local copies are deleted |
| R5 | SW | Check in the 7 (4 at the new paths). After: xforms identical to R1 apart from names, masses identical, top and drawings 08A11-HBA-01/-03 and 08A11-STA-01 open with no errors or dangling items |
| R6 | Admin | Re-read: new LS-08A-11 pin, steel unchanged; PartNo rows; ledger rows; old-to-new list to apex-admin |

Then the C change above uses `_Shared\08X11-*` (live only after revit-fiona's soffit check, via apex-admin).
