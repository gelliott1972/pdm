# Plates out of six 0804 weldments (brief `docs\briefs\plates-0804.md`, 13 Nov 2026)

## Set-up

- Dry-run copy: `C:\PDM-work\plates-0804\vault` (P:\APEX + P:\_Shared, 786 files, 13 Nov 09:44, read-only flags cleared).
- Before inventory (read-only, P:\): `scratch\inventory\plates-0804-before`.
- Tools: Dev builds SwCmd `bodies`, `place`, `new-asm`, `delete-feature`, `--round` on save-as/new-asm (Dev's copy `scratch\import\plates-0804\toolcopy`). Asked Dev for `add-config` too (PLP-32).

## What the six hold today (SwCmd cut-list on the copies, Default<As Machined>)

| Part | Used in | Instances | Members | Plate bodies | Other bodies |
| --- | --- | --- | --- | --- | --- |
| `08A04-HBW-02` | `08A04-SFA-2T` | 1 | RHS 80x40 1644 + 469 | Slot1 + LPattern1 (one item) **and** Slot2 (own item): 3 bodies | 2 "O10 hole" bodies (end caps?) |
| `08A04-HBW-06` | `08A04-SFA-3T` | 1 | RHS 80x40 1900 | Slot1 + Slot2 | 2 "O10 hole" bodies (end caps?) |
| `08B04-SFP-01` | `08B04-SFA-3T` | 1 | RHS 80x40 1900 | Slot1 + Slot2 | 2 end caps |
| `08B04-SFP-02` | `08B04-SFA-2T` | **5** | SHS 40x40 210 + 131 | inserted `GNRIC-LPP` DOUBLE_BOTTOM | 1 end cap |
| `08C04-SFP-02` | `08C04-SFA-2T` | 1 | RHS 80x40 1644 + 469 (profile library path missing) | Slot1 + LPattern1 **and** Slot2: 3 bodies | 2 end caps |
| `08C04-SFP-03` | `08C04-SFA-3T` | 1 | RHS 80x40 1900 | Slot1 + Slot2 | 2 end caps |

Open points to settle with `bodies`: the third slot body in HBW-02 / C SFP-02; that the O10 bodies are end caps.

## Other things noticed

- `08A04-SFA-2T` and `-3T` have drawings (`.SLDDRW`). After Replace Components their dimensions and balloons may lose their edges: check in the dry run.
- Parents of the six SFAs: the three STA-01s. Check them too (mates at that level to faces of the six parts).
- Worked example `08X04-HBW-05.SLDASM`: Weight/Material still point at the removed `08X04-HBW-05.SLDPRT`. For Admin.

## Plate comparison (13 Nov, SwCmd bodies + `tools\sw-macros\SwPlateShape`, copies only; `sw\plates-0804-shapes.txt`)

| Plate | Where | Count | Slots | Centre hole |
| --- | --- | --- | --- | --- |
| Type 1 | 2 each in HBW-02, HBW-06, B SFP-01, C SFP-02, C SFP-03 | 10 | 15 wide x 25, at ±65 / ±45, parallel to the 140 side | none |
| Type 2 (`PLATE_2`) | 1 each in HBW-02 and C SFP-02 | 2 | 13.5 wide x 23.5, at ±65 / ±45, parallel to the 180 side | none |
| `08X04-PLP-30` (150, long axis) | `_Shared` | - | 13.5 wide, at ±50 / ±45, along the long side | Ø10 |

End caps 72x32x5 (HBW-02/-06 with a Ø10 drain hole) and 36x36x5 stay. B SFP-02's plate = GNRIC-LPP DOUBLE_BOTTOM (same volume).
P2 stop: asked Admin (Type 2's own configuration and number? Ø10 hole in the new configurations?). PLP-31 short-axis not read yet.
Slip (01:56-01:58Z): my plate-comparison run didn't stop on a refused lock and shared SOLIDWORKS with Dev's commands; read-only on my copies, nothing saved; told Dev.
