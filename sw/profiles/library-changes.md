# Profile library: changes in the vault copy (brief sw-profiles, step 2)

16 Nov 2026 (night), SW. Staging copy `scratch\profiles\_Profiles\` = an exact copy of `M:\DLAPEX_Members` (8 files, MD5
checked), then corrected with `tools\sw-macros\SwLibFix` under Admin's rule for the night (Grant's go): name each size by what
it actually draws, with Descriptions to match. Only configuration names and the configuration Description changed; every
size's profile sketch (all dimensions and the sketch's extent) was read before and after and is identical. `M:\` untouched.
Logs: `sw\profiles\library\*-look.txt` (look pass) and `*-save.txt`.

## The three main files (`DLAPEX\`)

| File | Size | Before | After |
| --- | --- | --- | --- |
| rhs.sldlfp | RHS150x100x8 (draws 80 x 40 x 8, corner 16) | name RHS150x100x8, Description RHS150x100x8 | **name RHS80x40x8**, Description RHS80x40x8 |
| ea.sldlfp | EA50x50x6 | Description RSA50x50x6 | Description EA50x50x6 |
| shs.sldlfp | SHS100x100x12.5 | Description SHS100x100x5 | Description SHS100x100x12.5 |

## The historical copies (`DLAPEX\historical_files_accidental_rename\`)

Descriptions set to each size's name, which matches what it draws:

| File | Descriptions changed |
| --- | --- |
| EA 100x100x8.sldlfp | RSA100x100x10, RSA100x100x8 (both were "RSA40x40x4") |
| RHS 150x100x8.sldlfp | 7: RHS60x40x5, RHS80x40x8, RHS80x40x6.3, RHS80x40x5 (were "RHS80x40x4"/"RHS60x40x4"), RHS100x200x12.5, RHS100x200x16 (were "RHS100x50x5"), RHS150x75x10 (was "RHS150x100x8") |
| RHS 80x40x8.SLDLFP | the same 7, plus its size **RHS150x100x8**, which draws 80 x 40 x 8: Description "RHS80x40x8", **name left as RHS150x100x8** because the file already has a size named RHS80x40x8 (for Grant to glance at) |
| SHS 100x100x5.sldlfp | all 9: "RHS…" -> "SHS…" (and SHS100x100x12.5, SHS120x120x…, SHS60x60x5 had another size's text) |

## Unchanged

`LS-08A-04_KBEAM_WALL_PLATE.SLDLFP` (not a profile: skeleton sketches), and every other size.

## Which copy the members use (found in the rehearsal)

The members were built from 10 mm corner RHS 80x40x8, as in the historical files; the main rhs.sldlfp's 80x40x8 (the
renamed size) has a 16 mm corner and changes the tube's volume (08A04-HBW-05: 3,005,027 -> 2,848,453 mm3), so the run
points RHS80x40x8 members at `historical_files_accidental_rename\RHS 80x40x8.SLDLFP` and keeps the main file only when it
gives the same shape. Every repoint is checked: same volume per body and the same bounding box, or the file is not saved.
