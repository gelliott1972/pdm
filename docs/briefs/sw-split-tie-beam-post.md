# Brief for SW: split the tie-beam post (0806 A, in r4)

**Done 21 Oct 2026.** Admin re-checked the r4 probe: `08XXX-HBW-01` present (one configuration, numbered), old post gone, all references inside r4.

From Admin, 21 Oct 2026. Decision 11 in `docs\imports\LS-08A-06-decisions.md`.

## Where

**In r4**, not the tidy copy: `E:\PDM-work\LS-08A-06-r4\PDM-work\LS-08A-06\_Shared\LS-08X-X_TIE_BEAM_POST.SLDPRT`. r4 is the master for 0806 A since the equations fix.

## Why

The file holds two place tags: `Default` = `08XXX-HBW-01` (all shafts; used by A's three tie beams) and `SHAFT_B_CONCRETE_BEAM` = `08BXX-HBW-01` (shaft B only; unused in A). A family holds one place tag, so it can't be named correctly as one file.

## Steps (same pattern as the tie split)

1. Back up the file and the three tie beams first (`E:\PDM-work\scratch\LS-08A-06-pre-post-split\`).
2. Close all in SOLIDWORKS. Open the post, **Save As** `08XXX-HBW-01.SLDPRT` in the same `_Shared` folder. Delete the `SHAFT_B_CONCRETE_BEAM` configuration (and its `SHAFT_B<As Welded>` partner). Check the Weight formula names the new file; fix with DmEdit if not. `PartNo` `08XXX-HBW-01` stays on `Default<As Machined>`. Save.
3. `DmEdit replace-ref` in `LS-08A-06_TIE_BEAM_1`, `_2`, `_3`: `LS-08X-X_TIE_BEAM_POST` → `08XXX-HBW-01` (TIE_BEAM_3 has one suppressed instance too; same file).
4. Delete the old `LS-08X-X_TIE_BEAM_POST.SLDPRT` from r4 (it must have no users left), or move it to the backup folder.
5. Grant opens `LS-08A-06 SCREEN+STRUCTURE` from r4, rebuilds, checks the tie beams, Save All. Then **re-probe r4** into `scratch\inventory\LS-08A-06-r4` (overwrite): expect 58 files, 108 references, all inside r4, `08XXX-HBW-01` present, the old post gone.
6. Update `sw\status.md`, message Admin and Dev.

Not in this brief: the shaft B post (`08BXX-HBW-01`) comes in with B's own set.
