# 0A PDFs with white items (10 Nov 2026)

Scanned all 240 PDFs in `P:\APEX\SC08\_Review\LS-08X-06_0A` and `LS-08X-11_0A` (read-only; PyMuPDF). The scan counts text and lines drawn pure white on plain paper, so they can't be seen. Nothing white sits on a dark fill, so none of these is white on purpose.

There are 41 drawings: 35 have white text and lines, and 6 (GNRIC-BRP-10 to 15) have only two short white lines each. That makes 69 PDFs, because many drawings are in both packages.

| Drawing | In | White text | White lines |
| --- | --- | --- | --- |
| 08A06-STA-01 | 06 | 16 | 32 |
| 08A11-STA-01 | 11 | 36 | 72 |
| 08B06-HBW-03 | 06 | 8 | 15 |
| 08B06-STA-01 | 06 | 17 | 34 |
| 08B11-STA-01 | 11 | 15 | 30 |
| 08BXX-HBW-01 | 11 | 8 | 30 |
| 08BXX-MSP-01 | 06, 11 | 2 | 14 |
| 08BXX-PLP-01 | 06, 11 | 4 | 42 |
| 08C06-HBW-03 | 06 | 8 | 15 |
| 08C06-STA-01 | 06 | 16 | 32 |
| 08C11-STA-01 | 11 | 16 | 32 |
| 08X06-DBP-01 | 06 | 7 | 9 |
| 08X06-DBP-02 | 06 | 7 | 9 |
| 08X06-LMW-03 | 06 | 8 | 16 |
| 08X06-LPP-01 | 06 | 4 | 43 |
| 08XXX-PLP-30 | 06, 11 | 4 | 9 |
| 08XXX-PLP-31 | 06, 11 | 4 | 9 |
| 08XXX-PLP-32 | 06, 11 | 4 | 9 |
| 08XXX-PLP-33 | 06, 11 | 4 | 9 |
| GNRIC-BRP-10 | 06, 11 | 0 | 2 |
| GNRIC-BRP-11 | 06, 11 | 0 | 2 |
| GNRIC-BRP-12 | 06, 11 | 0 | 2 |
| GNRIC-BRP-13 | 06, 11 | 0 | 2 |
| GNRIC-BRP-14 | 06, 11 | 0 | 2 |
| GNRIC-BRP-15 | 06, 11 | 0 | 2 |
| GNRIC-BRW-16 | 06, 11 | 8 | 27 |
| GNRIC-LPP-01 | 06, 11 | 1 | 3 |
| GNRIC-LPP-02 | 06, 11 | 5 | 31 |
| GNRIC-LPP-03 | 06, 11 | 4 | 19 |
| GNRIC-LPP-04 | 06, 11 | 1 | 3 |
| GNRIC-LPP-05 | 06, 11 | 2 | 5 |
| GNRIC-LPP-06 | 06, 11 | 4 | 12 |
| GNRIC-LPP-07 | 06, 11 | 5 | 23 |
| GNRIC-LWP-21 | 06, 11 | 2 | 4 |
| GNRIC-LWP-22 | 06, 11 | 2 | 4 |
| GNRIC-LWP-23 | 06, 11 | 5 | 31 |
| GNRIC-LWP-24 | 06, 11 | 5 | 31 |
| GNRIC-PKP-01 | 06, 11 | 4 | 44 |
| GNRIC-PKP-02 | 06, 11 | 4 | 44 |
| GNRIC-PKP-03 | 06, 11 | 4 | 44 |
| GNRIC-PKP-04 | 06, 11 | 4 | 44 |

In 08A11-STA-01 (read from the drawing open in SOLIDWORKS):

- Layer "0" is near-black: RGB 25,25,36.
- 36 notes on layer 0 (the balloons) carry their own colour, white (RGB 255,255,255).
- 6 other annotations on layer 0 take the layer's colour.
- 47 notes are on no layer and are black.
- The PDF has 36 white text items, which are exactly those notes.

## Test, 10 Nov (Grant's go)

These are scratch copies in `C:\PDM-work\whitetest`, exported with SwCmd export-pdf. Neither drawing was saved, and each setting was put back afterwards.

| Export | 08A11-STA-01 white text / lines | GNRIC-BRP-10 white lines |
| --- | --- | --- |
| As it is now | 36 / 72 | 2 |
| (a) Page "Print White Items Black" = 1 (registry, set while SOLIDWORKS was closed) | 36 / 72 | 2 |
| (b) Drawings paper colour pure white (API pref 217) | 36 / 72 | 2 |
| (c) Page setup drawing colour = Black and white (API pref 139) | 36 / 72 | 2 |
| (d) PDF Export In Color off (API toggle 323) | 36 / 72, everything else black | 2 |
| (e) White annotations recoloured in memory to their layer colour, then export | **0 / 0** | **0** |

No SOLIDWORKS setting changes what Save as PDF writes. The white is a colour stored on the annotations themselves. BRP-10's two white segments are also annotations (2 of its 71); they show as two short tick marks at the drainage holes.

All settings were restored. Page, Colors and Color Schemes match the backup exactly, and a fresh SOLIDWORKS reads paper 14411494, page-setup colour 1 and PDF-in-colour on.

## Re-issue, night of 10 Nov (Grant's go in SW's window; Admin's conditions)

Dev added the recolour to SwCmd export-pdf (6a60bb0). It runs in memory only: the drawing closes without saving, and export-pdf refuses a drawing that's already open. App ran the release step with --reissue and filed the result.

| Drawings | Result |
| --- | --- |
| 35 re-issued (63 PDFs: LS-08X-06_0A 34, LS-08X-11_0A 29) | SW scan of the dry-run PDFs: 33 have no white, and they match the filed 0A apart from colour (pages, page size, every text item within 2 pt, number of drawn items) |
| GNRIC-BRW-16, 08BXX-HBW-01 | One white path is left: the sheet format's 24 zone ticks (≈5 mm, around the border), as in the old 0A. Admin: --accept-white for these two only |
| GNRIC-LPP-05 | Clean; --accept-rev with Grant's 9 Nov reason (REV read as "/ 0A") |
| 08A06/08B06/08C06/08A11/08B11/08C11-STA-01 | Left out (Admin): James checked in new versions of shared parts they use on 10 Nov, so a re-export would carry his changes. Still white |

SW's check after filing (all 240 PDFs in `P:\APEX\SC08\_Review\LS-08X-06_0A` and `LS-08X-11_0A`):
- White is left only in the 6 STA-01 PDFs and the zone ticks.
- All 63 filed PDFs are byte-identical to the scanned dry-run PDFs.
- Each ZIP holds exactly its folder's PDFs (127 and 113), byte-identical.
