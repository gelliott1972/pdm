# Draw: status

Brief: `docs\briefs\draw-1.md` (14 Nov 2026). Test folder: `C:\code\pdm\scratch\draw-test`. Tool: `draw\src\` (Draw.exe; build with `build.ps1`).

## Done

| Step | Result | Checked? |
| --- | --- | --- |
| 0 Templates | From a copy of James's GNRIC-PLP-06.SLDDRW into `C:\code\pdm\scratch\draw-test\_Templates`: `Unilumin-A3.slddrt` (sheet format), `Unilumin-A3.drwdot` (drawing template: his document settings, revision table emptied, old revision and date cleared), `Unilumin-revision.sldrevtbt`, `note-block.txt` | A new drawing from the template matches James's sheet (PDF looked at). BOM and cut-list table templates **not yet**: the plate drawing has neither; to take from an assembly and a weldment drawing before step 4 |
| 1 One plate | **GNRIC-PLP-06** (floor plate 350x200, 6 holes). Draft `C:\code\pdm\scratch\draw-test\PLP-06\GNRIC-PLP-06.SLDDRW` + `.pdf`; James's copy and PDF in `PLP-06\james\`. 8 s to make | Dimension values match James's exactly (my own PDF comparison). Waiting for Grant's review |

## Draft vs James (GNRIC-PLP-06)

| Item | James | Draft |
| --- | --- | --- |
| Views | Top face, side for thickness, isometric lower left | Same |
| Scale | 1:5 | 1:2 (rule: largest scale where the face view fits 190 x 110 mm); isometric 1:5 |
| Position dims | Ordinate from top-left: 0/48/172/302/350, 0/40/160/200 | Same values, same places |
| Hole callout | 6 x Ø13.5 THRU | Same text; a diameter dimension with text, so it stays tied to the hole |
| Thickness | 10 on the side view | Same |
| Centre marks | With extended lines | Short marks (SOLIDWORKS default) |
| Note block | Filename / Config / Weight / Finish | Same, magenta |
| Magenta | n/a | Everything placed is on layer DRAFT (magenta); 0 off-layer, 0 dangling |

Not a blind test: I looked at James's PLP-06 PDF while picking the plate. His 08XXX-PLP-30 uses chain dimensions instead of ordinate, so his style varies.

## Next

- Grant's feedback on the PLP-06 draft, then step 2 (rules file, an undrawn plate e.g. 08X04-PLP-01, one bracket).
- BOM / cut-list table templates from copies of an assembly and a weldment drawing.

## Open questions

- Magenta/DRAFT layer mechanism is Admin's proposal, not yet confirmed by Grant (used for now).

## Log (for sw-api-lessons)

| When | What | Lesson |
| --- | --- | --- |
| 14 Nov | `AutoDimension` with the ordinate scheme returned BadOptionValue | Place ordinates by hand: select base edge then the others, `IModelDocExtension.AddOrdinateDimension` |
| 14 Nov | `ISheet.SetScale` before views did nothing; first view insert resets the sheet scale | Set the scale with `ISheet.SetProperties2` after the views are in, then set the face view to use the sheet scale |
| 14 Nov | Template carried James's "0A" revision and old date | Clear `Revision-Sheet1` and `DWG_Date` in the template |
| 14 Nov | `AddHoleCallout2` needs a user click | Use a diameter dimension with prefix "n x Ø" and suffix " THRU", broken leader, horizontal text |
