# Brief: PDF releases, first step (the 0A checking set)

From Admin, 7 Nov 2026. Agreed with Grant (Desk A3): **Dev** adds PDF export to SwCmd; **App** builds the release step. Decisions: `docs\releases-decisions.md` (R1 to R10, R1/R2 changed 7 Nov). Report in your own `status.md` and the Desk status file; questions for Grant as Desk tickets.

## Aim

The vault makes the release PDFs from the drawings in SOLIDWORKS, so James no longer uploads his own, and the vault is the single source of truth for what was issued. First use: the **0A** set, the first PDFs for checking (James set the title-block revision on 56 drawings to `0A` on 7 Nov).

## Revision scheme (R1, R2 as of 7 Nov)

Official releases are numbers (`1`, `2`, ...); the client sees only numbers. Before each release, lettered versions for checking: `0A`, `0B` ... then `1`, then `1A`, `1B` ... then `2`. File names: `<number>_<code>.pdf`, e.g. `08A06-HBA-01_0A.pdf`. The title block REV shows the full code; the revision table lists official numbers only.

## Dev: SwCmd `export-pdf`

- `export-pdf <drawing.SLDDRW> <out.pdf>`: all sheets, through the running SOLIDWORKS, opened read-only. Same rules as the other commands: never `M:\`/Dropbox, never `.pdm`, never overwrite, never write into `P:\` (the release step files the PDF into the vault by check-in, not SwCmd).
- A read-only `drawing-info <drawing>`: title-block REV as shown (resolving `$PRP:"Revision-SheetN"`) per sheet, revision table rows, and the referenced model and configuration. `sw\` has `SwRev` (read-only survey) to start from.
- Test on scratch copies first; one SOLIDWORKS job at a time. The real run waits until 0804 is in.

## App: the release step

In the PDM app (or the Desk, App's call; say which and why):

1. Pick a set: a screen or shaft folder (R10), and the code (`0A` here).
2. For each drawing in the set: it's checked in, nobody has it out, and the local copy is the latest.
3. Export the PDF with SwCmd to a temp folder.
4. Check (R4): title-block REV on every sheet = the code; for a numbered release, the revision table's newest row = the number; file name = `<number>_<code>.pdf`. Read the PDF back (text) to confirm. Any mismatch stops that drawing and is listed; the rest carry on.
5. File the PDFs into the vault as new files in `<Scene>\_Review\` (R8; e.g. `P:\APEX\SC08\_Review`), with a check-in note naming the set.
6. Record the set: which drawing versions and model versions went into it, the code, the date, who ran it (R10; flat folders, the set recorded).

Not in this step: setting the revision on drawings (James did 0A by hand), the comments register (R6), clouds (R5), `_Released`/`_WIP` moves. Those come later.

## Open points (Admin takes them to Grant on the Desk as needed)

- R10: whether each drawing keeps its own code within a set.
- Desk A4: James's own PDFs in the vault (only two today).
- The 3 STA-01 drawings with the older sheet format and no revision table.

## Done-when

A dry run of the 0A set on scratch copies lists every drawing with pass/fail. Then, after 0804 is in and with Grant's go, the real 0A run puts the PDFs in `_Review` and records the set.
