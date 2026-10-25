# Admin status

Updated 26 Oct 2026, on the laptop. The next Admin session reads this first.

## Where we are

| | |
| --- | --- |
| MVP done-when | Met 24 Oct: check-out from the page on the desktop, laptop saw the lock |
| App updates | 0.1.7 (25 Oct): buttons on top; check-in one file at a time with a progress bar; checked on dummy data only. 0.1.6: Document Manager reads drawings. 0.1.5: add new files from the page; drawings carry no PartNo. 0.1.4: list no longer blanks when SOLIDWORKS holds a file open. All published from the laptop. |
| Vault | 0806 A, B and C plus `08A06-HBA-01.SLDDRW`: 94 files. No locks at the 25 Oct scan (James's 0806 A set was checked back in). `08XXX-PLP` has a new version (26 Oct, Grant): HDG drain hole unsuppressed in `PL_200x140-Perpendicular slots` (0811 D28). |
| Decisions | `docs\imports\LS-08A-06-decisions.md`, `docs\imports\LS-08BC-06-decisions.md`, `docs\imports\LS-08X-11-decisions.md` (D1–D29, all settled) |
| Ledger | `data\rename-ledger.csv` (A 164 rows, B/C 70) |
| Register | `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx` |
| Way of working | Since 25 Oct: Grant decides only in Admin; SW gives SOLIDWORKS steps; Dev and App ask Admin. Fixed labels per import (memory `one-window-for-decisions`). |

## 0811 import (brief `docs\briefs\import-0811.md`)

Prep in `C:\PDM-work\LS-08A-11`, `-08B-11`, `-08C-11` is done: STAs (D7), LCMM and brace splits (D14, D10), skeleton and MAIN_MEMBERS equations unlinked (D21), post (D24), Weight formulas (D26), STA properties (D25). Next: SW's full-tree SwCheck of the three tops, the `-r1` Pack and Go, then Dev plans A, then B, then C (C reuses B's `08Z11` subframes, D20), dry run, import, App loads.

## Known issue

Document Manager keeps a stale referenced configuration after Replace Components (and old component names and `E:\PDM-work` paths in its component list). In the vault: `08X06-LMW` [02A] and `08X06-SFW-01` [Default] say `08XXX-PLP-31`; SOLIDWORKS uses `-32`. No harm: the app uses only the reference table (App, 26 Oct), and Dev's plan takes SOLIDWORKS' list (`--swcheck`). Any future BOM or "configuration used" view must not come from DM's component list. 0806 A not yet checked (needs a P:\ SwCheck of its top).

## Next

1. 0811 import, above.
2. 0806 B/C grout and anchors, in the PDM (decision 7). B's concrete-beam anchor needs a new configuration in `MDL-ANCHOR-08X06`. Grant: "adding grout is small".
3. D17 after the 0811 import: FAST-HEX/NUT/WSH PartNos by check-out.
4. Ask Grant about App 0.1.7 at his next real multi-file check-in: did the bar show "Checking in n of m" and fill? Are the buttons at the top?
5. List for James: D3 drawings, D8/D9 register mismatches, D4/D20 changes, the splice kit weight (D26), D28.

## Open

- App signing: shelved 24 Oct (Grant). Azure Artifact Signing isn't open to NZ companies; a bought certificate needs days of identity checks. Until then Smart App Control is off on each engineer's PC.
- GitHub-to-AWS trust for CI (later).
- Cantilever brace fix (decision 2, A): first real check-out/check-in edit.
- 0804 tie file (A decisions, "r4 accepted").
- Shaft codes: Grant kept them as they are for now (D29); A+C parts are `X` in 0811. May come back.
