# LS-08X-11 (0811) import decisions

Kept by Admin. Each decision has a fixed label (D1, D2, ...) that never changes or gets reused. Sources: `docs\briefs\import-0811.md`, SW's list `sw\0811-list.md` (25 Oct 2026; its Q numbers are in brackets).

Status: **proposed** = waiting for Grant; **agreed** = Grant said yes (date); **changed** = Grant chose differently (what).

| Label | Decision | Recommendation | Status |
| --- | --- | --- | --- |
| D1 | Drawings point at James's PC. Copy them into the work copies, then repoint each to its model there. | Yes. (Copy done 25 Oct with Grant's OK.) | agreed 25 Oct |
| D2 | Drawing names: from the configurations a drawing's views show, look up the PartNo. If the name already matches one, keep it; if there's exactly one PartNo, rename to it; otherwise decide. In the model's folder, no PartNo of its own. | Yes (Dev's rule) | agreed 25 Oct |
| D3 | A's 9 older drawings (`LS-08A-11-TIE_BEAM_1..3`, `LS-08A-11_SF0x-...`) duplicate numbered ones [Q11] | Leave out, list for James | agreed 25 Oct: they never enter the PDM, only the import report lists them |
| D4 | `08C11-SFA-3T` draws B's `SF03-UPPER`; C's `SF03-UPPER` has no PartNo [Q10] | Repoint to C's `LS-08C-11_SF03-UPPER`, give it `08C11-SFA-3T` (Provisional), flag for James | agreed 25 Oct. Probe: B and C SF03-UPPER have identical components (3 brackets, 4 LCMM, 2+7 ties); C has no PartNo. Most likely the C drawing was saved from B's and never repointed |
| D5 | A, B and C in one pass | Yes (SW: no reason to split) | agreed 25 Oct |
| D6 | Set Grant's git name and email on the laptop | Yes | agreed 25 Oct |
| D7 | No separate STA assembly in 0811: the top carries `08x11-STA-01` and its drawing draws the top [Q1] | Make `08x11-STA-01` a separate structure assembly as in 0806 (top = STA + `LS-08X-11`), so decision 7 (anchors in the STA) applies the same way; the STA drawing draws it | proposed |
| D8 | `SF01-LOWER`: James has PART 1 = `-1A`, PART 2 = `-1B`; the register has PART 1 = `-1B` [Q2] | Keep James's (file and drawings agree), Provisional; register mismatch listed for James | proposed |
| D9 | Header beam parts: James's `08x11-HBP-02` / `08x11-HBW-03` vs register `HBP-01` / `HBP-02` [Q3] | Keep James's, Provisional; listed for James | proposed |
| D10 | Brace `LS-08X-11_BRACE` holds `08X11-` and `08B11-` configurations [Q4] | Split by place tag, like the LCMM (a family must not cross a place tag): `08X11-BRP` and `08B11-BRP` | proposed |
| D11 | `08B11-MSA-01` (expansion-joint bracket assembly, B top only) [Q5] | Keep `08B11`: only 0811 B uses it (its brackets went to `08BXX` because 0806 B uses them too) | proposed |
| D12 | `PLATE_Bolting_Connector` has `STOCK-MSP-..` [Q6] | `GNRIC-MSP-..`, Provisional, as the ties went STOCK → GNRIC | proposed |
| D13 | Splice kit `SPLICE_JOINT_ASSY` (register `08X11-FSA-01`) and `Hex Bolt, washers & Nuts assy` inside it [Q6] | Grant: is the splice a delivered kit (`08X11-FSA-01`) or a modelling aid (`MDL-`, like the bolt kits)? The hex bolt kit is `MDL-`, beside the other kits. Its missing `M8x50` config only matters for splice config M3, which no top uses: note, don't fix | proposed |
| D14 | LCMM split: swap the 2-size interface plate for the 4-size `08XXX-PLP` inside `08X11-LMW` / `08B11-LMW` [Q7] | Yes, as 0806 | proposed |
| D15 | Grout and anchors (old `GROUT+ANCHOR_BOLTS` in the tops) [Q8] | After import, in the PDM, as 0806 B and C | proposed |
| D16 | 2 loose M6x12 screws and washers directly in the A top [Q9] | Grant: keep or remove? | proposed |
| D17 | New fastener PartNos in vault files `FAST-HEX-8.8-HDG` (M6x12, M6x50, M8x55, M8x90, M10x60, M10x140), `FAST-NUT-8.8-HDG` (M6, M8), `FAST-WSH-8.8-HDG` (6, 8) | Yes: Grant checks them out, Dev writes the PartNos, Grant checks in | proposed |
| D18 | C's `LS-08C-11_SF03-UPPER` has no Description, Weight or Finish; B's identical file has `SF03 TOP WELDED ASSY`, a Weight formula and `HDG` [SW, after D4] | Copy B's three properties, with the Weight formula naming C's own file | proposed |
