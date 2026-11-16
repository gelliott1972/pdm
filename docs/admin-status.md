# Admin status

Updated 26 Oct 2026 (after the 0811 import), on the laptop. The next Admin session reads this first.

## Where we are

| | |
| --- | --- |
| MVP done-when | Met 24 Oct: check-out from the page on the desktop, laptop saw the lock |
| App updates | 0.1.7 (25 Oct): buttons on top; check-in one file at a time with a progress bar; checked on dummy data only. 0.1.6: Document Manager reads drawings. 0.1.5: add new files from the page; drawings carry no PartNo. 0.1.4: list no longer blanks when SOLIDWORKS holds a file open. All published from the laptop. |
| Vault | 181 files, 186 PartNos (26 Oct): 0806 A, B, C, `08A06-HBA-01.SLDDRW`, and **0811 A, B, C imported and loaded 26 Oct** (87 new files, 53.8 MB; index = S3 = 181). `08XXX-PLP` has a new version (drain hole, D28). |
| Decisions | `docs\imports\LS-08A-06-decisions.md`, `docs\imports\LS-08BC-06-decisions.md`, `docs\imports\LS-08X-11-decisions.md` (D1–D33, all settled) |
| Ledger | `data\rename-ledger.csv` (0806 A 164 rows, B/C 70; 0811 135: A 58, B 51, C 26) |
| Register | `data\APEX_Structure_Part_Numbering_Standard_Rev3.xlsx` |
| Way of working | Since 25 Oct: Grant decides only in Admin; SW gives SOLIDWORKS steps; Dev and App ask Admin. Fixed labels per import (memory `one-window-for-decisions`). |

## 0811 import (brief `docs\briefs\import-0811.md`)

Done 26 Oct. Decisions D1-D33 in `docs\imports\LS-08X-11-decisions.md`. Import route as 0806 plus a new step: SwResave-import of the new assemblies after SwFixRefs (Importer DM renames left stale instance names; a configuration switch then lost suppression). After-import checks now include SwSuppDump (every sub-assembly configuration). Rehearsed 4 times on scratch copies of P:\ first. Work copies kept aside as `C:\PDM-work\LS-08x-11-hold` (pre-import record). Left: D17 fastener PartNos by Grant's check-out.

## Known issue

Document Manager keeps a stale referenced configuration after Replace Components (and old component names and `E:\PDM-work` paths in its component list). In the vault: `08X06-LMW` [02A] and `08X06-SFW-01` [Default] say `08XXX-PLP-31`; SOLIDWORKS uses `-32`. No harm: the app uses only the reference table (App, 26 Oct), and Dev's plan takes SOLIDWORKS' list (`--swcheck`). Any future BOM or "configuration used" view must not come from DM's component list. 0806 A checked 26 Oct: same 2 entries only.

## Next

1. D17: 9 fastener PartNos (FAST-HEX/NUT/WSH) by Grant's check-out; Dev writes, Grant checks in.
2. 0806 B/C grout and anchors, in the PDM (decision 7). B's concrete-beam anchor needs a new configuration in `MDL-ANCHOR-08X06`. Grant: "adding grout is small".
2a. **Activity tab (Grant 9 Nov)**: admin-only view of what was checked in each day, per person, to track James's progress. Step 1 only; progress against a plan (step 2) later. Grant OK'd App's design (per-file history on check-in, backfill from S3 versions, tab in 0.1.12, admin = Grant hard-coded) and added: new drawings (and sheets) per day, drawing coverage per screen/shaft, and a simple finish forecast ("how long until James has finished 0804 Shaft A"). Coverage rule's judgment calls go to Grant. Users skip 0.1.11 (Grant 9 Nov): 0.1.12 carries the release guards, update check on Reload list + hourly, tray icon instead of the console window, Activity tab, re-issue option, new release checks; coverage/forecast if ready, else 0.1.13. Min version 0.1.12 once Grant's and James's PCs show it.
3. Tidy later: Dev's scratch vaults; the `-hold` work copies. **Grant 9 Nov: after the 0A filing, tidy his PC**: old vault copies and intermediate files (e.g. old P:\ stand-ins, scratch and rehearsal copies, PNGs), so he can't open an old copy by mistake (he checked a stale copy of 08XXX-PLP-33 on 9 Nov). Admin lists everything first; Grant OKs each delete. **Done 9 Nov (Grant's yes):** 4.6 GB of practice copies went to the Recycle Bin (all of C:\PDM-work, which is kept empty as the work root; the scratch\import rehearsal and test vaults; scratch\inventory; input\). Kept: scratch\import reports and logs, gnric-lwp (Dev says when it can go), restructure-0804, and _kept\ (evidence cited in sw\status.md). `P:\Claude outputs` was moved to `Documents\Claude outputs` (Grant: P:\ is PDM only).
4. Ask Grant about App 0.1.7 at his next real multi-file check-in: did the bar show "Checking in n of m" and fill? Are the buttons at the top?
5. List for James: D3 drawings, D8/D9 register mismatches, D4/D20 changes, the splice kit weight (D26), D28.
6. James's notes 28 Oct (`scratch\notes_from_james_2026-10-28.txt`), Grant's answers:
   - J1 failed open of 08A11-STA-01: James hadn't done Get latest; likely the cause.
   - J8 read-only after check-out: normal; close the file, check out, reopen. No app feature.
   - J2 Description/Finish/Weight missing in 0806 (never in the source): add from a spreadsheet, as an overnight job.
   - J3 DrawingRef (31 rows, all 0811): remove in the same job as J2.
   - J4 mass units grams to kg: strategy agreed; when is still open.
   - J5 Issue/PDF: build 29 Oct.
   - J6 check-out ticking, "check all" with drawings, check in unchanged files, filter: asked App 28 Oct.
   - State at end of 28 Oct:
     - App: 0.1.8 built (same-name drawings, tick all, check-out gets what's under it, check-out and Get latest progress). Not published: the laptop couldn't reach AWS (network).
     - Dev: sheet `scratch\import\vault-props-J2J3.xlsx` sent to Grant. Writer (DmEdit batch --checked-out, 6a65bce) tested on scratch: 288 changes in 106 files. Q1–Q6 (in the sheet) for Grant and James on 29 Oct.
     - SW: units survey done; kg tool (SwUnits/SwKg) in progress. Joint rehearsal with Dev not yet run.
     - Overnight run: another night, after the sheet is filled in.

## To-do from the drawings work (16 Nov)

| # | Item | Who |
| --- | --- | --- |
| T0 | **Tonight 16 Nov (Grant runs it once James has checked in): `docs\briefs\sw-profiles.md`.** Copy `M:\DLAPEX_Members` into the vault at `P:\_Shared\_Profiles` with the descriptions corrected, switch File Locations, repoint every member, and correct the cut-list labels. This covers T1 and T2. Grant 16 Nov: no profiles outside the vault; nearly all members already point at `M:\DLAPEX_Members`. | SW, App loads |
| T1 | **James 16 Nov, 08A04-HBW-05:** the cut list says RHS80x40x4, but the member is RHS 80x40x8. The structural member uses the custom profile "RHS80X40X8" from Grant's weldment profile library in Dropbox (`C:\Users\grant\Dropbox\Unilumi...`, Size field). That profile's description, which feeds the cut list, says 80x40x4. The geometry is 8 mm: the weight is 23.6 kg for 1.9 m, and the 72 x 32 in Detail A is the end plate, not the inside of the tube. Fix: (1) correct the description in the library profile (outside the vault and live Dropbox, so only with Grant's go); (2) update the cut lists through check-out in 08A04-HBW-05, 08B04-BRW-02, 08B04-SFP-04, 08C04-SFP-06, and the GNRIC-LM rows in `draw\cutlist-survey.md`. | Grant OK, then SW (Dev's `set-cutlist-prop`) |
| T2 | 51 SHS 40x40x4 members whose cut lists say RHS (`draw\cutlist-survey.md`). Probably the same kind of profile-description error, so check the library first. | SW |
| T3 | 08X04-PLP-03: the Default configuration has no PartNo or Description, so the BOM row is blank on 08B04-SFA-5B and 08C04-SFA-5B | SW |
| T4 | Parts with no Material (CHECK notes in `scratch\draw-test\load\night-1\batch.md`) | SW |
| T5 | Structure assemblies 08B04-STA-01 and 08C04-STA-01 don't fit A3: bigger sheet or two sheets? | Grant |
| T6 | 31 drawings not done overnight: welded brackets and two-body welded parts (10), views overlapping or off the sheet (8), holes or slots not found (6), program errors (3), end caps (2), structures (2, T5) | Draw, order from Grant |
| T7 | Notes for James: fill DRAWN on 08A11/08B11/08C11-STA-01; "J MCKLLOP" spelling (on 08XXX-PLP-20 and again on 08A04-HBW-05 16 Nov); on signed drafts, remove or move the CHECK note and other DRAFT items, or release refuses them | Grant to James |
| T8 | D6: read-only survey of the vault for dangling dimensions (unanswered) | Grant |
| T9 | Dev: an import-time check of the description against the measured section | Dev |

## Open

- App signing: shelved 24 Oct (Grant). Azure Artifact Signing isn't open to NZ companies; a bought certificate needs days of identity checks. Until then Smart App Control is off on each engineer's PC.
- GitHub-to-AWS trust for CI (later).
- Cantilever brace fix (decision 2, A): first real check-out/check-in edit.
- 0804 tie file (A decisions, "r4 accepted").
- Shaft codes: Grant kept them as they are for now (D29); A+C parts are `X` in 0811. May come back.
