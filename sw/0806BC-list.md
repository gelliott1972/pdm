# 0806 B and C: what the Pack and Go holds (23 Oct 2026)

Work copies: `E:\PDM-work\LS-08B-06` (59 files), `E:\PDM-work\LS-08C-06` (57 files), copied from `PDM-inbox` (Grant's Pack and Go, 13:14). Probes: `scratch\inventory\LS-08B-06`, `scratch\inventory\LS-08C-06`. Compared with the ledger (`data\rename-ledger.csv`) and the r4 source of the vault. No files changed.

## New to the vault

| | B | C |
| --- | --- | --- |
| Top | `LS-08B-06 SCREEN+STRUCTURE` | `LS-08C-06 SCREEN+STRUCTURE` |
| Skeleton | `LS-08B-06 Skeleton` | `LS-08C-06 Tie Beam Skeleton` |
| Subframes | `LS-08B-06_SF01-LOWER`, `SF01-UPPER`, `SF02-UPPER`, `SF03-UPPER`, `SF04-LOWER`, `SF04-UPPER`, `SF05-LOWER` | same for C |
| Lower members | `LS-08B-06_LOWER_STRUCTURE_MEMBERS` | `LS-08C-06_LOWER_STRUCTURE_MEMBERS` |
| Tie beams | `LS-08B-06_TIE_BEAM_1/2/3` + `_MAIN_MEMBERS` | same for C |
| B only | `SHAFTB_EXP_JOINT_BRACKET_FOR_CONCRETE_BEAM` (PartNos `08B11-MSW-01`, `PLATE ONLY` = `08B11-PLP-01`; on B's top) and `SHAFTB_EXP_JOINT_100x100_EA_BRACKET` (its part-link parent). Both also in 0811 B. | |

No structure assembly (`08B06-STA-01`, `08C06-STA-01`): the structure sits directly in the top, as A's did before `08A06-STA-01` was made.

## Old files the vault no longer has (repoint, like A)

| Old file | Used by B/C | Vault file | All configs used exist there? |
| --- | --- | --- | --- |
| `LCMM_40x80x8_SC08` | SF01-L/U, SF04-L/U (+ the shared cantilever) | `08X06-LMW` | Yes (01A, 01A_SLOTS_PARALLEL_SHORT_AXIS (B only), 01B.1, 02A) |
| `TIE_80x40x8` | top (460/960 BOLTED), SF01-L/U, SF02-U, SF03-U, SF04-L/U | `GNRIC-BRP` / `GNRIC-BRA` | Yes (385, 460, 710, 960 WELDED; 460, 960 BOLTED) |
| `LS-08X-X_TIE_BEAM_POST` | TIE_BEAM_1/2/3, `Default` only | `08XXX-HBW-01` | Yes. `SHAFT_B_CONCRETE_BEAM` (08BXX-HBW-01) is **not used** by B or C |
| `LS-08ABC-06v2` (screen) | top, config `SS-08BC-06` | `LS-08X-06` | Vault config is named `LS-08BC-06`, not `SS-08BC-06` |
| `GROUT+ANCHOR_BOLTS` | B top: `FLOOR_200X200_BIM`, `ROOF_220X220_BIM`, `200x400_SHAFTB_CONCRETE_BEAM`; C top: first two | `MDL-ANCHOR-08X06` (ROOF_220X220, FLOOR_200X200) + `NONPART-GROUT-08X06` | Not directly: names differ, and B's concrete-beam anchor has no config in the vault. Holds a virtual `Assem1^` and a Hilti at `C:\Users\DELL\...` (missing) |
| `LS-08X-11_BRACE`, 2-size `LS-08X-X_PLATE_TIE_BEAM_INTERFACE`, K-beam plate | only inside the old LCMM and cantilever | gone in A | Disappear with the LCMM repoint |

## Already in the vault

Identical to the r4 source (10): `200x250-MT-SC0806-SLAVE`, `BRACKET_80X40_BOLTED_TIE`, `DL_APEX`, `LS-08X-06_NARROW_SIDE_CROSS_BRACE`, `LS-08X-06_SF05_LOWER_LED_PLATE`, `LS-10A-06_SF01A_TIE_RIGHT_ANGLE`, `LS-10X-06_SF02A_CANTILEVER_BRACE`, `PLATE_FLOOR`, `PLATE_LED_CABINET`, `SLEEVE_TIE_BEAM`.

Different from the vault's copy (22). Most only because Pack and Go re-saved them or r4 was fixed:

| File | Why it differs |
| --- | --- |
| 9 panel SKUs, fasteners (3), bolt kits (4), cantilever sub-assembly | Re-saved by Pack and Go / r4 fixes (Promote, repoint). Same configs and PartNos |
| `LS-08X-X_TIE_BEAM_END_PLATE` | Live copy still has the equations link (vault fixed) |
| `LS-08X-X_TIE_BEAM_INTERFACE_PLATE` | Live copy still has the wrong Weight (vault fixed). Live file saved 20 Oct, r4's 16 Oct; no other property or config change |
| **`MEMBER_SHS_40x80x8`** (`GNRIC-LM`) | **Live copy edited 22 Oct 16:21, after r4:** PartNos added on the `<As Welded>` configs with a `b` suffix (e.g. `STOCK-LMP-04b`, `STOCK-LMW-20b`), plus more Descriptions and Weights. Still STOCK. Not in the vault |
| **`PACKER`** (`GNRIC-PKP`) | **Live copy saved 23 Oct 08:35.** No property or config change; geometry change unknown |
| `TIE_80x40x8` (old) | Live copy saved 23 Oct 08:25 (still STOCK numbers). Vault's split files came from the 20–21 Oct copy |

## Known items

| Item | Finding |
| --- | --- |
| B's tie-beam post `08BXX-HBW-01` | Not used by B (or C): all tie beams use `Default` |
| Right-angle tie `SF01`/`SF04` | B and C use only `SF01_DEEP` and `SF04_DEEP`, like A. `SF01`/`SF04` unused here too |
| End plate equations link | Vault copy is fixed and reused; nothing to import. B/C skeletons and other files: not checked yet (needs SwCheck) |
| Weight formulas | Only the interface plate (vault fixed) |
| Bolt kit Promote | Vault copies already Promote; B/C reuse them |
| Floor plate | `LS-08A-06_SF04-LOWER_FLOOR_PLATE` suppressed in B's and C's `SF04-LOWER`: same fix as A |
| `140x120x10` | Only a suppressed plate inside the old LCMM; goes with the repoint |
| Dropbox / `PDM-work` paths | Only in `GROUT+ANCHOR_BOLTS` (Hilti, virtual) and the old LCMM (K-beam plate). Both go with the repoints |

## SwCheck results (Dev, 23 Oct; raw output in `scratch\import\LS-08B-06-work-swcheck.txt`, `LS-08C-06-work-swcheck.txt` and the `-skeleton.txt` files)

| File | Equations linked to |
| --- | --- |
| End plate (B and C copies) | Screen 11's `LS_08A-11_Tie_Bar_equations.txt` (11 equations). The vault copy is already fixed and reused |
| `LS-08B-06 Skeleton` | `M:\311v3\SC08\LS-08X-06\Shaft B\LS_08A-06_Tie_Bar_equations.txt` (12 equations). No M: drive; .txt not in the pack |
| `LS-08C-06 Tie Beam Skeleton` | `M:\311v3\SC08\LS-08X-06\Shaft C\LS_08A-06_Tie_Bar_equations.txt` (11 equations) |

- No other file (tops included) links an equations file.
- Part links point to the `PDM-inbox` copy (main members to skeleton, cantilever return to brace, B's exp-joint bracket to the EA bracket), because the pack was copied from the inbox to `PDM-work`. A Pack and Go of `-r1` straight into `E:\PDM-work` fixes this, as for A.
- `GROUT+ANCHOR_BOLTS`: Hilti suppressed everywhere; virtual `Assem1^` loads from SOLIDWORKS' temp folder (normal). Goes with the grout/anchor repoint.
- B's exp-joint bracket component shows as not loaded. Both tops open with 2 "file not found" errors (which files isn't known; perhaps the Hilti and the EA bracket link).
- Referenced Documents has no Dropbox folder.
