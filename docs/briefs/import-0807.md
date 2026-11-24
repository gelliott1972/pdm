# Brief: import 0807 (LS-08A-07, and LS-08BC-07 as a mirror of A)

From Admin, 24 Nov 2026. Same pattern as 0804 (`docs\briefs\import-0804.md`). Grant's decisions come through apex-admin on the APEX Desk; recorded in `C:\code\apex\docs\decisions.md` (APEX-A84, A86) and conflict C15.

## Decisions so far

| # | Decision | Source |
| --- | --- | --- |
| 1 | Shaft A (`LS-08A-07v2 + STRUCTURE`) is the reference. B/C is rebuilt as a mirror of A, reusing A's parts; the old B/C model (`LS-08BC-07-FRAME+SCREEN`, `LS-08X-07_old\structure`) is not imported | Desk A16 = A |
| 2 | No grout: stair-frame legs 10 mm longer at the bottom so every floor plate sits on the floor. Old `GROUT+ANCHOR_BOLTS` kits removed | Grant 24 Nov, as APEX-A55 |
| 3 | Floor anchors HST3-R M12x185, 14 mm holes; fitted **after import, in the vault** (`MDL-ANCHOR-GNRIC`, new configuration FLOOR_150X200_M12x185) | apex-admin-13, -15 (APEX-A84) |
| 4 | `GNRIC-PLP` FP-150X200 holes 15 -> 14 mm for every screen (SW, check-out/check-in) | apex-admin-14 (APEX-A86) |

## Where things are

| Thing | Where |
| --- | --- |
| Live models (read-only) | `M:\311v3\SC08\LS-08X-07` |
| Work copy (A done: legs, plates, grout out; checked) | `C:\PDM-work\0807-sweep` (M:\ structure kept; references repointed; 0 outside) |
| Sweep data | `scratch\0807\` (walk.json, boxes-*.json, dm\) |
| Register numbers | `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx`, Rename Register SC08 (25 rows for 0807; A parts as 08A07-..., B/C place tag 08Z07) |

## Reuse from the vault

| Work-copy file | Vault file |
| --- | --- |
| `PLATE_FLOOR` (FP-150X200, FP-200x200) | `GNRIC-PLP` (-01, -04) |
| `PLATE_LED_CABINET` (DOUBLE_BOTTOM) | `GNRIC-LPP` (check the configuration exists) |
| `DL_APEX` | `DL_APEX` |
| `STEP_FRAME` configuration Floor Mount-500_04_07_SHARED | bolts to `08X04-PLP-03` / `08X04-MSA-01` (0804, in the vault): check the interface, don't change 0804 |

## Open (Admin, via apex-admin)

- Part numbers: A's steel parts are used by B/C too, so whether they keep the register's 08A07 numbers or take 08X07 (shared, as 0804).
- SKUs `SC08-07-CUSTOM-SKUSv2` (not steel): name and folder as earlier screens' SKUs.

## Steps

| # | Who | Step |
| --- | --- | --- |
| 1 | Admin | B/C mirror built in the work copy, checked part by part against A |
| 2 | Dev, now | Importer plan from the work copy for A (B/C added after step 1): file list, register numbers, reuse rows, anything not in the register listed for Admin. Dry run. Report to Admin |
| 3 | Dev, App | Import A and B/C together; App `pdm.exe load --dry`, then load |
| 4 | SW / Grant | Anchors in the vault (decision 3) |
