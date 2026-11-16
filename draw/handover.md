# Draw handover (16 Nov 2026, morning)

The previous Draw session ran out of context after the overnight run. Read this file, then `draw\status.md`, then the skill `draw\.claude\skills\drawing-standards\SKILL.md` (it holds Grant's learned rules). Then ask Grant for his information on the large assemblies and whether item 5 or item 6 comes first.

## Today's list (Admin, 16 Nov; plan in docs\admin-status.md, "Plan: drawings and vault clean-up")

| # | Item | Detail |
| --- | --- | --- |
| 5 | A route for welded brackets and two-body welded parts | 10 parts: 08B04-BKP-01, -03; 08X04-BKP-02, -03, -06, -07; 08Z04-SFP-01; 08A04-HBW-03; 08X06-SFW-02, -03 |
| 6 | The other overnight failures | Views overlapping or off the sheet (8), holes or slots not found (6, mostly slots in RHS walls), program errors (3), end caps not matching (2). The PartNos are in `night-1\batch.md` |
| 7 | 08B04-STA-01 and 08C04-STA-01 | Too big for A3 even at 1:50. Grant decides the sheet size |
| 8 | Housekeeping | Close Desk DR22 (08X04-BKP-01, now filed in night-1) and DR23 (08X06-LMW-02, already drawn by James). Compare `draw\outstanding.md` with `pdm coverage` (AWS works again) |
| 9 | Later | Learn from James's corrections to the 42 night-1 drafts when he checks them in |

**Working mode:** brief draw-3, one drawing at a time with Grant in this window. Generate, open and say "ready". Grant tweaks it or asks for edits; never regenerate a drawing he has touched. On "good": save, check, PDF, copy to a load folder, repoint to P:\, check again. Record his edits in the skill's "Learned from Grant's edits".

**SOLIDWORKS:** SW needs the lock for 30–45 min today (parts survey) and tonight (profile run), so there is no Draw overnight run tonight. Use `SwCmd lock --as draw --purpose "..."`. Release it when Grant finishes for the day, and tell Admin.

**Cut-list labels** in the profile library are wrong (e.g. "RHS80X40X4" on an 80x40x8 model). SW fixes them tonight. Until then keep flagging label/measured mismatches as CHECK notes (Draw already does this). The 08X04-PLP-03 properties and the missing materials are on SW's list.

## Where things stand

| What | Where | State |
| --- | --- | --- |
| Batches 1 and 2 (10 + 6, approved by Grant) | `scratch\draw-test\load\batch-1`, `batch-2` | Loaded |
| Overnight: 42 drafts | `scratch\draw-test\load\night-1\` + `batch.md` (every drawing, CHECK notes, the 31 failures and why) | Repointed to P:\, checked (0 dangling); sent to App (app-79) to load, not yet confirmed |
| Overnight working copies | `scratch\draw-test\overnight\APEX\` (copies of P:\APEX\SC08\LS-08X-04, -06, GNRIC, SC08\_Shared). Older versions in `old\` subfolders | Drafts are drawn here, then filed |
| Overnight logs | `scratch\draw-test\night-logs\b3`, `b4`, `b5` (one txt per drawing + `log.tsv`) | |
| Overnight scripts and lists | `draw\scripts\night\` | See below |
| Code | `draw\src`, last commits 379fdb6, 800af13, 7d4a469 | All committed; Admin pushes |

## Tools (draw\src\bin\Draw.exe; build with `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` in draw\src)

- **Build gotcha:** Draw.exe can't be rebuilt while any Draw.exe runs; the build then fails silently. Check the exe's timestamp after building.
- **Make:** `make-assembly <asm> <cfg> <out.SLDDRW> <template>`, `make-section` (members, angles, welded members with small end pieces), `make-plate` (plates). Template: `C:\code\pdm\scratch\draw-test\_Templates\Unilumin-A3.DRWDOT`. Options:
  - `--check="text"` adds a CHECK line for James (make-section, make-assembly; make-plate reads it too).
  - `--scale=N` only affects the long-member route. The assembly scale list is 5, 10 … 50, and 1:2 isn't on it.
- **Edit the open drawing** (no save; then `SwCmd save <file> --as draw`): remove-note, add-projection, move-view, delete-view, hole-dims, copy-layout, align-iso, save-active, place-flags.
  - `place-flags <drw> [--check="..."]` moves the CHECK note and can add a line. On a reopened assembly drawing the view outlines are wrong (no leafBoxes), so it often falls back to outside the border. Remaking the drawing with `--check` places the note better.
- **Check:** `check <drw>` (dangling, slot check), `pdf <drw> <pdf>` (refuses if the pdf exists), `close-mine`.
- **New overnight:**
  - make-plate takes a multi-body part as its plate (the flattest large body), with a CHECK note. It fails on the welded brackets because the RHS arm is as big as the plate. That is item 5's starting point: these need their own route (plate + member).
  - ExtentTrue counts upright dimension text, so CHECK notes no longer sit on labels.
- **Debug:** set `DRAW_DEBUG_EXTENT=1` to print each view's annotation boxes (ExtentTrue).

## Overnight scripts (`draw\scripts\night\`, bash)

- `run_night.sh LIST TAG`: one draft per row (PartNo, file, config, folder under APEX). Assemblies go to make-assembly. Parts go to make-section, then make-plate if that is REFUSED. Skips drafts that exist. Logs go to `$S/TAG/` (S defaults to night-logs).
- `file_draft.sh BATCH PN FOLDER`: copies to `load\BATCH\APEX\FOLDER`, repoints every overnight ref to P:\, then runs check and PDF.
  - The basename bug is fixed: write `${r##*"$B"}`, with `$B` in quotes. Unquoted, it never matched, so the first pass didn't repoint.
- `file_list.sh LIST BATCH [RESULTTAG]`: runs file_draft.sh for each row (PartNo, folder).
- `rows.sh CHECKFILE NOTESFILE`: writes the batch.md table rows.
- **Bash and backslash paths:** use `B='\'` and `${var}`. Write scripts with the Write tool rather than heredocs inside commands.

## Learned overnight (not yet in the skill)

- SOLIDWORKS's DisplayData text heights look like sheet size for dimensions (no scaling found). Balloon leader points are SOLIDWORKS's own (Grant: leave the balloons).
- The 08B/08C04-SFA-5B BOM uses 08X04-PLP-03's Default configuration, which has no properties. 08A04-SFA-5B uses 08X04-MSA-01 there.
- The 08X04-BKA-01 to -04 small assemblies would need 1:2.5 to be readable; 1:2 doesn't fit four projections.
