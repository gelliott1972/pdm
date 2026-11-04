# Brief: import 0804 (LS-08X-04, shafts A, B and C)

From Admin, 4 Nov 2026. Same pattern as 0806 and 0811 (`docs\briefs\import-0811.md`, `docs\imports\`). Report in your own `status.md`. Items marked **open** wait for Grant.

## Where things are

| Thing | Where |
| --- | --- |
| Live 0804 models (read-only) | `M:\311v3\SC08\LS-08X-04` (FRAME\ShaftA, ShaftB, ShaftC, Plates, _Shared, _Standards; SUB). Last model change 23 Oct. |
| Tops (register, Structure Register tab) | `LS-08A-04_WITH_STRUCTUREv4`, `LS-08B-04_WITH_STRUCTUREv4`, `LS-08C-04_WITH_STRUCTUREv4` |
| Register | `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx`: Rename Register SC08 has 89 rows for 0804 (62 normal, 21 "by others", 6 virtual components to save out first); Configurations SC08 has 21 rows (the LCMM family split to `08X04-LMW`) |
| Pack and Go target, work copies | `C:\PDM-work\inbox\<top>`, `C:\PDM-work\<screen>` |
| Vault | `P:\` = `C:\PDM` (0806 A/B/C, 0811 A/B/C, `_Shared`). Vault-tracked: check-out/check-in only. |

## What's different this time

1. **Old names.** Unlike 0811, the files still have their working names (`LS-08A-04_KBEAM_S1`, `LS-08X-04 KBEAM SPLICE PLATE`, ...). This is a full rename import, like 0806. Use the register's numbers; anything missing from the register is listed for Grant, not invented.
2. **No drawings** in the 0804 folder.
3. **By others.** 21 register rows are reference models by others (GC K-beam assemblies `08A04-KBA-01` etc., the ARUP concrete beam, the SAE cantilever). SW reports how the tops use them; they load as the register says.
4. **Reuse.** Shared parts already in the vault (ties, fasteners, bolt kits, `08XXX`/`GNRIC` items) are reused, not imported again. Any change 0804 needs to an existing vault file is listed first, then done by check-out and check-in. Register open item 19: `TIE-1890-LS-08X-04-SF03_TIEBEAM` moves to `08X04`.
5. **Properties and kg (open).** The overnight job (Description, Finish, Weight; kg) has not run on the vault yet. Proposal: set these on the 0804 work copies before loading (they aren't vault files yet, so no check-out needed), from a sheet James fills in like `vault-props-J2J3-2910.xlsx`. 0804 then arrives in its final state and the overnight job stays as rehearsed.
6. **Not imported:** older tops (`v2`, `v3`, `LS-08A-04.SLDASM` etc. unless a v4 top uses them), `Assem2`, `_old` files and folders, `LS-08A-04_PnG*`, `CopyDesignTemp`, `RHS_IDEAS`, `old_files`, the conflicted copy of `LS-08X-04_KBEAM_BRACKET` (check nothing was lost), STEP/zip/jpg. The `~$` lock files (Sep) are stale.
7. **Floor plate shared with 0807** (`LS-08X-04_FLOOR_PLATE_SHARED_WITH_08X-07`): 0807 isn't in the vault, so it comes in with 0804 under its register number.

## Steps

| # | Who | Step |
|---|---|---|
| 1 | SW, now | Read-only probe of the three v4 tops straight from `M:\` (no saves). List: every file each top uses, with its register number (or "not in register"); by-others files; virtual components; files already in the vault (by ledger) and whether 0804's copy differs; anything outside `LS-08X-04`. Report to Admin. No file changes. |
| 2 | Grant (SW guides) | Pack and Go each top from `M:\` with **Include suppressed components** ticked into `C:\PDM-work\inbox\<top>`, closing all between. |
| 3 | Dev, in parallel | Get the importer ready for a full-rename import from the register (as 0806). Draft the 0804 properties sheet if Grant agrees item 5. |
| 4 | Grant, SW | Agreed fixes (virtual components saved out, etc.), then a clean Pack and Go `-r1` per top. |
| 5 | Dev | Plan from `-r1` with the register and ledger. List every change to an existing vault file. Dry run. Report to Admin. |
| 6 | Dev, Grant, App | Import new files. Changes to existing vault files by check-out and check-in. SwCheck on the tops. App `pdm.exe load --dry`, then load. |

Proposal: A, B and C in one pass, as 0811. SW says if the probe gives a reason to split.
