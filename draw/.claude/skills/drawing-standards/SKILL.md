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
| No Material property | 08X04-BKP-01 |
| Weight property far from the part's weight in steel | 08X04-BKP-01: 0.15 kg vs 1.2 kg |
