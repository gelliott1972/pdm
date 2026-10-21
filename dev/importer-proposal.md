# Importer: proposal

Dev, 21 Oct 2026. For Grant to agree before building. Engine: Document Manager (agreed 20 Oct, after the proving tests in `status.md`).

## Shape

Two steps, each a small tool, with Grant's review in between (as now).

| Step | Tool | In | Out |
| --- | --- | --- | --- |
| 1. Plan | `tools\importplan\make_plan.py` (exists; extended) | Probe inventory, register, ledger, decisions | `<screen>-import-plan.xlsx`, which Grant reviews and fills in |
| 2. Import | `tools\Importer` (new, C#, Document Manager) | The reviewed plan, the source folder, the vault root | Files in the vault, ledger rows, a report |
| 3. Confirm | `tools\SwCheck` (exists) | The top assemblies in the vault | Pass, or a list of anything missing or outside the vault |

Step 1 also writes the reviewed plan out as a plain `<screen>-import-actions.csv` (Python reads Excel well; the C# side then only reads CSV). One row per file: source path, action, new name, new folder; plus one row per PartNo and one per repoint rule.

## What the importer does, in order

Default is a **dry run**: it does every check and writes the report, and touches nothing. `--apply` does the work.

| # | Step | Detail |
| --- | --- | --- |
| 1 | Read the actions | Files to import, files to leave out, files already in the vault (reused from the ledger), and repoint rules |
| 2 | Check before touching anything | Every source exists; no two files get the same new name; no new name already in the vault (unless it is a reuse); the file name agrees with its PartNo; every reference of every imported file has somewhere to go (imported, reused, replaced, or left out and only used by suppressed components). Any failure stops the run. |
| 3 | Copy | Each file copied (never moved) to `vault\<new folder>\<new name>`. The source is never changed. |
| 4 | Repoint | In each copied file, `ReplaceReference` for every reference: to the new copy, to the file already in the vault (shared, reused), or to the replacement file (retired plate, split families). Includes part links (return -> brace, skeletons). |
| 5 | Number | `PartNo` at the level James uses (file or configuration); `PartNoStatus = Provisional` next to it where the number came from the sample register. |
| 6 | Ledger | One row per file and per numbered configuration in `data\rename-ledger.csv` (columns as already set up). |
| 7 | Check after | Re-read every vault file with the Document Manager: every reference resolves inside the vault, every PartNo reads back. |
| 8 | Report | Plain language: counts, what was done, anything left out or failed, what to check in SOLIDWORKS. |

Then `SwCheck` on `LS-08A-06+STRUCTURE` and `08A06-STA-01` (SOLIDWORKS closed first), and Grant opens them himself.

## Safety

- Refuses any source or target path inside Dropbox.
- Never deletes. To re-run, the screen's folders in the test vault are cleared first (asked each time).
- Never edits the source folder; all edits are on the vault copies.

## Answered (21 Oct)

- 1 and 2: the splits and the retired plate are repointed during prep (decision 10, done by SW with `DmEdit`); r4 arrives final. The importer only renames, moves, repoints and numbers. It still checks that an assembly doesn't use two split files of the same family, in case B, C or 0811 need it.
- 3: yes, `subst P:` to the test vault before importing (Grant).
- 4: LCMM's unused brace and K-beam plate were deleted in the split. Any left are listed in the report.

## Risks and choices for Grant (as proposed 21 Oct)

1. **Split families need per-assembly repointing.** `ReplaceReference` changes every use of a file inside one assembly at once; it can't send one tie to `GNRIC-BRP` and another to `GNRIC-BRA` in the same assembly (`Component.Replace` would, but didn't work in the test). In 0806 A no assembly mixes them (the structure uses only bolted ties, the subframes only welded), so a rule "in this assembly, `TIE_80x40x8` -> this file" works. The importer checks this and stops if an assembly mixes them. If B, C or 0811 mix them, those few get fixed in SOLIDWORKS.
2. **Where the final files come from.** Some files must come from somewhere other than the r3/r4 Pack and Go: the latest `MEMBER_SHS_40x80x8` (changed 19 Oct), James's tie file, and the split files SW is preparing (`08X06-LMW`, `GNRIC-BRP`, `GNRIC-BRA`). Proposed: **r4 contains every file in its final state**, and the importer only renames, moves, repoints and numbers. It doesn't merge, split or edit models.
3. **Write `P:\` paths from the start.** If the test vault is mapped as `P:` (a temporary `subst P: E:\PDM-test\P`) before importing, the stored references are `P:\APEX\...` from day one, which is what Test 3 needs. Otherwise they are `E:\PDM-test\P\...` and need repointing again later.
4. **Left-out files still referenced by suppressed components** (e.g. LCMM's floor plate, 08X11 brace, K-beam plate). They never load, but the references stay. Proposed: leave them and list them in the report. In `08X06-LMW` they mostly go away when the other screens' configurations are removed in the split.

## Build order

1. Extend the plan tool: the retire/replace and split-family rules from the decisions, the actions CSV.
2. Importer dry run on r3, report only.
3. Importer `--apply` on r3 into the test vault, then `SwCheck`.
4. When r4 arrives: re-plan, Grant reviews, import, Grant opens it.
