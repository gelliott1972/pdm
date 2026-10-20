# Pending changes to SPEC.md and the numbering standard

Decided by Grant on 18 Oct 2026. To be written into `SPEC.md` (Part numbers and file names) at the Fable review. The numbering-standard change also needs James's agreement and an update to the Rev 3 workbook.

## Screen top level

Decided for screen 06 in scene 08 (`APEX\SC08\LS-08X-06\`). Many other screens will follow the same pattern, but not all: each screen's split is decided when it is brought in. Don't assume it.

The top assemblies sit in the screen folder root:

| Room | Screen (panels and SKUs only, not numbered) | Structure (numbered) | Screen + structure + grout (not numbered) |
| --- | --- | --- | --- |
| A | `LS-08X-06`, configuration `LS-08A-06` | `08A06-STA-01` | `LS-08A-06+STRUCTURE` |
| B | `LS-08X-06`, configuration `LS-08BC-06` | `08B06-STA-01` | `LS-08B-06+STRUCTURE` (name to confirm) |
| C | `LS-08X-06`, configuration `LS-08BC-06` | `08C06-STA-01` | `LS-08C-06+STRUCTURE` (name to confirm) |

- `LS-` screen names are established and do not change. They contain the screen only, no structure.
- For screen 06, B and C share one screen but have different structures.
- **Screen 06 keeps one screen file** (decided 19 Oct 2026, replacing "retire multi-screen files" for this screen): `LS-08ABC-06v2` becomes `LS-08X-06`, with the screens as configurations named after them (`LS-08A-06`, `LS-08BC-06`). X means shared across the shafts, as in `08X06`. Effect in the PDM: checking out the file locks both screens. Other screens are decided as they come in.
- Grout is placed in the screen + structure assembly, not the structure; the anchor kit is placed in the structure. Names and folders: see "Files that aren't parts" below.

## Numbering standard (Rev 3) change

- New category **ST, Structure**: `STA` only. A complete structure assembly, e.g. `08A06-STA-01`, `08B06-STA-01`, `08C06-STA-01`.
- Replaces the rule "Top-level structure assemblies: their identifier is the place tag itself" (`docs\part-numbering.md`, SolidWorks rules).
- Note: type A is defined as "ships to site"; the whole structure never ships as one item, but it is an assembly, so A is used.

## SOLIDWORKS File Locations check at install

Found 18 Oct 2026 on Grant's NZ PC: **Referenced Documents** listed the live Dropbox `_Fasteners` folder. SOLIDWORKS searches that list before the assembly's own folders, so it silently loaded the live fasteners instead of the work copies. Proposed addition to the File Locations bullet in SPEC.md: the app also checks **Referenced Documents** at install and warns about any folder outside `P:` (ideally the list holds only `P:\...` folders, or is empty).

## Files that aren't parts (decided 18 Oct 2026)

| Kind | Examples | In the BOM | Name | Decided |
| --- | --- | --- | --- | --- |
| Not ours | Grout, the SC08 building, concrete | No: Exclude from BOM | `NONPART-` prefix | Yes |
| Modelling group | Anchor kits, bolt kits (these are modelling groups, not bagged sets) | The group no (Promote); its contents yes | `MDL-` prefix | Yes |
| Skeleton belonging to a structure | `LS-08A-06 Skeleton` | No (not placed in any assembly) | Structure number + `-SKEL`, e.g. `08A06-STA-01-SKEL`, in the structure's folder | Yes |

- `NONPART-` and `MDL-` files carry no `PartNo`. The PDM checks the BOM setting matches the prefix (`NONPART-` = Exclude from BOM, `MDL-` = Promote).
- **Grout:** one file per screen, shared across that screen's shafts, e.g. `NONPART-GROUT-08X06` in the screen's `_Shared` folder, holding only the configurations that screen uses. Names must be unique because SOLIDWORKS matches by name.
- **Anchor kit:** one per screen, shared across shafts, e.g. `MDL-ANCHOR-08X06`. Reason: limits the knock-on of a change as the models grow.
- **Bolt kits:** `MDL-`, no numbers. `STOCK-FSA-01` comes out of the register; the FSA category may then be unused (James). Where bolt kits live (shared or per screen) is not yet decided.
- Not yet decided: where `NONPART-` files that aren't per screen live (e.g. the SC08 building); how placement skeletons are named; K beams (`KBA` in the standard, by others) becoming `NONPART-` needs James.

## STOCK renamed GNRIC (decided 18 Oct 2026)

Grant and James prefer **GNRIC** (generic) to STOCK for the job-wide place tag. Five characters, so the format is unchanged: `STOCK-PKP-01` becomes `GNRIC-PKP-01`, and the `STOCK` folder becomes `GNRIC`. Needs updating in the Rev 3 workbook and register, `docs\part-numbering.md`, SPEC.md (folder tree and examples) and the import plan tool.

## Follow-up decisions (18 Oct 2026, later)

- **Skeletons** are named for what they drive. `LS-08A-06 Skeleton` drives the shaft A header beams (formerly tie beams), and each shaft has its own, so proposed `08A06-HB-SKEL` (to confirm).
- **Linked equation files** (`.txt`) are part of the model and must be carried by Pack and Go, the import and the vault. For screen 06 (19 Oct 2026): Grant unlinked `LS_08A-06_Tie_Bar_equations.txt` from the shaft A header-beam skeleton, so its equations are now held in the skeleton itself and no file needs carrying. The live Shaft A, B and C copies of the `.txt` were separate (identical) files, so nothing was shared across shafts. B and C are decided when they come in.
- **Level rule for `MDL-` and `NONPART-` files:** same as parts, the lowest level that covers everywhere they're used, e.g. a bolt kit used by 08X06 and 08X11 lives in `SC08\_Shared`; the SC08 building lives in `APEX\SC08\`. Proposed (to confirm): end the name with that level's place tag, e.g. `MDL-ANCHOR-08X06`, `NONPART-BUILDING-08XXX`, so names stay unique and the PDM can check the level.
- **Hilti anchor length (19 Oct 2026):** Grant measured the job anchor at 125 mm. The standard's example `FAST-HST3-R-M12x115-A4` (`docs\part-numbering.md`) should read `FAST-HST3-R-M12x125-A4`.
- **K beams keep their standard category** (KBA in the Rev 3 standard): not our part, but they need drawings that line up with the GC's. Not `NONPART-`.

## Family configuration names (19 Oct 2026, Grant leaning; to confirm with James)

- The standard says a family file's configurations are named by number only (`STOCK-BRP` configuration `07`). Grant prefers to **keep descriptive configuration names** (e.g. `TIE-460-WELDED`) and let `PartNo` carry the number: renaming configurations means changing every assembly that uses them, and bare numbers make generic parts hard to find.
- To keep generic parts findable, the PDM app should offer a **parts lookup**: search by PartNo, description or size, showing the family file and configuration. A consistent configuration-name pattern per family (e.g. `TIE-<length>-<WELDED|BOLTED>[-<end caps>]`) helps too; tidy names when a family is next edited.

## Family file names (20 Oct 2026)

- A family may hold two types of one category when the parts are the same model with and without a welded end cap: `MEMBER_SHS_40x80x8` becomes `GNRIC-LM`, holding `GNRIC-LMP-01..19` and `GNRIC-LMW-20..24`. The file name is then place and category only, and the PDM's name check accepts a category-only family name. The rule "a family holds one place tag" is unchanged.
