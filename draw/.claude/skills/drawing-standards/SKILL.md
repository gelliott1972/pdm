---
name: drawing-standards
description: How Draw's machine drafts should look (James's Unilumin sheet, Grant's feedback 14 Nov 2026). Use when making or changing a plate or member draft with draw\src\Draw.exe.
---

# Drawing standards (from Grant's feedback and James's drawings)

Built up from feedback, one line per rule, with where it came from. A rule about one drawing is not a rule for all until Grant says so.

## Sheet

| Rule | Source |
| --- | --- |
| James's Unilumin A3 template, extracted from a copy of his GNRIC-PLP-06 (`scratch\draw-test\_Templates`) | Brief draw-1 step 0 |
| Revision row "0A* / date / ORIGINAL DESIGN", REV box 0A*; DRAWN/CHECKED/APPROVED empty | Grant 14 Nov (D5 with Admin) |
| Everything the machine places is on layer DRAFT (magenta) until accepted (moved to -None-) | Brief D3 |

## Layout

| Rule | Source |
| --- | --- |
| Learned, not fixed: `learn-layout` measures hand-laid-out drawings as proportions; `layout-examples.json` holds them | Grant 14 Nov: "use my drawings to get the feel" |
| Face view and its side/end view linked (projected), centred on the sheet | Grant, 08X04-BKP-01-grant |
| Isometric with its note block in the free corner bottom left (left of the title block) | Grant, 08X04-BKP-01-grant |
| CHECK flags in a free corner (top left learned) | Grant, 08X04-BKP-01-grant |

## Scale

| Rule | Source |
| --- | --- |
| 1:5 by default for consistency; bigger (1:2, 1:1) only if the part would be under 60 mm on the sheet; smaller if it doesn't fit | Grant 14 Nov ("50/50 call") |
| Isometric at the same scale as the other views; if it doesn't fit, the sheet scale steps down | Grant 14 Nov (bracket isometric too small) |

## Dimensions

| Rule | Source |
| --- | --- |
| Plates: ordinate from the top-left corner, across the top and down the right | James, GNRIC-PLP-06 |
| Each ordinate attaches to the hole nearest its dimension line (no extension line across other holes) | Grant 14 Nov |
| Members: ordinates along the member from one end, across the face from the heel; length as a reference (in brackets) | James, GNRIC-BRP-10; Grant to confirm with James |
| End view: section sizes and wall thickness | James, GNRIC-BRP-10 |
| Dimension before centre marks (marks first made holes measure to their edge) | 14 Nov bug |
| Every value checked against the model; nothing saved on a mismatch or anything dangling | Brief D1, 14 Nov |

## Callouts and marks

| Rule | Source |
| --- | --- |
| Holes: "n x Ø d THRU" as a diameter dimension (stays tied to the hole) | James, GNRIC-PLP-06 |
| Slots: "n x W X L THRU" note, arrow to the top of the slot | James, 08XXX-PLP-30; Grant 14 Nov |
| Centre marks as James's: 12.5 mm, gap 2.5, extension lines, centreline font | Grant 14 Nov, read from James's PLP-06 |

## Flags (magenta CHECK notes)

| Flag | Why |
| --- | --- |
| Description length (L=nnn) differs from the model | 08X04-BKP-01: L=150 vs 100 |
| Section in the description (RHS/SHS and size) differs from the section measured in the model | GNRIC-LMW-01: RHS 40x40x4 vs SHS 40x40x4 (Grant / Admin 15 Nov) |
| No Material property | 08X04-BKP-01 |
| Weight property far from the part's weight in steel | 08X04-BKP-01: 0.15 kg vs 1.2 kg |

## Assemblies (Grant 14 Nov, 08B04-SFA-2T)

| Rule | Source |
| --- | --- |
| Plan the sheet from the model's size before drawing; draw each view once | Grant ("algorithmic, repeatable") |
| Isometric (front, upright, never on its side) centred under the BOM, from the left border to about grid 5.5, at the largest scale that fits with balloon room | Grant |
| Balloons in columns either side of the isometric and a row on top, level with their parts, in order (no crossing, not over the part) | Grant's hand layout |
| Front with top above, bottom below, left and right sides; largest scale that fits the rest with room for dimensions; block centred | Grant |
| Back isometric only if it fits (option B) | Grant |
| BOM: James's template, top left; the configuration must be named or it comes out empty | James; 14 Nov bug |
| BOM rows in alphabetical order of part number (description second), items renumbered, balloons follow; every assembly. Sort again before saving (a rebuild puts it back) | James / Grant 15 Nov |
| Dimensions: ordinates to each horizontal member (up the side) and each upright (across the top) from the bottom-left, overall in brackets; checked against the model | Grant / James's 08A04-SFA-2T |
| Member dims to the underside / left side for now: CHECK flag until James says (centreline?) | open |
| Measure assembly views from the model's visible parts, not the view outline (counts suppressed parts) | 14 Nov |

## LED members / long members (Grant signed off 15 Nov, 08X06-LMW-02 and -01)

Layout copied from Grant's `08X06-LMW-01-grant` and `08X06-LMW-02-grant` (in `scratch\draw-test\LMW\...\_Shared`).

| Rule | Source |
| --- | --- |
| Isometric, front (face), side (plates face-on), end view of the top plate; zig-zag breaks only as long as needed; largest scale whose breaks cut out at most a third | Grant 14 Nov |
| Views spaced as in Grant's file (face 49 mm in from the zone, side 49.5 mm right, end view 77.7 mm further); note block under the BOM; CHECK flags bottom left | Grant's -01 file |
| Plates and plate holes as ordinates down the side view, plate edges in brackets; overall length (top plate to bottom, e.g. 2155) 8 mm left of the face view; the RHS length is in the BOM | Grant 14-15 Nov, -02 file |
| Every hole defined: across each view from the RHS faces (off-centre check against the model), plate holes from the plate edge with the RHS faces in the row | Grant 15 Nov |
| Each ordinate takes the round hole or edge nearest its line, so no extension line crosses the part; across rows from the bottom holes, labels about 15 mm under the member | Grant 15 Nov |
| Top plate (end view): slot centres both ways from the bottom-left corner, plate size in brackets; HDG drainage hole: its own callout only | Grant 15 Nov |
| One size callout per hole size: "n x Ø d THRU" (plates), "THRU ALL" / "THRU ONE WALL" (member, from the part's own edges), "n x w WIDE SLOT THRU". Member holes: right of the face view; plate holes: right of the side view; slots: centred under the end view | Grant's files |

## All drafts (Grant 15 Nov)

| Rule | Source |
| --- | --- |
| **Slots: located by their centres only, in both directions, on every drawing.** Their size comes only from the slot callout ("n x w WIDE SLOT THRU"). Never dimension to a slot's end, arc, side or edge. Every save checks the model faces behind each dimension and refuses a drawing with one (as strict as dangling). If SOLIDWORKS won't take a slot centre in a row, the slot is left undimensioned with a magenta CHECK, never put on its end. (15 Nov evening: the "one row per centre mark" limit was SOLIDWORKS staying in add-to-this-ordinate mode after a row; ending it after every row (clear selection, rebuild) lets both rows use the same slot's centre mark: 08X04-PLP-32) | Grant 15 Nov ("they should only have centres") |
| A dimension too narrow for its text (under 6 mm on the sheet): text outside the extension lines, beside the outer one, arrows outside pointing in | Grant 15 Nov (GNRIC-LMW-01 wall "4") |
| Stock with the material on the note block: "Plain Carbon Steel, SHS 40x40x4", built from the section measured in the model (hollow sections so far); magenta CHECK if the cut-list label or the description disagrees | Grant D7, 15 Nov |
| Revision 0A* on every draft; James removes the * and fills DRAWN when he issues (release refuses * or empty DRAWN) | Grant D5 = A, 15 Nov |
| Label every view that shows a different body (or hides one): e.g. "END CAP 36x36x5" under the cap view, "SHS 40x40x4 (END CAP NOT SHOWN)" under the section view | Grant D9, 15 Nov (the cap's 36 was read as the tube's inside) |
| No inside size on a tube's section view (outside and wall only) | Grant 15 Nov, DR12 redo ("the 32 dimension is in the middle of the part, there is no need for it") |
| Each new version of a draft gets its own PDF ("GNRIC-LMW-01 v2.pdf"); the Desk ticket points at the newest | Desk draft-review tickets, 15 Nov |

## Welded members with end caps (GNRIC-LMW, batch 1)

| Rule | Source |
| --- | --- |
| The member is the longest body; end caps count in the overall length and weight | batch 1 |
| Upright: face view on the side with the holes; open-end view with the section (the tube alone, caps hidden); cap-end view with the cap size and "Ø10 for HDG Drainage" ("2 x ... one in each end cap" when both ends are capped); each end view level with its end | James's GNRIC-LWP-20, Grant's 08X06-LMW |
| Length from the top: 0 ... (overall) in brackets; across row 0 / 40 under the member, to the tube's outer edges | 08X06-LMW |
| End views no smaller than 1:5, labelled "SCALE 1:5" when the sheet is smaller | batch 1 (1:20 members) |
| Hole in a tube: "Ø10 THRU ONE WALL" / "THRU ALL", beside the hole on the left | 08X06-LMW rule |

## Learned from Grant's edits (forward only: never applied to drawings already accepted or saved)

How it works (Grant D12, 15 Nov): one drawing at a time. Draw generates it and opens it; Grant tweaks it by hand or by asking (Draw edits that open drawing, never regenerates); Grant says "good"; Draw checks, saves, exports the PDF, moves it to the load folder and never touches it again. After each one, Draw compares its generated version with Grant's saved one and writes down what he changed here.

| What Grant changed | Rule for future drawings | From |
| --- | --- | --- |
| Moved the CHECK note from above the title block to outside the drawing border, bottom left of the sheet (x 3.4, y 5.4 mm from the sheet corner, one line) | **In the generator:** CHECK notes for James outside the border, bottom left of the sheet (brief draw-3) | GNRIC-LMW-02, 15 Nov |
| Moved the isometric (with its note block) up 16 mm and left 12 mm on GNRIC-LMW-02; on GNRIC-LMW-01 said "the isometric should be vertically center aligned to the main 2D view" | **In the generator:** the isometric's vertical centre level with the main view's (its left-right place as before) | GNRIC-LMW-02, -01, 15 Nov (Grant agreed) |
| On GNRIC-LMW-04 (caps at both ends): "if it has endcaps both end, the top and bottom views should be the same - I also think it needs to be clearer on the drawings" | **In the generator:** caps at both ends: two matching cap views, top end at the top and bottom end at the bottom, each labelled "END CAP 36x36x5, TOP END" / "BOTTOM END" with its own cap size and "Ø10 for HDG Drainage" (no "2 x"); no tube-only section view (the stock SHS 40x40x4 on the Material line is taken from the tube's own walls); drainage callouts placed last, above left of the top view and left of the bottom view | GNRIC-LMW-04, 15 Nov |
| Confirmed the side hole on GNRIC-LMW-06 (Ø10 through one wall, 10 from the bottom) is right: "an additional drainage hole" | He then added "for HDG Drainage." under the callout on -06 himself. **In the generator:** a hole through one wall of these tubes is called out "Ø10 THRU ONE WALL" over "for HDG Drainage", left of the member clear of the part ("make sure the text doesn't collide with any part - there is room") | GNRIC-LMW-06, 15 Nov |
| On 08X04-PLP-02: "where is the material?" (the plate note block had no Material line, copied from James's plate version) | **In the generator:** plates' note block has a Material line under Config name, as the members' | 08X04-PLP-02, 15 Nov |
| On 08X04-PLP-02 moved the face and edge views up 7 mm and the isometric (with its note block) up 17 mm, off the revision table | Not in the generator yet (one plate): offer as a rule if he does it again | 08X04-PLP-02, 15 Nov |
| On 08X04-PLP-32: "have the slot description point to the closest slot" | **In the generator:** the slot callout's leader goes to the slot nearest the callout text, at that slot's nearer end | 08X04-PLP-32, 15 Nov |
| On 08X04-PLP-32 put the isometric back to 1:2 (the sheet scale; I had shrunk it to 1:5 to keep it inside the border) and moved it right, inside the border, with its note block | **In the generator (plates):** keep the isometric at the sheet scale where it fits; move it right to stay inside the border rather than shrink it | 08X04-PLP-32, 15 Nov |
| On 08X04-BKP-02 (first welded bracket) moved the face view 50 mm left, the side view 29 mm left (face-to-side gap 21 mm wider), the isometric and note block 52 mm down into the bottom-left corner above the revision table, and balloon 1 in off the left border; "can't lock the BOM into the top LH corner" | **In the generator:** the cut-list table attached to the sheet's cut-list anchor at the border corner (12.5, 284.5; the template's anchors sit at 20, 287). View moves: not yet (one drawing); offer as a rule if he does it again on -03 | 08X04-BKP-02, 16 Nov |

## Welded brackets: plate with welded arm (item 5, Grant agreed the content 16 Nov)

| Rule | Source |
| --- | --- |
| make-plate with a multi-body part: the plate dimensioned as before; each welded body's outer sides seen on the plate face join the plate's two ordinate rows; its stand-off from the plate face on the side view; thickness from the plate's own edges | Grant 16 Nov (proposal agreed) |
| James's cut-list table (ITEM NO. / QTY. / DESCRIPTION / LENGTH, from his 08X04-HBW-07) top left, attached to the corner anchor; balloons on the isometric, outward from its centre, never under it | James's welded drawings; Grant 16 Nov |
| CHECK notes: "WELDS NOT ADDED"; cut-list label vs the tube measured in the model; a plate row without a description (the cell can't be typed into; SW adds the Description) | Grant 16 Nov |
| More than two CHECK lines: two to a line along the bottom, outside the border | 08X04-BKP-02, 16 Nov |
| Every part has a Material (SW fixes models overnight); until then the CHECK note stays | Grant 16 Nov |

## Any part the full routes can't finish: Grant's recipe (16 Nov, from the drawings he dimensioned himself, DW7-DW10)

Grant: "the dimensioning is quite straightforward and I'm not sure why you didn't try". His own drawings of 08X04-BRP-02, 08X04-SFP-04, 08B04-SFP-05 and 08C04-SFP-04 (pictures in `scratch\draw-test\look\*-grant.png`) all follow one recipe that doesn't depend on the part type. `make-basic` now does it (`src\General.cs`).

| Rule | Source |
| --- | --- |
| Face view: ordinates from one end to every flat edge and every round-hole centre, across and up. A long view gets a row at each end, each with its own zero | Grant's 08C04-SFP-04 (bottom row 0 ... 356, right row 0 / 5 / 25 / 106) |
| One hole-size dimension per hole size ("4 x Ø11"), text outside the part on the side the holes are nearer, beyond any ordinate row there | Grant's 08C04-SFP-04, 08X04-SFP-04 |
| End or side view: the overall sizes; the tube wall when the cut list names an RHS/SHS a x b x t and the view shows it | Grant's 08X04-BRP-02 (40, 40, 4) |
| A row starts from a flat edge and holds more than hole centres (no hole-to-hole rows) | 08X04-BKP-07 draft, 16 Nov |
| Positions come from the model (flat faces square to the sheet, full cylinders along the view); a position is dimensioned only where the view draws an edge or circle there; every value is checked against the model and a wrong one is removed with a CHECK note, not the whole drawing refused | Grant 16 Nov ("you often get really close but something gets missed at the last hurdle") |
| The face view must show the part's two largest sizes; if the named view doesn't, try the others | 08A04-HBW-03 ("*Front" showed 184 x 40) |
| Slots, welds and angled features are left for James, said in the first CHECK note | 16 Nov |
| A feature the face view can't show (hole in the far wall) gets a CHECK note; the rest of the drawing is still made | 08X04-BRP-02 |

## Look at every draft as a picture (16 Nov)

Number checks miss layout faults. Before a draft goes to the Desk: render the PDF to PNG (PyMuPDF, `scripts\try_basic.sh` does it) and read the picture. Compare with Grant's approved drawing of the same or a similar part. Found this way on 16 Nov: CHECK lines running into the revision table, a balloon on the revision table, hole text over the part, a leader across the whole part, a wrong face view, a row placed off the view.

| Rule | Source |
| --- | --- |
| CHECK notes: two lines at most, outside the border bottom left (several notes share a line) | 08X04-BRP-02 draft, 16 Nov |
| Balloons close to the isometric (first clear spot stepping out from the part), placed again after a change of scale, never on the revision table | 16 Nov drafts |
| Room on the sheet is used before anything is shrunk: the views move up before the isometric gets smaller | Grant 16 Nov ("a layout tweak is all that is needed as there is plenty of room on the page") |
| A cut-list row with no description gets "CHECK: CUT LIST ITEM n HAS NO DESCRIPTION IN THE MODEL"; the fix is in the model (end caps: "END CAP 72x32x5", done by SW 17 Nov) | Grant 16 Nov |

## Working on the drawing Grant has open (`Draw.exe live`, 17 Nov)

Grant, 17 Nov: "a mechanism for me to ask you what to do with what I have open might be a great idea - at the moment rebuilding from scratch is slowing us down a lot". And, after a slot took 25 minutes: "this is taking too long for something SO SIMPLE".

- `Draw.exe live info [--all] [--view=NAME]` reads the active drawing and the selected view (edges, slots, holes, every annotation with its layer and place). Read-only. Run it first, every time.
- `Draw.exe live slots [--edges=12,112] [--side=left] [--no-marks] [--no-rows] [--no-callout]` adds to the selected view: slot centre marks, a row of vertical ordinates beside it (zero at the bottom edge), one "n x W X L THRU" note under the view. Only what it adds goes on DRAFT. `live remove --names=D7,D8` takes back DRAFT items by name. `live part-slots` shows how the slots are made in the part.
- It runs under Grant's own Desk lock (he asked in the Draw window), never opens, closes or saves, and only on a drawing inside the test folder. Tell Grant not to click while a step runs.
- **Be quick or hand it back.** If SOLIDWORKS refuses something twice, stop experimenting on his open drawing: say what is in, what is not, and give him the hand steps. Experiments belong on a copy, later.
- **Slots that arrive with an inserted part** (08B04-BKP-01: the angles are "Stock" features, the slots come in with them): SOLIDWORKS makes no slot-centre mark for them. The automatic insert says yes and adds nothing; by hand, "Slot Centers" marks the two ends. A row that picks such a mark reads the slot END (75, not 70): the value check catches it. What works, by hand: right-click the row's 0 > Add To Ordinate, right-click a straight side of the slot > Select Midpoint. By program the midpoint pick is not solved yet (SelectByRay with swSelMIDPOINTS selected nothing).
- Which edges go in a row: Grant's own row on that view was plate bottom, plate top, slot centre, top of the angle (0, 12, 70, 112). He left out the angle's leg thickness and the fillet tangent lines. That is one drawing, not a rule.

## A drawing Grant has edited or accepted is his (17 Nov)

Grant, 17 Nov, when Draw started re-attaching a balloon on a drawing he had pressed Done on: "Why are you trying to fix a document I have edited?" and "You are getting stuck in loops!"

- Once Grant has edited a drawing or pressed Done, Draw changes nothing on it. File it exactly as saved.
- If a check flags something on it (dangling, slot check, overlap), tell Grant in one line and file it as it is unless he says otherwise. Do not hold it back and do not write a fix.
- That day's case: a balloon he placed by hand to point at a hidden end cap counted as dangling. He fixed it himself by adding a cropped isometric of the other end.
- More widely: when a small thing fails twice, stop and say so. Do not build a tool for a one-off while Grant waits.

## Learned from James's finished drawings (Grant's yes 20 Nov, through Admin)

From comparing 8 drafts with what James checked in on 17 Nov (`draw\status.md`, "Learned from James"). Grant: they apply to new drafts of the drawing types where James did them. They are not a rule for every drawing. `--classic` on any make command turns them off.

| Rule | Applies to | In the generator | Source |
| --- | --- | --- | --- |
| No bottom plan view | Small subframes (SFA) | make-assembly, part number with -SFA-, not a long member | James, 08A04/08B04/08C04-SFA-5B |
| One scale step bigger (1:30 -> 1:25, 1:15 -> 1:10) | Small subframes (SFA) | Comes from the room the bottom plan gave back: the largest of James's scales that fits. Not forced past what fits | James, SFA-5B, 08C04-SFA-01 |
| Balloons: one per instance, beside its part, not in rows or columns | Small subframes (SFA) | make-assembly: a balloon on every instance, each at the first clear spot out from its part | James, the four SFA |
| A few plain linear dimensions instead of ordinates: the length; a single hole from the nearer end and from the heel; section sizes on the end view | Simple plates and short members | make-section: 300 mm or shorter, no slots, one hole position at most. Longer or busier members keep ordinates | James, 08C04-HBP-01, -02, 08X04-PLP-06 |
| Ordinates stay | Assemblies, and plates like the BKP plates | Unchanged (make-plate, make-basic, make-assembly) | James, 08X04-BKP-06, SFA-5B |
| CHECK items go on a list for Grant on the Desk ticket, not as a note on the sheet | The drawings above | The CHECK lines are written to `<drawing>.checks.txt` beside the drawing and taken off the sheet; put them in the ticket body when posting | James deleted the CHECK note on 7 of 8 |

Not agreed, so not done: flipping the main view to another face, weld symbols, isometric at sheet scale or a second isometric, moving the note block, taking items off DRAFT.
