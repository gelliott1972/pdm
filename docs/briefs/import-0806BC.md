# Brief: import 0806 B and C

From Admin, 23 Oct 2026. Same pattern as A (`docs\imports\LS-08A-06-decisions.md`). Report in your own `status.md`.

**New rule: `P:\` is vault-tracked.** 0806 A is loaded into the PDM (57 files). Nobody changes a file already on `P:\` outside the PDM: no re-applying imports, repointing, property writes or clearing read-only. A change to an existing vault file goes through `pdm.exe checkout` / `checkin`, and App is told first. New files are added by the importer (overwrite off) and then loaded by App with `pdm.exe load`.

| # | Who | Step |
|---|---|---|
| 1 | Grant | Pack and Go 08B06 and 08C06 from the laptop ("Include suppressed components" ticked) into `E:\Grant\Dropbox\PDM-inbox\<screen>`. |
| 2 | SW | Copy each into `E:\PDM-work\<screen>`, probe it, and list for Admin: files new to the vault; files already in the vault (by ledger) and whether B/C's copy differs from the vault's; the known B/C items (B's tie-beam post `08BXX-HBW-01`, the right-angle tie's SF01/SF04 configurations, the end plate equations link, Weight formulas, bolt kit Promote, Dropbox or `PDM-work` paths). No file changes until Admin agrees the list with Grant. |
| 3 | Grant, SW | The agreed fixes, then a clean Pack and Go `-r1` per screen. |
| 4 | Dev | Plan from `-r1` with the ledger (files already in the vault = reuse; their B/C copies are not imported). List every change B/C needs to an existing vault file (new PartNos on existing configurations, new references). Dry run. Report to Admin. |
| 5 | Dev, Grant, App | Import new files. Changes to existing vault files: Grant checks them out with `pdm.exe`, Dev applies, Grant checks in. Then App runs `pdm.exe load`. SwCheck on the B and C tops. |
