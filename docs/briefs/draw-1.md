# Brief: Draw 1, the SOLIDWORKS 2D drawing assistant (weekend start)

From Admin, 14 Nov 2026. Agreed with Grant the same day. Owner: **Draw**. Background: `docs\research\drawing-workflows.md` (read first), `docs\research\claude-drives-solidworks.md`, `docs\research\auto-drawings.md`. AutoCAD/DWG is parked until this works.

## Aim

Load a model, and the machine makes the drawing and does the heavy, repetitive work. James (or Grant this weekend) checks and corrects it, and every correction feeds back so the next drawing is better. Grant's target: in time, about 90% of the heavy lifting. Grant is available this weekend to review drawings and dimensions, so move quickly and show him real drawings early.

## Grant's decisions (14 Nov)

| # | Decision |
| --- | --- |
| D1 | **Have its best go.** After any copy, repair every dangling dimension if it can (re-create it on the right edges). Only if it truly can't, remove it and mark the spot. Never save a drawing with a dangling dimension. Keep James's conventions, e.g. dimensions in brackets are reference (indicative) dimensions, and stay reference. |
| D2 | **Dimension everything, assemblies included.** The machine has a go at dimensions on parts and assemblies; James edits or tweaks. Add a **learning phase**: when James fixes a drawing and checks it in, the differences feed the rules for dimensioning from then on. |
| D3 | **Everything the machine adds shows in magenta** until a person has checked it, so it's clear what the machine did. Once checked, it becomes normal. (Grant's understanding, to be confirmed with him: see "Magenta" below.) |
| D4 | **Start with one part now.** Grant reviews basic parts and gives first-level feedback. |

## Magenta: proposed mechanism (confirm with Grant before App/Dev build anything)

- Everything the machine places (dimensions, notes, balloons, callouts) goes on a drawing layer **DRAFT**, coloured magenta. Things it has to flag (a dimension it couldn't repair, a guess) also get a short magenta note.
- The person checking moves each item they accept onto the normal layer, either one at a time or with an "accept all" macro once they're happy. Accepted items turn normal colour.
- Release refuses a drawing that still has anything on DRAFT or anything magenta in its PDF (like the existing white-text check in `src\Pdm\Release.cs`).
- Later (App): a "Drafts waiting" list on the PDM page. Not this weekend.

## Steps

All work on copies in **`C:\PDM-draw-test`**. Copy files out of `P:\` (read-only to you); never write to `P:\` or `C:\PDM-work`. SOLIDWORKS 2026 only, through `tools\SwCmd` with `--as draw` and its lock.

| # | Step | Done when |
| --- | --- | --- |
| 0 | **Extract James's templates from his own drawings** (copies): sheet format (`.slddrt`), a drawing template (`.drwdot`), BOM and cut-list table templates, and the note block. Keep them in `C:\PDM-draw-test\_Templates`. Don't put anything in `P:\_Shared` (James's call later). | A blank drawing from the extracted template looks like James's sheet |
| 1 | **One plate James has already drawn** (you pick it; tell Grant which). Make the drawing without looking at James's dimensions: views, scale, title block, note block, dimensions, hole callouts, all magenta on DRAFT. Export a PDF and compare it with James's PDF yourself first. | Grant has reviewed it in SOLIDWORKS and given feedback |
| 2 | Act on Grant's feedback; start the rules file (`draw\.claude\skills\drawing-standards\`) from his feedback and the survey of James's 230 drawings. Then one plate nobody has drawn (e.g. 08X04-PLP-01) and one bracket. | Grant says the drafts are a good start |
| 3 | **Copy-then-repair** on one shaft B or C twin of a drawn shaft A subframe: count the dimensions that survive, repair the rest (D1), magenta for anything added. | A report: survived / repaired / flagged, and Grant's view of the result |
| 4 | **One small assembly**: sheet, views, BOM, balloons, notes, and a first go at dimensions (D2). | Grant's feedback recorded |

After each step, update `draw\status.md` and send Admin one short message. Keep a log of how long each drawing took and what went wrong (it becomes `sw-api-lessons`).

## Working with Grant this weekend

- Grant gives drawing feedback directly in the Draw window. Keep your SOLIDWORKS steps short and exact (file path, what to look at).
- Real decisions (anything that changes the workflow, the vault, the PDM app or what James sees) still go to Admin or a Desk ticket.
- When a drawing is ready, leave it open in SOLIDWORKS for Grant, or give him the exact path to open.

## Not in this brief

Writing drawings into the vault, the "Drafts waiting" list, the release magenta check, the learning loop's automatic diff (design it from what you learn; build later), AutoCAD/DWG.
