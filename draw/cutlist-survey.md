# Cut-list survey

For SW's later clean-up (Grant, 15 Nov). Read-only, with the Document Manager over every part on `P:\` (`draw\src\CutList.cs --survey`, 15 Nov 2026): 117 parts, 87 with cut lists, 882 cut-list lines (one per item per configuration). Raw list: `scratch\draw-test\cutlist-survey.tsv`.

Drawings don't depend on this: Draw writes the stock on the note block's Material line from the section it measures in the model (D7), and flags a cut-list label that disagrees.

| Finding | Count | What it means |
| --- | --- | --- |
| Label calls a square tube RHS (or a rectangular one SHS) | 51 labels in 49 files | e.g. "RHS40x40x4" for SHS 40x40x4. Comes from the profile library's description |
| Label disagrees with its profile file | 2 | e.g. "RHS40x40x4" from `SHS_40x40x4.SLDPRT` |
| Label disagrees with the model (measured) | 1 so far | GNRIC-LM: "RHS80x40x4", James's GNRIC-LWP-20 shows the profile 80 x 40 x **8**. Measuring needs SOLIDWORKS: Draw checks each part as its batch is drawn (batch 1: all 10 GNRIC-LMW are SHS 40x40x4, labels say RHS) |
| Cut-list item with no Description | 233 lines in 38 files | Mostly end caps and plates (only MATERIAL and QUANTITY) |
| As Welded length differs from As Machined | 9 | Same configuration name, different LENGTH: a stale cut list, or the As Welded child belongs to another length |

## 1. Labels: RHS for a square tube

| File | Label | Configurations |
| --- | --- | --- |
| GNRIC\GNRIC-LMW.SLDPRT | RHS40x40x4 | 13 |
| SC08\LS-08X-04\ShaftA\08A04-HBP-01.SLDPRT | RHS40x40x4 | 1 |
| SC08\LS-08X-04\ShaftA\08A04-HBP-02.SLDPRT | RHS40x40x4 | 1 |
| SC08\LS-08X-04\ShaftA\08A04-HBW-01.SLDPRT | RHS40x40x4 | 2 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-01.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-02.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-03.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-04.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-05.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-05.SLDPRT | RHS100x100x5 | 4 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-06.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftB\08B04-BKP-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-01.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-02.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-03.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-04.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-05.SLDPRT | RHS150x150x8 | 2 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-06.SLDPRT | RHS150x150x8 | 2 |
| SC08\LS-08X-04\ShaftB\08B04-SFP-05.SLDPRT | RHS40x40x4 | 2 |
| SC08\LS-08X-04\ShaftC\08C04-HBP-01.SLDPRT | RHS40x40x4 | 2 |
| SC08\LS-08X-04\ShaftC\08C04-HBP-02.SLDPRT | RHS40x40x4 | 2 |
| SC08\LS-08X-04\ShaftC\08C04-HBW-01.SLDPRT | RHS40x40x4 | 2 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-01.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-02.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-03.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-04.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-05.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-06.SLDPRT | RHS150x150x8 | 4 |
| SC08\LS-08X-04\_Shared\08X04-BKP-06.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-04\_Shared\08X04-BKP-07.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-04\_Shared\08X04-BRP-02.SLDPRT | RHS40x40x4 | 2 |
| SC08\LS-08X-06\ShaftA\08A06-HBW-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftA\08A06-HBW-02.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftA\08A06-HBW-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftB\08B06-HBW-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftB\08B06-HBW-02.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftB\08B06-HBW-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftC\08C06-HBW-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftC\08C06-HBW-02.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-06\ShaftC\08C06-HBW-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftA\08A11-HBP-02.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftA\08A11-HBW-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftB\08B11-HBP-02.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftB\08B11-HBW-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftB\08B11-HBW-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftC\08C11-HBP-02.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftC\08C11-HBW-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\LS-08X-11\ShaftC\08C11-HBW-03.SLDPRT | RHS100x100x5 | 2 |
| SC08\_Shared\08BXX-HBW-01.SLDPRT | RHS100x100x5 | 2 |
| SC08\_Shared\08XXX-HBW-01.SLDPRT | RHS100x100x5 | 2 |

## 2. Label vs profile file

| File | Label | Profile file | Configurations |
| --- | --- | --- | --- |
| GNRIC\GNRIC-LMW.SLDPRT | RHS40x40x4 | SHS_40x40x4.SLDPRT | 13 |
| SC08\LS-08X-04\ShaftC\08C04-HBW-01.SLDPRT | RHS40x40x4 | SHS_40X40X4_HORIZONTAL_TOP_BAR^LS-08A-04_SF01.SLDPRT | 2 |

## 3. Cut-list items with no Description

| File | Items |
| --- | --- |
| SC08\LS-08X-04\ShaftA\08A04-KBP-03.SLDPRT | Cut-List-Item14, Cut-List-Item15, Cut-List-Item16, Cut-List-Item17, Cut-List-Item18, Cut-List-Item5, Cut-List-Item6, Cut |
| SC08\LS-08X-04\ShaftA\08A04-KBP-05.SLDPRT | Cut-List-Item10, Cut-List-Item14, Cut-List-Item15, Cut-List-Item16, Cut-List-Item17, Cut-List-Item20, Cut-List-Item8 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-01.SLDPRT | Cut-List-Item1, Cut-List-Item4, Cut-List-Item5, Cut-List-Item6, Cut-List-Item7, Cut-List-Item9 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-02.SLDPRT | Cut-List-Item10, Cut-List-Item4, Cut-List-Item5, Cut-List-Item6, Cut-List-Item7, Cut-List-Item9 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-05.SLDPRT | Cut-List-Item11, Cut-List-Item15, Cut-List-Item4, Cut-List-Item6, Cut-List-Item8, Cut-List-Item9 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-02.SLDPRT | Cut-List-Item10, Cut-List-Item12, Cut-List-Item4, Cut-List-Item6, Cut-List-Item9 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-04.SLDPRT | Cut-List-Item10, Cut-List-Item13, Cut-List-Item14, Cut-List-Item16, Cut-List-Item8 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-06.SLDPRT | Cut-List-Item10, Cut-List-Item11, Cut-List-Item4, Cut-List-Item5, Cut-List-Item8 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-03.SLDPRT | Cut-List-Item11, Cut-List-Item4, Cut-List-Item6, Cut-List-Item7, Cut-List-Item9 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-04.SLDPRT | Cut-List-Item10, Cut-List-Item4, Cut-List-Item5, Cut-List-Item6, Cut-List-Item8 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-01.SLDPRT | Cut-List-Item12, Cut-List-Item14, Cut-List-Item15, Cut-List-Item16, Cut-List-Item18 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-03.SLDPRT | Cut-List-Item4, Cut-List-Item5, Cut-List-Item7, Cut-List-Item8 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-02.SLDPRT | Cut-List-Item4, Cut-List-Item5, Cut-List-Item6 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-04.SLDPRT | Cut-List-Item4, Cut-List-Item5, Cut-List-Item7 |
| SC08\LS-08X-04\ShaftC\08C04-KBP-06.SLDPRT | Cut-List-Item4, Cut-List-Item5, Cut-List-Item7 |
| GNRIC\GNRIC-LMW.SLDPRT | Cut-List-Item1, Cut-List-Item4 |
| SC08\LS-08X-04\ShaftB\08B04-BKP-03.SLDPRT | Cut-List-Item2, Cut-List-Item4 |
| SC08\LS-08X-04\_Shared\08X04-BKP-06.SLDPRT | Cut-List-Item2, Cut-List-Item4 |
| SC08\LS-08X-04\_Shared\08X04-BKP-07.SLDPRT | Cut-List-Item2, Cut-List-Item3 |
| SC08\LS-08X-06\_Shared\08X06-SFW-04.SLDPRT | Cut-List-Item3, Cut-List-Item6 |
| GNRIC\GNRIC-LM.SLDPRT | PLATE, 5 x 32 x 72<1> |
| SC08\LS-08X-04\ShaftA\08A04-HBW-04.SLDPRT | Cut-List-Item7 |
| SC08\LS-08X-04\ShaftA\08A04-HBW-05.SLDPRT | Cut-List-Item5 |
| SC08\LS-08X-04\ShaftA\08A04-KBP-01.SLDPRT | Cut-List-Item5 |
| SC08\LS-08X-04\ShaftB\08B04-BRW-01.SLDPRT | Cut-List-Item2 |
| SC08\LS-08X-04\ShaftB\08B04-BRW-02.SLDPRT | Cut-List-Item1 |
| SC08\LS-08X-04\ShaftB\08B04-KBP-05.SLDPRT | Cut-List-Item12 |
| SC08\LS-08X-04\ShaftB\08B04-SFP-04.SLDPRT | Cut-List-Item2 |
| SC08\LS-08X-04\ShaftB\08B04-SFP-05.SLDPRT | Cut-List-Item2 |
| SC08\LS-08X-04\ShaftC\08C04-HBW-01.SLDPRT | Cut-List-Item23 |
| SC08\LS-08X-04\ShaftC\08C04-SFP-04.SLDPRT | Cut-List-Item3 |
| SC08\LS-08X-04\ShaftC\08C04-SFP-05.SLDPRT | Cut-List-Item2 |
| SC08\LS-08X-04\ShaftC\08C04-SFP-06.SLDPRT | Cut-List-Item2 |
| SC08\LS-08X-04\_Shared\08X04-BKP.SLDPRT | Cut-List-Item2 |
| SC08\LS-08X-04\_Shared\08X04-PLP-05.SLDPRT | Cut-List-Item1 |
| SC08\LS-08X-04\_Shared\08X04-SFP-04.SLDPRT | Cut-List-Item3 |
| SC08\LS-08X-11\ShaftB\08B11-HBW-01.SLDPRT | Cut-List-Item5 |
| SC08\LS-08X-11\ShaftC\08C11-HBW-01.SLDPRT | Cut-List-Item5 |

## 4. As Welded length differs from As Machined

| File | Configuration | Item | As Machined | As Welded |
| --- | --- | --- | --- | --- |
| GNRIC\GNRIC-LM.SLDPRT | OPEN-1336+2M+250-SPL<As Machined> | RHS80x40x4<1> | 2086 | 2930 |
| GNRIC\GNRIC-LM.SLDPRT | SPL-250+9M+680-FP1<As Machined> | RHS80x40x4<1> | 3180 | 2930 |
| GNRIC\GNRIC-LM.SLDPRT | PL-395+6M+250-SPL | RHS80x40x4<1> | 2145 | 2930 |
| GNRIC\GNRIC-LM.SLDPRT | SPL-250+7M+680-FP1 | RHS80x40x4<1> | 2680 | 2670 |
| GNRIC\GNRIC-LM.SLDPRT | SPL-500+9M+180-FP1 | RHS80x40x4<1> | 2930 | 2920 |
| GNRIC\GNRIC-LM.SLDPRT | OPEN-46+8M+250-SPL | RHS80x40x4<1> | 2296 | 2546 |
| GNRIC\GNRIC-LMW.SLDPRT | EC-0+2M+46-OPEN | RHS40x40x4<1> | 541 | 1041 |
| GNRIC\GNRIC-LMW.SLDPRT | EC-0+3M+46-OPEN | RHS40x40x4<1> | 791 | 541 |
| GNRIC\GNRIC-LMW.SLDPRT | EC-0+5M+0-EC | RHS40x40x4<1> | 1240 | 1286 |
