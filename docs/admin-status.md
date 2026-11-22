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

## 0811 import (brief `docs\briefs\import-0811.md`)

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

## Plan: drawings and vault clean-up (16 Nov)

In order. Only one thing can use SOLIDWORKS at a time (the SwCmd lock), so the profile run (SW) and Draw's work don't overlap.

### 1. Tonight 16 Nov
| # | Item | Who |
| --- | --- | --- |
| 1 | **Profiles into the vault, then repoint and relabel** (`docs\briefs\sw-profiles.md`). Starts once James has checked in, on Grant's go. Fixes James's 16 Nov note: on 08A04-HBW-05, 08B04-BRW-02, 08B04-SFP-04 and 08C04-SFP-06 the member is RHS 80x40x8 but the cut list says 80x40x4, from the description in the `M:\DLAPEX_Members` profile. Also fixes the 51 SHS members labelled RHS (`draw\cutlist-survey.md`). Grant 16 Nov: no profiles outside the vault. SW survey (read-only) from 16 Nov daytime. | SW, App loads; Grant checks the description list and the rehearsal |

**Item 1 result (17 Nov ~03:40, commit edd1044):**
- 63 parts were repointed to `P:\_Shared\_Profiles`, relabelled and checked in, including James's four RHS 80x40x8 parts and step 11 (08B04-BKP-01 is now a weldment; its configurations are Default<As Machined> and Default<As Welded>).
- 27 parts were restored unchanged, because 59 of their drawings dimension the profile sketch (Thickness, V_leg, H_leg) and those dimensions would dangle (list in `sw\profiles\survey.md` section 3).
- Refused: 08B04-KBP-01 (its cut list regroups) and 08C04-SFP-04 (its shape changes).
- No check-outs were left.
- File Locations steps: `sw\profiles\file-locations.md`.
- BOM changes: `scratch\profiles\bom-changes.tsv`.

**Resume here (17 Nov ~11:40, Grant shut the laptop):**
- All 15 drawings Grant accepted on 17 Nov are in the vault.
- 08X04-SFP-03 drawing filed (James's rename).
- FAST-HST3 is stainless 316. The 3 LED panel parts need no Material.
- James has the 08X04-BKP per-configuration label steps.
- Open, for Grant:
  - (1) 08BXX-MSW-01 weldment: A/B/C. SW's rehearsal passed, but the released 08BXX-MSA-01.SLDDRW parts list has crossed PART NUMBER/DESCRIPTION columns, and its plate part number would go blank. Admin recommends B: Draw fixes that parts list first, stays 0A. After the weldment, Draw adds the DW16 cut list and posts it on the Desk.
  - (2) File Locations on Grant's PC: Weldment Profiles = only `P:\_Shared\_Profiles` (Grant chose: delete `M:\DLAPEX_Members` and the built-in ProgramData folder). Referenced Documents stays empty. Then James's PC: check his Get latest has `P:\_Shared\_Profiles` first.
  - (3) The 27 unchanged parts plus 08X04-SFP-03, with Grant.
  - (4) DW19 08X04-BKP-07: saved view square to the plate; waits for James's check-in. Draw may trial it on a copy.
- Sessions now start with fixed names: admin, dev, sw, app, draw (`start-pdm.cmd`).

**Overnight 17-18 Nov (Grant: B for 08BXX-MSW-01; James had 0 check-outs at 23:50):**
1. Draw fixes the 08BXX-MSA-01 parts-list columns; App checks it in, still 0A.
2. SW makes 08BXX-MSW-01 a weldment.
3. Draw adds the DW16 cut list (Desk).
4. SW sets the 08X04-BKP plate label (James didn't).
5. SW adds a named view to 08X04-BKP-07; Draw drafts DW19 (Desk).
6. SW preps the 27 plus 08X04-SFP-03 on copies (`sw\profiles\the-27.md`).
7. Draw learns from James's check-ins (read-only).
8. App rehearses the D13 cases on a scratch vault.
Morning with Grant: File Locations, the 27, and the Desk.

### 2. James (this week)
| # | Item | Who |
| --- | --- | --- |
| 2 | Work through the 42 overnight drafts in the vault (night-1). Accept the magenta items, remove the *, fill DRAWN, and delete the CHECK notes once dealt with: release refuses anything left on DRAFT. The "CUT LIST SAYS RHS80X40X4" notes are fixed by item 1. | James |
| 3 | Fill DRAWN on 08A11/08B11/08C11-STA-01. "J MCKLLOP" spelling: on 08XXX-PLP-20, and again on 08A04-HBW-05 16 Nov. | Grant to James |
| 4 | Leave profile and cut-list labels for SW (item 1) | Grant to James |

### 3. Draw: the 31 not done overnight (with Grant, one at a time, draw-3)
| # | Item | Who |
| --- | --- | --- |
| 5 | A route for welded brackets and two-body welded parts (10: 6 BKP and 4 others) | Draw, with Grant |
| 6 | Views overlapping or off the sheet (8), holes or slots not found (6: mostly slots in RHS walls), program errors (3), end caps not matching (2) | Draw |
| 7 | 08B04-STA-01 and 08C04-STA-01 don't fit A3 even at 1:50 | Grant decides the sheet size, then Draw |
| 8 | Close Desk DR22 (08X04-BKP-01, now filed in night-1) and DR23 (08X06-LMW-02, already drawn by James). Compare `draw\outstanding.md` with `pdm coverage` (AWS works again 16 Nov). | Draw |
| 9 | Later: learn from James's corrections when he checks the drafts in | Draw |

### 4. SW model clean-up (after item 1, through check-out)
| # | Item | Who |
| --- | --- | --- |
| 10 | 08X04-PLP-03: the Default configuration has no PartNo or Description, so the BOM row is blank on 08B04-SFA-5B and 08C04-SFA-5B | SW |
| 11 | **Moved into tonight's run (Grant 16 Nov, via Draw): every part gets a Material, and the welded-bracket plate bodies get a cut-list Description "PLATE, t x w x l"** (sw-profiles steps 9-10). Parts with no Material (listed in `scratch\draw-test\load\night-1\batch.md`) | SW |
| 12 | GNRIC-LMW "As Welded" children with wrong lengths; 08X04-PLP-06 is only 23 cm3 | SW |

### 5. Dev
| # | Item | Who |
| --- | --- | --- |
| 13 | At import, check each description against the measured section | Dev |

### Decisions for Grant
| # | Question |
| --- | --- |
| Da | **Grant 17 Nov: leave at 0A, no re-issue**; they update at their next revision. Released (0A) drawings whose BOM text changed with item 1: 13 drawings in LS-08X-06_0A: 08A06/08B06/08C06-HBW-01/-02/-03 and -SFW-01, plus 08X06-SFW-04 |
| Dd | The 27 parts left on the Dropbox profiles. **Grant 17 Nov: work through them with him later**, so they will be fixed, not left. 59 drawings (most released 0A) dimension the profile sketch. |
| De | **Grant 21 Nov (late): B2. SW's rehearsal found the angles are inserted parts (08B04-BKP-02 / 08BXX-MSP-01), so re-insert them with "transfer cut list properties" and mirror again. This keeps the angle parts and their drawings. Rehearsal on the copy in C:\PDM-work\angles-b first; Grant approves the live change.** **On hold (Grant 22 Nov): sort out 08A06 and 08A11 first; he'll say when.** Earlier: B, rebuild as structural members (shaft B only; 2 parts, 16 users). Was: the angle items' MATERIAL cut-list text on 08B04-BKP-01 and 08BXX-MSW-01 (SOLIDWORKS rewrites the link, SwCmd's read-back refuses it). Already done: 08X04-BKP plate label (expression, 150/180 per configuration, checked in 18 Nov); FAST-HST3 Material (17 Nov); the 3 PANELS parts need no Material (Grant 17 Nov). Admin re-raised the plate label by mistake on 21 Nov; cancelled. |
| Db | **Grant 21 Nov: leave at A3; James does the top-level (structure) assembly drawings by hand.** Draw doesn't draft them. |
| Dn | **Grant 21 Nov:** new 08A11 bracing parts need drawings (08A11-HBA-04, -HBW-04, -HBW-05, -DBW-01): later (bracing released to Kevin). App builds a "Needs drawing" list on the page (yes, "great for James"), plus the missingRefs re-resolve fix; ships with Make PDFs. |
| Dc | Draw's order: welded brackets first, or the other failures? |
| D6 | Read-only survey of the vault for dangling dimensions? (unanswered) |

## Open

- App signing: shelved 24 Oct (Grant). Azure Artifact Signing isn't open to NZ companies; a bought certificate needs days of identity checks. Until then Smart App Control is off on each engineer's PC.
- GitHub-to-AWS trust for CI (later).
- Cantilever brace fix (decision 2, A): first real check-out/check-in edit.
- 0804 tie file (A decisions, "r4 accepted").
- Shaft codes: Grant kept them as they are for now (D29); A+C parts are `X` in 0811. May come back.
