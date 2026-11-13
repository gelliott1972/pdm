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

## Dry run on the copy, steps 1-4 (13 Nov, ~02:05-02:40Z; logs `sw\plates-0804-dry`)

| Step | Result |
| --- | --- |
| PLP configurations (SwCmd add-config) | `180x140x10_SLOTS_SHORT_AXIS` (from 150 SHORT, 180 long, slot 15, Ø10 suppressed) = Type 1, 238931.42 mm3. Provisional `180x140x10_SLOTS_LONG_AXIS` (from 150 LONG, 180, Ø10 suppressed) = Type 2, 240874.45 mm3. Names provisional (Admin's call) |
| Weldment-only parts (save-as --copy + delete-feature) | all six; only members and end caps left in both configurations. B: the Insert Part's own planes and `Body-Move/Copy2` named too |
| New assemblies (new-asm + place) | all six; 13 plates placed fixed, each 0 % missing (overlap = full volume) |
| Mass gate | old part = new assembly to the last digit, all six (mass, volume, area) |
| Properties (DmEdit batch, `scratch\import\plates-0804\sw\props-dry.tsv`) | 46 changes, read back. Assemblies: PartNo, PartNoStatus, Description, Weight (own file). Weldment-only: PartNo = new number on every configuration, PartNoStatus; As Welded Weight of HBW-04/-05 pointed at the old file, fixed. PLP-32 PartNo + Description "SF INTERFACE PLATE 180x140"; Type 2 PartNo left out (number to come) |
| SFA replace + mates | waiting for Dev's mate-repairing replace (Dev: 10 mates break in 08A04-SFA-2T) |

## SFA swap, look pass (13 Nov ~02:40Z; `sw\plates-0804-dry\swap-look-*.txt`)

swap-asm (Dev, 03e0eb3) without --save on all six copies: no mate put back in any SFA (every face "not found", "Resolved lightweight components (swResolveError)"); other components unmoved; nothing saved. Dev's own test had 9 of 10. Sent to Dev; waiting. Patterns whose direction was on the old part: A-2T and C-2T "LocalLPattern1 - STUBS", A-3T "LocalLPattern1 - VERTICALS + STUBS". Mates on plate faces: Distance2 in A-2T and C-2T (946 mm).

## Rehearsal on a fresh full copy (13 Nov, `C:\PDM-work\plates-0804\rehearsal`, script `sw\plates-0804-run.ps1`, logs `sw\plates-0804-rehearsal`)

| Gate item | Result |
| --- | --- |
| Only planned files written | 19 of 19, nothing else |
| Plates placed | 13, each 0 % missing |
| SFA mates | 45 of 45 put back, none dropped, 0 components moved. Patterns: A-2T/C-2T STUBS and A-3T VERTICALS + STUBS directions put back; B-3T and C-3T "LocalLPattern1- VERTICALS + STUBS" new warning, instances unmoved (Grant's check) |
| Masses | old part = new assembly to the last digit, all six |
| Properties | 46 changes read back; no PartNo on two files apart from old part / new assembly (until the removal) |
| SwCheck | six SFAs: all inside, nothing missing. STA-01s: only stored part links to P:\ (K-beam skeletons etc., a copy artefact, not these files) |
| 08A04-SFA-2T/-3T drawings | open with nothing wrong; drawing-info same as before the swap |

Found: `pdm load` stops on one PartNo on two files, and `pdm remove` refuses a file still used. So on P:\ the new assemblies' PartNo goes in by a second check-out and load after the removals (script stage 4).
Real run started on P:\ (stage 1) after the gate passed and Admin's "network OK, go".

## Real run on P:\ (13 Nov, 12:57-14:10 NZ; logs `sw\plates-0804-real`)

| Step | Result |
| --- | --- |
| Stage 1 (SOLIDWORKS) | PLP-32/-33, 6 weldment-only parts, 6 assemblies (13 plates, 0 % missing), 6 SFA swaps (45/45 mates, 0 moved). Exactly the 19 planned files written. Two stops, both safe, nothing saved: place refused GNRIC-LPP (read-only, only a configuration switch; Dev 8167178), swap-asm on 08B04-SFA-2T "08B04-SFP-05 not loaded" (Dev 094a030). Resumed with `-From` / by hand with the old parts read-only |
| Stage 2 | masses old = new to the last digit (all six); 40 properties read back |
| Stage 3 | SwCheck: six SFAs all inside P:\, nothing missing; STA-01s only the ReadOnly flag (as before) |
| Stage 4 | first load dry run listed 33 files: 14 were older copies on this PC of files James checked in during the day (STA-01, 08A04-SFA drawings, LMW, GNRIC-MSW-01...). Stopped, nothing uploaded; `pdm get` fetched exactly those 14; 08A04-STA-01 and the 08A04-SFA-2T/-3T drawings re-checked with the swapped SFAs: nothing wrong. Then load (19), remove the six old .SLDPRT (reason "plates moved to assembly 0xx04-xxx-0n.SLDASM"), check out the six assemblies, PartNo, load (6). Read back: every PartNo as P4 |
| Not done | 08X04-HBW-05.SLDASM Weight/Material: James has it checked out |
