# Brief: weldment profiles into the vault, then repoint every member

From Admin, 16 Nov 2026. Owner: **SW** (App loads the files). Grant runs this **tonight (16 Nov), once James has checked in**. Grant gives the go in his own words. SPEC already plans for this: profiles in `P:\_Shared\`, and File Locations on every PC pointing there.

## Why

James, 16 Nov, on 08A04-HBW-05: the member is RHS 80x40x8, but the cut list says RHS80x40x4. The member uses a profile from `M:\DLAPEX_Members` (Grant's live Dropbox), and that profile's description is wrong. Grant: there should be no profiles outside the vault. An earlier exercise pointed almost every member at `M:\DLAPEX_Members`, with perhaps one or two stragglers. So exact copies of those files in the vault, with the descriptions corrected, can be swapped in without changing any shape.

## Steps

| # | What | Rules |
| --- | --- | --- |
| 0 | **Before tonight (read-only prep, allowed now).** Survey every weldment part in `P:\`: each structural member's profile path, standard, type and size; its measured section; and its cut-list label. Flag stragglers (any path other than `M:\DLAPEX_Members`), labels that disagree with the measured section (start from `draw\cutlist-survey.md`: the 51 SHS labelled RHS, the four RHS 80x40x8 labelled x4), and labels typed by hand into the cut list. Write the result to `sw\profiles\survey.md`. Prepare the scripts and the rehearsal. | Read only. Document Manager, or SOLIDWORKS only with a free lock (`--as sw`). |
| 1 | **Copy the library.** Copy `M:\DLAPEX_Members` exactly, with the same folders, file names and configurations, into a staging folder `scratch\profiles\_Profiles\`. | **Never write to `M:\`.** Copy only. |
| 2 | **Correct the descriptions in the copies.** Only description and label properties, never sketches or dimensions. Each copy's sketch must match its original exactly; check it. List every change in `sw\profiles\library-changes.md` for Grant. | Grant checks the list before the load. |
| 3 | **Into the vault.** App adds the staging folder as `P:\_Shared\_Profiles\` with `pdm load --note "Weldment profiles from M:\DLAPEX_Members, descriptions corrected"`. | App does the load. |
| 4 | **File Locations.** On Grant's PC, change Weldment Profile Folders from `M:\DLAPEX_Members` to `P:\_Shared\_Profiles` (Tools > Options > File Locations). Leave the built-in SOLIDWORKS entry for now. Write the same steps for James's PC, with exact menu names, for Grant to pass on. | One small step at a time. |
| 5 | **Rehearse on copies.** Copy the affected parts, their assemblies and their drawings into `scratch\profiles\rehearsal\`, then run steps 6 and 7 in full on the copies. | Grant sees the rehearsal result before step 6 runs on `P:\`. |
| 6 | **Repoint and relabel, through check-out.** For every member not on `P:\_Shared\_Profiles` (stragglers included): change its profile to the vault copy with the same standard, type and size. Then update the cut list and confirm each label matches the measured section; use `SwCmd set-cutlist-prop` where a label doesn't update. Folder by folder: check out, edit, check, check in, with the note "Profile repointed to the vault; cut-list label corrected". | Skip any file someone else has checked out, and list it. |
| 7 | **Checks, before and after, on every changed part.** Same mass, volume and bounding box. No rebuild errors. Every drawing that shows the part has the same number of dimensions and 0 dangling. Every assembly using it rebuilds with no new errors. | **Any difference: don't check that file in. Undo its check-out and list it.** |
| 8 | **PDFs.** Regenerate the PDFs of the changed drawings that are not released. List the released drawings (0A and later) whose BOM text changes. | Grant decides about re-issuing. Change no revisions. |

## Not in this brief

- Changing any member's size or shape. Members that are really the wrong size go on a list for Grant.
- Deleting or renaming anything in `M:\DLAPEX_Members`. Grant decides about the Dropbox folder after the vault copy is in use.

## Report

One message to Admin when the rehearsal is done (step 5), and one when the run is done: files changed, labels corrected, stragglers, skipped files, and any checks that failed. Questions for Grant go as Desk tickets with owner `sw`.
