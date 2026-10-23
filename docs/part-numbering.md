# DL APEX – Structural Part Numbering Standard

> **Status: DRAFT (Rev 3, 15 Oct 2026).** Details may still be tweaked. If a rule here conflicts with an instruction from Grant or James, follow them and flag the conflict.
> Master copy: `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx`

## Scope

- Applies to **steel structure** only: frames, subframes, beams, braces, brackets, plates, packers, and the bolt kits we assemble.
- **Not covered:** LED panel SKUs (e.g. `500x750-SC0804-24-10G`), ghost carts, wooden shrouds, and other non-steel items. Leave their names as they are.
- Bought-in fasteners have their own format (see Fasteners).

## Format

```
PLACE-CCT-nn
```

| Field | Width | Meaning |
|---|---|---|
| PLACE | 5 | Where the part is used. See Place below. |
| CC | 2 | Category |
| T | 1 | Type: **A** Assembly, **W** Weldment, **P** Part |
| nn | 2 | Item number 01–99, unique within PLACE + CCT |

Examples: `08A11-SFA-1T`, `08Z11-HBA-01`, `08XXX-PLP-03`, `STOCK-PKP-01`

## Place

### Standard place tag: `SS E NN`

| Part | Values |
|---|---|
| SS | Scene `00`–`12` |
| E | Shaft. See the table below. |
| NN | Screen number from the LS type, or `XX` if the part is used on more than one screen in the scene |

| Shaft code | Meaning |
|---|---|
| A / B / C | That shaft only |
| **Z** | Shafts B and C (the only two-shaft combination) |
| **X** | All shafts (A, B and C) |
| 0 | Scene has no shafts (scenes 00 and 12) |

**Important:** the shaft letter is decided **per part**, by which rooms that part actually fits. It does **not** come from the LS screen type.

Example: LS-08BC-11 is one screen design, but:

- The subframes are the same in B and C, so they are `08Z11`. Shaft A is the mirror, so its subframes are `08A11`.
- The header beams differ between B and C, so they are `08B11` and `08C11`.

### Special place tags

| Tag | Meaning |
|---|---|
| `STOCK` | Job-wide. Used in more than one scene. Lives in the STOCK folder. |
| `08MGL` / `08MGR` | Structure tag for a named or handed structure. Here, the MacGuffin left hand and right hand (the steel is handed). It supports LS-08x-02 and 03. |

### Choosing the scope

Number each part at the **lowest level that covers everywhere it is used**:

1. One screen, one shaft: `08A11`
2. One screen, B and C: `08Z11`
3. One screen, all shafts: `08X11`
4. Several screens in one scene: `08XXA` / `08XXZ` / `08XXX` (keep the shaft letter if it applies, e.g. `08BXX`)
5. More than one scene: `STOCK`

## Categories

| Category | CC | A | W | P | Notes |
|---|---|---|---|---|---|
| Header Beam | HB | HBA | HBW | HBP | Beams at the top, fixed to the roof; panels hang from them. These were called "tie beams". |
| K Beam | KB | KBA | – | – | By others (GC). Reference only, marked BY OTHERS. |
| Subframe | SF | SFA | SFW | SFP | Only category that uses the T / B / X position code |
| LED Member | LM | LMA | LMW | LMP | Members with LED plates welded on |
| LED Plate | LP | – | – | LPP | LED mounting plates |
| Standard Plate | PL | – | – | PLP | Floor, splice, base and connection plates, cleats, gussets |
| Brace | BR | BRA | BRW | BRP | A = bolted (capped RHS with holes), P = welded-in |
| Diagonal Brace | DB | DBA | DBW | DBP | |
| Bracket | BK | BKA | BKW | BKP | |
| Packers / Shims | PK | – | – | PKP | Normally STOCK |
| Fastener Kit | FS | FSA | – | – | A bagged bolt set for one connection |
| Miscellaneous Steel | MS | MSA | MSW | MSP | Catch-all for one-offs. If ~3 similar items appear, give them their own category. |

> **Open:** HB may move to **HD** (Header Beam) so that HB can mean Horizontal Brace. Settle this before numbers are entered.

### Types

| Type | Meaning | Who sees it |
|---|---|---|
| A | Finished item that ships to site | Site, delivery docket, paint mark |
| W | Welded sub-assembly inside an A, e.g. the welded frame inside a header beam | Factory drawing set only |
| P | Loose part: cut RHS, plate, etc. | Cut lists and plate nests |

### Subframe assemblies (SFA only)

`nn` is a subframe digit 1–9, then a position letter:

| Position | Meaning |
|---|---|
| T | Top |
| B | Bottom |
| X | One piece (no top/bottom split) |

Example: `08Z11-SFA-1T` is subframe 1, top, shafts B and C.

L and R mean **Left / Right (hand)**, and are used only in structure tags. This matches the LED panel machining detail.

## Letter meanings by position

| Position | Letters |
|---|---|
| Shaft | A B C Z X 0 |
| SFA position | T B X |
| Hand (structure tag) | L R |

The same letter can appear in different positions. It never means two things in the **same** position.

## Fasteners (bought-in)

Fasteners are identified by their specification, not by a project number:

```
FAST-<type>-<size>-<grade>-<finish or material>
```

| Field | Rule |
|---|---|
| type | HEX, CSK, SKT, NUT, WSH (flat washer), SPW (spring washer), or the Hilti product code |
| size | `M20x35`: lowercase x, no spaces. Nuts and washers give the diameter only. |
| grade | 8.8, 10.9, A2, A4 (no G prefix). Nuts: 8, 10. Washers: leave out. |
| finish | Always given for carbon steel: HDG, ZP, BLK. For stainless, the material (A2/A4) is the last field. |

Examples:

- `FAST-HEX-M20x35-8.8-HDG`
- `FAST-NUT-M20-8-HDG`
- `FAST-WSH-M20-HDG`
- `FAST-HST3-R-M12x115-A4`. Job anchors are **HST3-R (stainless A4)**. Plain HST3 is carbon steel and is not used.

Bolt **kits** are our own assemblies and use the normal format, e.g. `STOCK-FSA-01` or `08X04-FSA-01`.

Each fastener has exactly one spelling. Otherwise SolidWorks counts the variants as different items.

## SolidWorks rules

- **The part number lives in the `PartNo` custom property**, configuration-specific, on every part, assembly and configuration. BOMs read `PartNo`, not the file name.
- **Single-configuration file:** file name = part number (`08A11-SFA-1T.SLDASM`).
- **Family file** (one file with configurations):
  - File name = place + category, e.g. `STOCK-BRP`
  - Configuration name = nn, so `PartNo` = `STOCK-BRP-07`
- **A family must not cross a place tag.** All configurations in one file share the same place. Example: `LCMM_40x80x8_SC08` holds configurations for screens 04, 06 and 11, plus one for shaft B only. It must be split into `08X04-LMW`, `08X06-LMW`, `08X11-LMW` and `08B11-LMW`.
- **Folder = place.** A file's folder matches its place tag:
  - The screen folder holds `ShaftA`, `ShaftB`, `ShaftC`, `ShaftBC` (Z) and `_Shared` (X)
  - `SC08\_Shared` = `08XXX`
  - `STOCK` = job-wide
- **Top-level structure assemblies:** one per room (e.g. 08A11, 08B11, 08C11). Their identifier is the place tag itself. LED screen top-levels like `LS-08X-11.SLDASM` are screens, not structure, so don't number them.
- **Virtual components** (names containing `^`) must be saved to an external file before they can be numbered.
- **Never rename in Explorer or Dropbox.** Use SolidWorks Rename or Pack and Go, with every file checked in. Renames happen once, at the PDM load.
- **Not part of the live tree:** `old`, `_old`, `PnG`, `PacknGo` and `Working` folders, and Dropbox "conflicted copy" files.
- **STOCK and `_Shared` files** affect every screen that uses them. One nominated owner edits them; everyone else is read-only.

## General rules

1. Mirror (handed) parts get their own number. One drawing may show both hands.
2. Revisions go in the title block only, never in the part number. Pre-release revisions are numbers (1, 2, 3); release revisions are letters (A, B, C), skipping I and O.
3. A change that affects fit or form gets a **new part number**, not a revision.
4. End caps are not numbered. They are covered by a standard detail with a cap table by section size.
5. Part-number columns in Excel must be formatted as **Text**, because scene-00 tags (e.g. `00040`) lose their leading zeros otherwise.
6. PL (Standard Plate) and LP (LED Plate) use the same letters reversed. Check for swaps.

## Current state (SC08)

- The SC08 BOM contains 236 steel file names. Proposed numbers are in the **Rename Register SC08** tab of the Rev 3 xlsx.
- The 74 configurations of the 9 configured files are in the **Configurations SC08** tab.
- Category and type were **guessed from the file names** and need James to check them.
- Nothing has been renamed or moved yet.

### Known issues before renaming

- LS-08BC-07 references 9 files in `LS-08X-07_old\structure`. Relink these first.
- There are duplicate live files: `MEMBER_SHS_40x80x8`, `TIE_80x40x8` and `LS-08A-06_LOWER_STRUCTURE_MEMBERS`.
- 13 files sit at the wrong sharing level. See `SC08_BOM_File_Reconciliation.xlsx`.
- Both MacGuffin top-level assemblies are named "(LH)". Identify the RH one.
- `LS-08A-11_SF01-LOWER` has configurations PART 1 and PART 2. It's not yet known what these are.
- The two interface-plate files both contain PL_180x140 and PL_200x140, which may be duplicate designs.
- There are 26 Dropbox conflicted copies, and the fastener folders exist in several copies. Consolidate these at the PDM load.
