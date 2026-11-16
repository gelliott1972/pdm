# Weldment profiles survey (brief sw-profiles, step 0)

16 Nov 2026, SW. Read-only. Tool: `tools\sw-macros\SwProfiles.cs` (SOLIDWORKS 2026, opens read-only, never saves).
Raw rows: `scratch\profiles\survey\raw.tsv`.

**Status: half done.** The profile library is surveyed (section 1). The parts (section 2) are not yet: the run stopped on the
first part when Draw needed SOLIDWORKS (Admin, 16 Nov). The Document Manager can't see member profile paths (they are not
among a part's external references), so the parts need SOLIDWORKS: 87 parts (those with a cut list, `scratch\profiles\survey\members.txt`).

## 1. The library: `M:\DLAPEX_Members`

Read from a copy (`scratch\profiles\survey\m-snapshot-1116\`, copied 16 Nov; nothing written to `M:\`). 8 files:

| File | Sizes (configurations) | What it is |
| --- | --- | --- |
| `DLAPEX\ea.sldlfp` | 7 | equal angle, EA40x40x4 to EA100x100x10 |
| `DLAPEX\rhs.sldlfp` | 7 | rectangular tube |
| `DLAPEX\shs.sldlfp` | 9 | square tube, SHS40x40x4 to SHS150x150x8 |
| `DLAPEX\historical_files_accidental_rename\` (4 files) | 7 to 9 each | older copies of the same three profiles, named after one size each |
| `LS-08A-04_KBEAM_WALL_PLATE.SLDLFP` | 2 (Default As Machined / As Welded) | not a profile: a library feature holding skeleton sketches (grid datum, K-beam master) |

Members store the **Dropbox path**, not `M:\`: e.g. `C:\Users\grant\Dropbox\Unilumin-Private\Projects\DL APEX\Models\Screen Models\DLAPEX_Members\DLAPEX\rhs.sldlfp`.

### Wrong descriptions and sizes in the three live files

Each size's profile sketch was measured (outside sizes and wall) and compared with its name and Description.

| File | Size (configuration) | Description | Sketch draws | Problem |
| --- | --- | --- | --- | --- |
| rhs.sldlfp | **RHS150x100x8** | RHS150x100x8 | **80 x 40 x 8** | Name and description both wrong: this is the 80x40x8 tube. There is **no size named RHS80x40x8** and no real 150x100x8 in this file |
| ea.sldlfp | EA50x50x6 | RSA50x50x6 | 50 x 50 x 6 | Description uses the old RSA name |
| shs.sldlfp | SHS100x100x12.5 | SHS100x100x5 | 100 x 100 x 12.5 | Description says wall 5 |

Every other size in `ea`, `rhs` and `shs` matches its name and description. In the current `shs.sldlfp`, SHS40x40x4's description is
"SHS40x40x4", so the "RHS40x40x4" labels in the cut lists are older than this file (the historical SHS file below still says RHS40x40x4).

### The historical files

| File | Wrong descriptions |
| --- | --- |
| `EA 100x100x8.sldlfp` | sizes named RSA…; RSA100x100x10 and RSA100x100x8 are described "RSA40x40x4" |
| `RHS 150x100x8.sldlfp` | RHS80x40x8, RHS80x40x6.3 and RHS80x40x5 described "RHS80x40x4"; RHS60x40x5 "RHS60x40x4"; RHS100x200x… "RHS100x50x5"; RHS150x75x10 "RHS150x100x8" |
| `RHS 80x40x8.SLDLFP` | as above, and its RHS150x100x8 size draws 80x40 and is described "RHS 80x40x8" |
| `SHS 100x100x5.sldlfp` | every SHS size described "RHS…"; SHS100x100x12.5, SHS120x120x… described "RHS100x100x5", SHS60x60x5 "RHS40x40x4" |

These explain James's note: **08A04-HBW-05's member asks for size RHS80x40x8 of `rhs.sldlfp`** (a size that file no longer has),
its geometry measures 80 x 40 x 8, and its cut-list label "RHS80x40x4" is the description the older file gave that size.

## 2. The parts

Not yet run (needs SOLIDWORKS). One part read so far:

| Part | Member | Profile file | Size asked | Measured | Cut-list label |
| --- | --- | --- | --- | --- | --- |
| 08A04-HBW-05 | Rhs RHS80X40X8(1) | DLAPEX\rhs.sldlfp | RHS80x40x8 (not in the file) | 80 x 40, wall 8 | RHS80x40x4 |

## What this means for tonight

- **Repointing re-reads the profile.** A member whose size name is missing from the vault copy (RHS80x40x8 above) can't be
  repointed "same size" without either renaming a size in the copy or choosing another size. The copy's configurations must
  stay as they are (brief step 2), so this needs Grant's call once the parts survey shows how many members are affected.
- **Same file names.** Opening a member part makes SOLIDWORKS load its Dropbox profile (hidden). The vault copies have the
  same file names, so SOLIDWORKS must be closed down (Close All) between Dropbox-pointing and vault-pointing work.
