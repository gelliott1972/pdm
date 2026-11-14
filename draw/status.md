# Draw: status

Brief: `docs\briefs\draw-1.md` (14 Nov 2026). Test folder: `C:\code\pdm\scratch\draw-test` (Grant: nothing in the root of C:\). Tool: `draw\src\` (Draw.exe; build with `build.ps1`).

## Done

| Step | Result | Checked? |
| --- | --- | --- |
| 0 Templates | From a copy of James's GNRIC-PLP-06.SLDDRW into `scratch\draw-test\_Templates`: `Unilumin-A3.slddrt` (sheet format), `Unilumin-A3.drwdot` (drawing template: his document settings, revision table emptied, old revision and date cleared), `Unilumin-revision.sldrevtbt`, `note-block.txt` | A new drawing from the template matches James's sheet. BOM and cut-list table templates **not yet** (plate drawing has neither; take from an assembly and a weldment drawing before step 4) |
| 1 One plate | **GNRIC-PLP-06**. Draft v3 `scratch\draw-test\PLP-06\GNRIC-PLP-06.SLDDRW`; PDFs side by side in `PLP-06\review\` (James, draft v1, draft v3). 11 s to make | Values checked against the model by the tool and against James's PDF by me. Grant reviewed v1 |

| 2 Rules, undrawn plate, bracket | Rules: `draw\.claude\skills\drawing-standards\SKILL.md`; layout learned from Grant's hand-laid-out drawings (`learn-layout`, `layout-examples.json`). **08X04-PLP-01** (undrawn plate) worked first time. **08X04-BKP-01** (angle, two slots) with new `make-section`: face view, linked end view, slot callout, section sizes, CHECK flags (L=150 vs 100, no material, weight) | Grant: bracket layout right, plate layout good (14 Nov). Desk DR1 open (100 or 150 long) |

Not a blind test: I looked at James's PLP-06 PDF while picking the plate. His 08XXX-PLP-30 uses chain dimensions instead of ordinate, so his style varies.

## Grant's feedback on PLP-06 (14 Nov) and what changed in v3

| Feedback | Done in v3 |
| --- | --- |
| Scale 1:5 for consistency (50/50 call) | Rule: 1:5 by default; 1:2 or 1:1 only if the plate's longest side would be under 60 mm on the sheet; 1:10 and smaller only if it doesn't fit. Isometric never bigger than the sheet scale |
| James's centre marks | 12.5 mm, gap 2.5, extension lines, centreline font (read from his drawing) |
| Revision table: 0A with a * for work in progress | Row "0A* / date / ORIGINAL DESIGN", REV box shows 0A*. DRAWN/CHECKED/APPROVED left empty. **Open with Admin**: release check needs to know about the * |
| Centre the layout (centre of area in the middle) | Face view + side view in a row, isometric + note block below, rows centred, whole group's centre of area put in the middle of the free sheet (inside the border, above the title block) |

## Next

- Step 3: copy-then-repair on a shaft B/C twin of a drawn shaft A subframe; or step 4 (small assembly) if Grant prefers.
- BOM / cut-list table templates from copies of an assembly and a weldment drawing.

## Open questions (with Admin)

- Magenta/DRAFT layer mechanism: Admin's proposal, not yet confirmed by Grant (used for now).
- "0A*" WIP revision: what release does with it (today's check wants the top row to equal the release code, so 0A* would be refused until the * is removed); who fills DRAWN.

## Housekeeping

- Empty folder `C:\PDM-draw-test\PLP-06` left behind (SOLIDWORKS held it, then a safety check stopped me deleting a folder at the root of C:). Grant to delete.

## Log (for sw-api-lessons)

| When | What | Lesson |
| --- | --- | --- |
| 14 Nov | `AutoDimension` with the ordinate scheme returned BadOptionValue | Place ordinates by hand: select base edge then the others, `IModelDocExtension.AddOrdinateDimension` |
| 14 Nov | `ISheet.SetScale` before views did nothing; first view insert resets the sheet scale | Set the scale with `ISheet.SetProperties2` after the views are in, then set the face view to use the sheet scale |
| 14 Nov | Template carried James's "0A" revision and old date | Clear `Revision-Sheet1` and `DWG_Date` in the template |
| 14 Nov | `AddHoleCallout2` needs a user click | Use a diameter dimension with prefix "n x Ø" and suffix " THRU", broken leader, horizontal text |
| 14 Nov | **Wrong values**: with centre marks (extension lines on) already placed, ordinates to holes measured to the hole edge (41.3 not 48) | Dimension first, centre marks last; the tool now checks every dimension value against the model and refuses to save on any mismatch |
| 14 Nov | Revision table didn't refresh the title block's REV after `AddRevision` | Set `Revision-Sheet1` too |
| 14 Nov | Opening a copied drawing without its model next to it can load the vault's model (read-only) | Always copy the model next to a copied drawing |
| 14 Nov | Ordinate to a slot centre: a centre mark selected by API won't take an ordinate | Pick the centre mark by position (`SelectByID2 "CENTERMARKSYMS"`); only one ordinate group per view can do that, so the other direction uses the slot's end arc |
| 14 Nov | Value check passed with slot dimensions missing | Expected values now come from the model's holes and slots, not from what was placed |
| 14 Nov | A free-standing end view came out upside down against the face view | Project it from the (turned) face view: true projection, and linked |
