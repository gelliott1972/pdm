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
