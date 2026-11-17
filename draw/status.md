# Draw: status

Brief: `docs\briefs\draw-1.md` (14 Nov 2026). Test folder: `C:\code\pdm\scratch\draw-test` (Grant: nothing in the root of C:\). Tool: `draw\src\` (Draw.exe; build with `build.ps1`).

## Brief draw-2 (15 Nov): all outstanding drawings, one batch per type

| Step | State |
| --- | --- |
| Outstanding list | Done: `draw\outstanding.md`, 89 of 319 items (LMW 10, PLP 7, BKP/BKA/MSA 15, singles and simple weldments 27, SFA/STA 30), from P:\ with `src\Outstanding.cs` (read-only, pdm coverage rule). Counts sent to Admin. Not yet compared with pdm coverage (Grant's AWS login) |
| Batch 2 PLP | **Done 15 Nov**: 08X04-PLP-01, -02, -03, -06, -32, -33 approved by Grant and in `load\batch-2\` (batch.md). -05 (a welded part) moved to batch 4 |
| Overnight 15-16 Nov | Grant: Draw has SOLIDWORKS overnight; James works from the drafts, so drafts that pass the checks are filed. Order: batch 5 subframes/structures (James needs them), then batch 3, then batch 4 single members. Copies of P:\APEX\SC08\LS-08X-04, -06 and GNRIC in `scratch\draw-test\overnight\`. Regression: 08B04-SFA-2T rerun, all member positions, heights and BOM match the reviewed version, 0 dangling. Done 16 Nov ~01:30: 73 tried, 42 filed in `scratch\draw-test\load
ight-1\` (28 subframes, 7 brackets/small assemblies, 7 members/small assemblies), repointed to P:\ and checked (0 dangling); 31 not filed, each with its reason in night-1\batch.md. Sent to App to load; counts to Admin. Next: a route for welded brackets (plate + RHS arm); the batch 4 failures (slots in RHS walls, end caps, program errors); STA structures need a bigger sheet |
| Batch 1 LMW | **Done 15 Nov: all 10 GNRIC-LMW approved by Grant one at a time (draft-3) and in `scratch\draw-test\load\batch-1\`** (-01 to -06, -09 to -12), each saved as he left it, repointed to `P:\APEX\GNRIC\GNRIC-LMW.SLDPRT`, opened read-only to check (values unchanged, 0 dangling, slot check OK), PDF identical to the approved one; list in `load\batch-1\batch.md`. Desk DR12-DR21 closed. Rules learned (in the skill): CHECK notes outside the border bottom left; isometric centred on the main view; caps at both ends: matching TOP END / BOTTOM END cap views; one-wall holes "for HDG Drainage"; views and labels kept inside the border and clear of the note block. Waiting: App to load; Grant on DR22 (08X04-BKP-01, accepted) and DR23 (08X06-LMW-02, redo) |

## Done

| Step | Result | Checked? |
| --- | --- | --- |
| 0 Templates | From a copy of James's GNRIC-PLP-06.SLDDRW into `scratch\draw-test\_Templates`: `Unilumin-A3.slddrt` (sheet format), `Unilumin-A3.drwdot` (drawing template: his document settings, revision table emptied, old revision and date cleared), `Unilumin-revision.sldrevtbt`, `note-block.txt` | A new drawing from the template matches James's sheet. BOM and cut-list table templates **not yet** (plate drawing has neither; take from an assembly and a weldment drawing before step 4) |
| 1 One plate | **GNRIC-PLP-06**. Draft v3 `scratch\draw-test\PLP-06\GNRIC-PLP-06.SLDDRW`; PDFs side by side in `PLP-06\review\` (James, draft v1, draft v3). 11 s to make | Values checked against the model by the tool and against James's PDF by me. Grant reviewed v1 |
| 2 Rules, undrawn plate, bracket | Rules: `draw\.claude\skills\drawing-standards\SKILL.md`; layout learned from Grant's hand-laid-out drawings (`learn-layout`, `layout-examples.json`). **08X04-PLP-01** (undrawn plate) worked first time. **08X04-BKP-01** (angle, two slots) with new `make-section`: face view, linked end view, slot callout, section sizes, CHECK flags (L=150 vs 100, no material, weight) | Grant: bracket layout right, plate layout good (14 Nov). Desk DR1 open (100 or 150 long) |
| 3 Copy-then-repair | **08A04-SFA-2T (James) -> 08B04-SFA-2T**, copies in `scratch\draw-test\SFA-2T`. Shaft B is a mirror of A with different parts (08B04-BRW/SFP, 08Z04-SFP-01 instead of 08A04-HBW, 08X04-BRP-01). After switching a copy of James's drawing to B (`copy-switch`): of 23 dimensions 3 unchanged, 16 loose, 4 gone; 18 of 25 balloons/notes stayed attached but the BOM renumbered, so they now label different parts; views come out mirrored and overlapping. Not saved (D1). PDFs: `SFA-2T\08A04-SFA-2T - James.pdf`, `...ShaftB\08B04-SFA-2T.switched.pdf`; item list `.switch-report.tsv` | Measured. **Finding:** copy-then-repair isn't worth it for mirrored twins; draft B fresh (step 4 route), using A's drawing as the style example. **Also:** our copy of James's 08A04-SFA-2T already has 10 of 23 dimensions dangling before any switch (not yet checked on P:\) |
| 4 One assembly | **08B04-SFA-2T** fresh (`make-assembly`), copies in `scratch\draw-test\SFA-2T`. Sheet planned from the model's size (Grant): isometric 1:25 centred under James's BOM, balloons in columns round it (Grant's arrangement); front, top, bottom, left, right at 1:30, balanced, room for dimensions. Dimensions: member positions as ordinates (heights 0/230/1230/2296/(2586), uprights 0/1296/1480/1980/2480/2905/(2975)), depth 600, all checked against the model. Flags: dims to underside/left side of members; welds not added. About 3.5 min per run (views about 170 s is SOLIDWORKS) | Grant reviewing on his phone. Not yet: speed (draft quality / batch, Grant: later), weld symbols, whether James dims to member undersides or centrelines |
| LED members | **08X06-LMW-02 and -01** (James's LMW family; Admin picked them), copies in `scratch\draw-test\LMW`. Long-member mode: isometric, front, side (plates face-on), end view of the top plate; plates and holes as ordinates down the member (plate edges in brackets, exact values); overall length 2155 beside the member (Grant 15 Nov, `08X06-LMW-02-grant`; the RHS length is in the BOM); zig-zag breaks only as long as needed. Scale rule: the largest scale whose breaks cut out at most a third (1:10 here; `--scale=N` forces one). Every hole defined (Grant 15 Nov): holes across each view from the RHS faces (off-centre check), plate holes from the plate edge with the RHS faces in the row, taken from the bottom holes (Grant 15 Nov; SOLIDWORKS refuses that row once, so it is tried twice), top plate: slot centres only (Grant 15 Nov), one size callout per size (THRU / THRU ALL / THRU ONE WALL / WIDE SLOT, from the part's own edges), HDG drainage callout. BOM in alphabetical order of part number, on every assembly (Grant 15 Nov, after James asked for alphabetical), description second; balloons follow. Layout copied from Grant's `08X06-LMW-01-grant.SLDDRW` (view spacing, note block under the BOM, flags bottom left, callout spots) and `08X06-LMW-02-grant.SLDDRW` (member-hole callout right of the face view); every ordinate takes the hole or edge nearest its line, so no extension line crosses the part. All values checked against the model. About 55 s each | Weld symbols stay with James. The arc-centre fallback for ordinates is in but hasn't been triggered in a test yet |
Not a blind test: I looked at James's PLP-06 PDF while picking the plate. His 08XXX-PLP-30 uses chain dimensions instead of ordinate, so his style varies.

## Grant's feedback on PLP-06 (14 Nov) and what changed in v3

| Feedback | Done in v3 |
| --- | --- |
| Scale 1:5 for consistency (50/50 call) | Rule: 1:5 by default; 1:2 or 1:1 only if the plate's longest side would be under 60 mm on the sheet; 1:10 and smaller only if it doesn't fit. Isometric never bigger than the sheet scale |
| James's centre marks | 12.5 mm, gap 2.5, extension lines, centreline font (read from his drawing) |
| Revision table: 0A with a * for work in progress | Row "0A* / date / ORIGINAL DESIGN", REV box shows 0A*. DRAWN/CHECKED/APPROVED left empty. **Open with Admin**: release check needs to know about the * |
| Centre the layout (centre of area in the middle) | Face view + side view in a row, isometric + note block below, rows centred, whole group's centre of area put in the middle of the free sheet (inside the border, above the title block) |

## Next

- Grant's feedback on the 08B04-SFA-2T draft; then try the planner on another assembly to check it generalises.
- BOM / cut-list table templates from copies of an assembly and a weldment drawing.

## Open questions (with Admin)

- Magenta/DRAFT layer mechanism: Admin's proposal, not yet confirmed by Grant (used for now).
- "0A*" WIP revision: what release does with it (today's check wants the top row to equal the release code, so 0A* would be refused until the * is removed); who fills DRAWN.

## Housekeeping

- Empty folder `C:\PDM-draw-test\PLP-06` left behind (SOLIDWORKS held it, then a safety check stopped me deleting a folder at the root of C:). Grant to delete.

## Log (for sw-api-lessons)

| When | What | Lesson |
| --- | --- | --- |
| 14 Nov | `AutoDimension` with the ordinate scheme returned BadOptionValue | Place ordinates by hand: select base edge then the others, `IModelDocExtension.AddOrdinateDimension` |
| 14 Nov | `ISheet.SetScale` before views did nothing; first view insert resets the sheet scale | Set the scale with `ISheet.SetProperties2` after the views are in, then set the face view to use the sheet scale |
| 14 Nov | Template carried James's "0A" revision and old date | Clear `Revision-Sheet1` and `DWG_Date` in the template |
| 14 Nov | `AddHoleCallout2` needs a user click | Use a diameter dimension with prefix "n x Ø" and suffix " THRU", broken leader, horizontal text |
| 14 Nov | **Wrong values**: with centre marks (extension lines on) already placed, ordinates to holes measured to the hole edge (41.3 not 48) | Dimension first, centre marks last; the tool now checks every dimension value against the model and refuses to save on any mismatch |
| 14 Nov | Revision table didn't refresh the title block's REV after `AddRevision` | Set `Revision-Sheet1` too |
| 14 Nov | Opening a copied drawing without its model next to it can load the vault's model (read-only) | Always copy the model next to a copied drawing |
| 14 Nov | Ordinate to a slot centre: a centre mark selected by API won't take an ordinate | Pick the centre mark by position (`SelectByID2 "CENTERMARKSYMS"`); only one ordinate group per view can do that, so the other direction uses the slot's end arc |
| 14 Nov | Value check passed with slot dimensions missing | Expected values now come from the model's holes and slots, not from what was placed |
| 14 Nov | A free-standing end view came out upside down against the face view | Project it from the (turned) face view: true projection, and linked |

## 16 Nov evening: where to resume (written at a battery stop)

- Committed 1d48d2b: member route not all-or-nothing; make-basic adds checked overall sizes. Member regression (4 approved, `scripts\regress-members.tsv`) passed; plate code untouched.
- Grant's ask (now on Fable): look at the drawings he approved (DW7-DW10: 08X04-BRP-02, 08X04-SFP-04, 08B04-SFP-05, 08C04-SFP-04; also DW3, DW6) and get the simple things right. Compare his saved drawings with the machine drafts BY EYE: render PDFs to PNG with PyMuPDF (`python -c "import pymupdf"`) into `scratch\draw-test\look\` and read the images. His PDFs: `look\*-grant.pdf` (3 exported; 08X04-SFP-04, 08B04-SFP-05 and 08X04-PLP-05 still to export with `Draw.exe pdf`). Machine tests: `scratch\draw-test\try-16nov\`.
- Seen already on the machine's 08X04-BRP-02: three CHECK lines run up into the revision table (pack them into two lines at most); ordinate 0/10 text sits on the callout leader.
- Not done: redo of the 11 basic drafts in blocks C/D (DW11-DW23; list `scratch\draw-test\redo-16nov.tsv`, 3 done in `redo-16nov\`, stopped). Tickets DW11-DW25 still open on the Desk with the old drafts.
- SOLIDWORKS lock released. A hidden read-only 08X04-PLP-05.SLDDRW may still be open in SOLIDWORKS (close-mine).

## 16 Nov late evening: general ordinate route; handed SOLIDWORKS to SW (written ~23:00)

- **New: `src\General.cs`** (in make-basic). Grant's recipe from the drawings he dimensioned on the Desk (DW7-DW10): in the face view, ordinates from one end to every flat edge and round-hole centre, both ways (a row at each end of a long view, each with its own zero), one hole-size callout per size. Positions come from the model's flat faces and full cylinders; every value is checked against the model; nothing is saved dangling. On 08C04-SFP-04 it reproduces his bottom and right rows exactly.
- **Looking at drafts:** PDFs rendered to PNG with PyMuPDF and read as pictures (`scripts\try_basic.sh` makes `png\` beside the drafts). Do this for every draft before it goes to the Desk. Grant's six approved drawings as pictures: `scratch\draw-test\look\*-grant.png`.
- **Layout fixes from looking:** CHECK notes two lines at most (PackFlags); balloons close to the part and re-placed after a scale change; a balloon under the part keeps the isometric off the revision table; hole-size text outside the part; make-basic moves the views up before shrinking the isometric (IsoClearOfDims); the face view is checked to show the part's largest side (08A04-HBW-03); rows need a flat edge to start from; blank cut-list rows get a CHECK note.
- **NOT YET DONE (needs SOLIDWORKS, after SW's run):**
  1. Rerun the 11 redo drafts (`scratch\draw-test\redo-16nov.tsv`) with the last fixes: `bash scripts/try_basic.sh <list> scratch/draw-test/redo-16nov`; look at every PNG. 08B04-BRW-01 failed once with RPC_E_DISCONNECTED (rerun to see if it repeats).
  2. **Regression before anything is posted:** plates, brackets and angled (`scripts\regress.sh` with the three lists) and members (`regress-members.tsv`): PlaceBalloons, PlaceIso and the CHECK-note packing are shared and changed tonight, untested on those routes.
  3. Swap the redone drafts into the overnight folders and repost DW11-DW23 (history + version bump).
  4. File the six approved drawings (DW3, DW6, DW7-DW10) to `load\block-B` with `file_draft.sh`, then tell App.
  5. 08B04-BKP-01 after SW makes it a weldment.
- **End caps (Grant 16 Nov):** cut-list rows for end caps are empty because the model's cut-list item has no Description. Grant: fix in the model, "END CAP 72x32x5". SW does it tonight (step 12 of its run) by End cap feature on the vault files; my cross-check list is `scratch\draw-test\endcaps\endcaps.tsv` (92 caps in 48 parts; `scripts\endcap_survey.sh`, `endcap_list.py`).
- SOLIDWORKS was force-closed and restarted once tonight (stuck view-only after a killed PDF export; nothing unsaved). Don't kill Draw.exe or SwCmd mid-call.
- Lock released ~23:00 for SW's run; SW messages Draw when finished.

## 17 Nov early morning: after SW's profiles run (written ~05:00)

- **Filed for the vault:** the six drawings Grant approved (DW3, DW6-DW10) refiled to `scratch\draw-test\load\block-B` against the models as SW corrected them: 0 dangling, every value as Grant saved it (PDF text compared with his). App loaded five. **08C04-SFP-04 (DW8) is held:** James removed that part from the vault on 16 Nov ("renamed and replaced by 08X04-SFP-03"). Tested on a copy (`scratch\draw-test\sfp03-carry`): Grant's drawing pointed at 08X04-SFP-03 opens with 20 dimensions, 0 dangling, all values the same. Admin/Grant to decide whether to file it under the new number. 08X04-SFP-03 missed SW's run (cut list still RHS80x40x4, end-cap row blank, no material).
- **Model copies refreshed:** 76 files in `overnight\APEX` replaced from P:\ (old ones in `scratch\draw-test\overnight-models-before-17nov`).
- **Regression (final build 04:40, commit 34382d8):** 6 plates and 4 members the same as 16 Nov apart from the corrected labels; 4 brackets closer to Grant's approved layouts (isometric at 1:5 on all). Outputs: `regress-17nov-c`, `regress-17nov-members2`. Compared by `scripts\pdf_diff.py` (text, positions, ink) and `scripts\overlap_check.py` (text on text, lines through text), and looked at as pictures.
- **Fixed in the shared layout code tonight:** "0 ROW(S) WITHOUT A DESCRIPTION" note; isometric shrinking over a few mm (now: close the gap between the two views, or nudge the views up 15 mm at most); a balloon pushed onto the part; the note block jumping 17-36 mm between runs (knife-edge at TitleLeft, 1 mm tolerance); make-basic's first CHECK note said "edges and round holes dimensioned" when only a hole size was placed; make-basic went to 1:10 too early for tall views.
- **Desk:** DW11-DW15, DW17, DW19-DW23 reposted as version 2 (11 redone drafts, old files in `overnight-old-drafts-16nov`); DW26 new (08B04-BKP-01, now a weldment, bracket draft); DW7-DW10 set done. DW16, DW18, DW24, DW25 unchanged and still open.
- **Known weak spots, not fixed:** sloping members (08B04-SFP-03, 08X06-SFP-03) get end-view sizes only; 08X04-BKP-07's model is skewed to the standard views (needs a view square to the plate); the two small assemblies (08A04-HBW-06, 08B04-SFP-01) get no BOM or dimensions (make-assembly crashes in MemberHoles / dimensions a slot end); 08C04-HBW-01 is small at 1:10; hole-size leader crosses the top ordinate row on 08X06-SFW-02/-03; make-section crashes ("Sequence contains no elements") on a member that isn't along a model axis, and make-plate saves a nearly empty drawing for such a part if it is tried (try_routes.sh did; the production flow doesn't).
- SOLIDWORKS lock released ~05:00, 0 documents open.

## 17 Nov morning: DW8 filed under the new part number

- **Grant (in Admin, 17 Nov): yes**, file his approved DW8 drawing as `08X04-SFP-03.SLDDRW` in `_Shared`. Done: his saved drawing copied to `scratch\draw-test\load\block-B2\APEX\SC08\LS-08X-04\_Shared\`, repointed to `P:\APEX\SC08\LS-08X-04\_Shared\08X04-SFP-03.SLDPRT`, nothing else changed. Check: 20 dimensions, 0 dangling; PDF text against his filed PDF differs only in the part number and the finish (HDG & PAINTED, from the renamed part); no word moved. Cut list still RHS80x40x4 with a blank end-cap row until the part is corrected with the 27 (expected).
- The old `08C04-SFP-04.SLDDRW` and PDF moved out of `load\block-B` to `scratch\draw-test\load-superseded\block-B\ShaftC` so they can't be loaded by mistake.
- **Grant decided (A):** the "CHECK: MODEL HAS NO MATERIAL PROPERTY" notes stay on the four loaded block-B drawings; James deletes them as he works. Nothing for Draw to do.
- DW8 closed on the Desk with a note about the rename. Lock released, 0 documents open.
- App loaded block-B2 on 17 Nov: 08X04-SFP-03.SLDDRW is checked in at _Shared. All six of Grant's approved drawings are now in the vault.

## 17 Nov morning: working on Grant's open drawing (DW26, 08B04-BKP-01)

- Grant asked for tweaks on the drawing he had open, and for a way to do that instead of rebuilding from scratch. New: `src/Live.cs`, command `Draw.exe live` (info, slots, remove, part-slots). It runs under Grant's Desk lock, never opens, closes or saves. Notes in the drawing-standards skill.
- Done on his new projected view (Drawing View5): right-hand ordinates 0, 12, 112 and the note "2 x 13.0 X 23 THRU", on DRAFT. Grant added the 70 to the slot centre himself (Add To Ordinate, then Select Midpoint on a straight side).
- Not solved by program: a slot-centre mark, or a midpoint pick, for slots that arrive with an inserted part (the angles here). It took about 25 minutes of trying on his open drawing, which was too long; next time hand it back after two refusals.
- Slot in the part checked: 13 x 23, half-round ends r 6.5, centres 115 and 285 across, 70 up. Plate holes are 13.5 dia, slots 13 wide: for James to confirm.
- Open: the midpoint pick by program; `live` tools for ordinates on any view, balloons and layout moves; tests of `live` on a copy.

## 17 Nov late morning: Grant reviewed DW11-DW26

- Accepted and in the vault (load folder block-C, loaded by App): DW11, 13, 15, 16, 17, 20, 21, 22, 23, 24, 25, 26. DW18 (08C04-SFP-03) filed as Grant saved it in block-C2, with App.
- DW16 (08BXX-MSW-01) has no cut list: the model is not a weldment. Admin asked to have SW convert it; then add the cut list (Draw.exe live cutlist).
- Problem, DW12 (08A04-HBW-06): "This part should have weldment pieces (we did a previous one)". Problem, DW14 (08B04-SFP-01): "Same as DW12 - this is a welded assembly I think - check 08C04-SFP-03 - should be the same". Not started; waiting for Grant's go.
- Problem, DW19 (08X04-BKP-07): "This one is on a strange angle - I think we need to re-orientate the part first and then redo". With Admin for SW.
- Draw got this wrong: it held DW18 back and began writing a balloon fix for a drawing Grant had accepted. Grant stopped it. Rule now in the skill: his edited or accepted drawings are filed as saved; checks are reported, not acted on.
- New commands: Draw.exe live (info, slots, cutlist, remove, part-slots) and fix-balloons (tested on a copy only; not for Grant's accepted drawings).
- DW12 and DW14: redone from Grant's finished 08C04-SFP-03 drawing pointed at each sister assembly (DmEdit replace-ref on a copy: 21 dimensions, 0 dangling). Grant tidied and accepted both; filed as saved in block-C3, with App. My own make-assembly route gave a poor layout on these and crashed SOLIDWORKS once in MemberHoles (now behind --no-member-holes).
- Lesson: for a sister of a drawing Grant has finished, copy his drawing and repoint it before trying a route.
- App loaded block-C2 and block-C3 on 17 Nov: all 15 drawings Grant accepted from DW11-DW26 are in the vault. Open: DW19 (SW re-orients 08X04-BKP-07, then redraw) and the DW16 cut list (SW makes 08BXX-MSW-01 a weldment).

## 17 Nov afternoon: where things stand at shutdown

Nothing of Draw's is running or open in SOLIDWORKS; the lock is not Draw's. All Draw code and notes are committed.

| Item | State | Waits for |
| --- | --- | --- |
| DW1-DW15, DW17, DW18, DW20-DW26 | Done, in the vault as Grant saved them | Nothing |
| DW16 (08BXX-MSW-01) | In the vault without a cut list | Grant's choice in Admin after SW's rehearsal, then Admin's brief |
| DW19 (08X04-BKP-07) | Problem; Grant chose the saved-view route | James (he has the part checked out) |

- **DW16 route, agreed with Admin:** once SW has checked in 08BXX-MSW-01 as a weldment, copy the part from P: to scratch, take Grant's accepted drawing from `scratch\draw-test\load\block-C`, add only the cut list and its balloons (DRAFT layer, `Draw.exe live cutlist` or the same code on a copy), check (0 dangling, his dimensions unchanged, PDF compared), post as a new version of DW16 saying only those two things were added. After Grant presses Done, hand to App: the drawing is already in the vault, so App takes it in by check-out and check-in as a new version, not a new-file load.
- **New from SW's rehearsal (commit 42c00ea):** the released `08BXX-MSA-01.SLDDRW` parts list has its PART NUMBER and DESCRIPTION columns crossed, and its part number goes blank when 08BXX-MSW-01 becomes a weldment. If Grant picks option B, Draw fixes that parts list first. Wait for Admin's brief; don't start before it.
- **DW19:** a trial of drawing from a named view on a scratch copy is allowed when SOLIDWORKS is free; nothing is to be posted until James has re-oriented or released the part.
- **Session names after restart:** admin, dev, sw, app, draw. Message "admin".
- **Not asked for, noted only:** a midpoint pick by program for slots from inserted parts; tests of `live` on copies (`--centre=midpoint` does not work); more `live` tools.

## 17-18 Nov night: Admin's overnight queue

- **Item 1, 08BXX-MSA-01 parts list: no change made, none needed.** The table has 5 columns with PART NUMBER hidden: ITEM NO. | QTY. | (hidden) PART NUMBER | DESCRIPTION | PartNo. The 0A PDF prints "DESCRIPTION | PartNo". SwBomDiag read 4 columns and the custom-property indexes skip the hidden one, which made the columns look crossed; they are not. With SW's rehearsal weldment held open only the hidden part-number cell of row 1 goes blank; every printed cell is unchanged; 12 dimensions, 0 dangling. Vault file untouched. Told Admin and SW; lock released.
- My first try (set the columns "like 08B11-MSA-01") blanked DESCRIPTION on the scratch copy; discarded (`scratch/draw-test/msa01-17nov/try1-blank`). `Draw.exe bom-columns` is now look-only (`--hold=<part>`, `--set=col:prop` trials; never saves) and shows hidden columns.
- For Grant: the two sister drawings print different lists. 08BXX-MSA-01: "DESCRIPTION | PartNo", plate row 08BXX-PLP-01. 08B11-MSA-01: "PART NUMBER | DESCRIPTION", plate row 08BXX-MSW-01. Making them alike changes a released PDF; not touched.
- Note: SwBomDiag starts a hidden SOLIDWORKS when none is running; Draw.exe can't attach to that one. Start SOLIDWORKS with Draw.exe or SwCmd first.

## Learned from James (18 Nov, for Grant to review; nothing goes into the generator without his OK)

Read-only comparison of the 8 drawings James checked in on 17 Nov (22:38 NZ) that began as Draw first passes. Files: `scratch\draw-test\james-compare` (dump, PDF and PNG of each version; James's copies from App in `scratch\draw-test\james-17nov`).

Limits: James's drawings were opened against the older models on this PC, so a changed VALUE may be a model change, and many of his dimensions came out white in my PDFs (read from the dumps instead). 08C04-SFA-01 opened with views off the sheet, so its layout was not judged. 08A04-SFA-5B is on the list of files hit by the accidental load of 18 Nov 00:07; the copy looks like James's work (DRAFT layer empty, welds added) but is unconfirmed. 08X04-BKP-06 looks unfinished (CHECK note left, still 0A*).

| Drawing | Scale | Views | Dimensions (draft -> James) | Other |
| --- | --- | --- | --- | --- |
| 08A04-SFA-5B | 1:30 -> 1:25 | Base Front -> Back; bottom plan deleted; isometric from a named view at sheet scale | 26 -> 21: elevation row 4 -> 9 stops, moved left; horizontal row and overall 639 deleted | 4 weld symbols; balloons 7 -> 12, one per instance beside the part; CHECK deleted; BOM off DRAFT |
| 08B04-SFA-5B, 08C04-SFA-5B | 1:30 -> 1:25 | As above, plus a second isometric (named view) | 28 -> 23 / 22: as above, plus linear 354, 460, 279, 80 inside the views | 4 weld symbols; 12 balloons; CHECK deleted though BOM row 1 is still blank (balloon reads "?"); 08B04 BOM still on DRAFT |
| 08C04-SFA-01 | 1:15 -> 1:10 | Looks rebuilt: base is the right end view, dimetric pictorial | 25 -> 25: three ordinate runs plus linear 210, 210, 374 | 3 weld symbols incl. a stitch weld; balloons 5 -> 11 |
| 08C04-HBP-01, -02 | 1:2 | Same three views, moved up | 7 -> 4: side-view ordinates deleted, one plain linear 210; end view 40, 40, 4 kept | CHECK (RHS vs SHS) deleted |
| 08X04-BKP-06 | 1:5 -> 1:8 | Plate face flipped; 2 auxiliary views added (one with hidden lines); pictorial from a named view | 14 -> 34: plate ordinates kept with 90, 210, 103.7 added; side-view 450.3, 103.7, 246.8 deleted, ordinates along the tube axis added | "2 x dia 22 THRU ALL" added; cut list off DRAFT and widened |
| 08X04-PLP-06 | 1:1 | Third view deleted; base changed to the hole face; hidden lines on the profile | 14 -> 7: no ordinates; 50, 25, 25 and 50, 50, 5 | Hole callout: "THRU" removed; centre mark and centreline added; "Finish: Refer Assembly" line removed; CHECK deleted |

Patterns seen in two or more drawings (observations, not rules):

1. Small subframes go one scale step bigger (1:30 -> 1:25, 1:15 -> 1:10).
2. A view the draft added is deleted: bottom plan on the three SFA-5B, third view on PLP-06.
3. On simple parts ordinates are replaced by a few plain linear dimensions (HBP-01, HBP-02, PLP-06) and there are fewer of them. Ordinates stay on assemblies and on the BKP-06 plate, with more stops (4 -> 9 on the SFA-5B elevations).
4. On the SFA-5B the horizontal ordinate row and the overall 639 are deleted.
5. The main view is flipped to another face on 6 of 8.
6. Weld symbols with TYP are added by hand on all four SFA (Draw adds none).
7. Balloons: one per instance, beside the part, not in rows (four SFA).
8. Isometrics at sheet scale, with the "Isometric scale" line removed; a second isometric on two; named views used on four.
9. The note block is moved to sit above or left of the isometric on all 8; dimension runs are moved to the other side of the view on 7.
10. CHECK notes are deleted in 7 of 8, in three cases without the thing they flag being changed in the drawing (blank BOM row 1 on 08B04/08C04-SFA-5B, title on PLP-06).
11. Kept items are taken off DRAFT (6 drawings at zero); 0A* becomes 0A in 7 of 8.

Questions this raises for Grant (not acted on): whether Draw should drop the bottom plan on subframes; use linear dimensions on simple plates and short members; pick the next bigger scale; place balloons per instance; and whether a CHECK note that James deletes without a fix should go to a list for Grant instead of the sheet.
