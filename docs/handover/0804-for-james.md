# 0804 is in the vault: notes for James

From Grant, 9 Nov 2026: what changed in the vault over the weekend of 7–8 Nov.

## First, before you open anything

1. Open the PDM page and press **Update to 0.1.10** (or **Restart** on the update banner), then log in again.
2. Press **Reload list**, then **Download latest**.

The update matters: this weekend some files moved folders, and 0.1.10 moves your copies to match. If you sync on the old version by mistake, nothing is lost; 0.1.10 tidies up on its next Download latest.

## What's new

| What | Where |
| --- | --- |
| **0804 (LS-08X-04), all three shafts**: 168 files, all part numbers Provisional | `P:\APEX\SC08\LS-08X-04\` |
| Each top is now `LS-08x-04+STRUCTURE` = **`08x04-STA-01`** (all the steel) + the complete screen `LS-08x-04`, the same as 0806 and 0811 | top level of `LS-08X-04` |
| The screen walls and roofs (9 files) | `LS-08X-04\Screen_Subassemblies\` |
| `LS-08B-04_WITH_SCREEN` and `LS-08C-04_WITH_SCREEN` are retired: their steel is in the STA-01, their screen is in the top | removed from the vault (can be restored) |
| **No more `ShaftBC` folders.** Files shared by B and C now live in the set's `_Shared`. This includes **0811's 11 `08Z11-…` files** (moved from `LS-08X-11\ShaftBC\` to `LS-08X-11\_Shared\`) | `_Shared` |

In 0811 only those paths changed (`08B11-STA-01`, `08C11-STA-01` and the moved drawings were re-saved to point at the new place). Nothing in 0806 changed.

## What was changed in the 0804 models on the way in

- **Grout and anchor kits** were taken out of the tops (and the 14 inside each A and C K-beam). Grant adds grout and anchors into the STA-01s in the PDM.
- **Dead items deleted**: unused configurations and components suppressed in every configuration the tops use (screen SKUs kept). B's two mates that already pointed at nothing were dropped; every part is where it was.
- **Equations**: A's skeleton and six K-beam parts no longer link to `LS-08X-04_equations.txt`; every value was kept.
- **Ties** point at the vault's `GNRIC-BRP` / `GNRIC-BRA`; B's 145 and 474 end-capped ties are `08B04-BRW-01` / `-02`; the 184 tie with one end cap is `GNRIC-BRW-16`.
- **Fasteners**: an M12x200 configuration was added to `FAST-HEX-8.8-HDG`; nut and washer M16 numbers written.
- The loose M12 bolts, nuts and washers in the A and B tops were **kept** (they join this structure to another).
- Every 0804 file is set to **kg**.

## Numbering calls Grant made (all Provisional; tell Grant if any looks wrong)

- Where a file already had your number, **your number was kept**; the register only filled gaps.
- **SF04 in B and C** is numbered per configuration, like A's (`08B04-SFA-45/46/47`, `08C04-SFA-41/45/46/47`); the four sizes A doesn't have are `4A`–`4D`. **C's SF01** is `08C04-SFA-01`.
- **LED members**: the 40x80 family is `08X04-LMW`; the shared 40x40 sizes are `08X04-LMW-21`…`-29` in `08X04-LMW-40X40` (A's and C's own keep `08A04-LMW-02/-03`, `08C04-LMW-05/-09`); the generic ones are `GNRIC-LMW-01`…`-14`.
- **As Welded** configurations carry their As Machined parent's number.
- Virtual parts were saved out and named by the number they carry.
- `08X04-HBW-02/-04/-05/-07` and `08X04-BRP-01/-02` as agreed; end-capped welded ties are BRW.

## Properties

- **Description**: yours where you'd written one; otherwise in your pattern. 36 were worded from file names (e.g. "KBEAM SPLICE PLATE"): please glance over them and correct any in the PDM.
- **Finish**: subframe and other fabricated assemblies `HDG & PAINTED`; parts and weldments inside an assembly `Refer Assembly for Surface Finish.`; loose bolt-on parts `HDG`; K-beam by others blank; STA-01s none.
- **Weight**: `SW-Mass` per configuration, in kg.

## Drawings and releases

- **Revision table rule** (Grant, 7 Nov): one row per formal release, plus a top row for the issue you're looking at, replaced at each lettered issue until the next formal release locks it in. The first formal release is `1`. So `0A` drawings with a single `0A` row are right. Example over a life: `0A` → `0B` → `1` → `1A, 1` → `1B, 1` → `2, 1`.
- The vault now makes the release PDFs itself; **no PDFs in the vault next to the models** (check-in refuses them). Grant is removing your two old PDFs in `LS-08X-06\ShaftA` (`08A06-HBA-01.pdf`, `08A06-HBW-01.pdf`).
- 0804's first drawings are in (`08A04-STA-01`, `08A04-SFA-01`, `08X04-LMW-27/-28/-29`). `08B04-STA-01` and `08C04-STA-01` aren't drawn yet.

## Small jobs for you

1. Two older drawings still link to old Dropbox paths: `GNRIC-LPP-05.SLDDRW` and `08XXX-PLP-33.SLDDRW`. Repoint them when you next have them out.
2. Glance over the 36 file-name descriptions in 0804.
