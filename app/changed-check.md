# Changed-check, step 1: what a no-change re-save changes (App, 7 Nov 2026)

Brief: `docs\briefs\changed-check.md`. Scratch copies only; nothing on `P:\` was written.

## Test

Four copies outside the vault: `GNRIC-PKP.SLDPRT` (part, 4 configurations), `GNRIC-LM.SLDPRT` (25 configurations), `08A06-HBA-01.SLDDRW`, `08A06-STA-01.SLDASM`. Each one went through SwCmd open → save → close twice, with no change, in SOLIDWORKS 2026 SP3 (34.3.2) on the NZ PC. Before and after were compared section by section. The file format (2015+) is a series of sections: a marker, a stamp, a checksum, the compressed and raw sizes, a name with nibble-swapped bytes, then raw-deflate data. My reader (scratchpad `swsec.py`) unpacks every section and checks that it inflates to its stated size.

## Result: a no-change save rewrites far more than metadata

| | Part (PKP) | 25-config part (LM) | Drawing (HBA-01) | Assembly (STA-01) |
| --- | --- | --- | --- | --- |
| File MD5 changed | yes | yes | yes | yes |
| File size change on the 2nd no-op save | −1.7 KB | −2.9 KB | +0.8 KB | −7 bytes |
| Sections whose unpacked content changed (of total) | 15 of 74 | 15 of 224 | 8 of 33 | 29 of 50 |

Expected, and safe to skip: Preview and PreviewPNG images, `docProps/core.xml` and `ISolidWorksInformation.xml` (save time), `_MO_VERSION_*/Biography` (save history), `ModelStamps`, `DLUpdateStamp`, `Header2`.

But on every no-op save, core model sections change too:

| Section | What changed on a no-op save |
| --- | --- |
| `Contents/Config-1-Partition` (part) | 5,927 of 6,481 bytes |
| `Contents/Definition` | 20 to 102 scattered places |
| `Contents/DisplayLists` | 25 to 1,644 places |
| `Contents/Config-0-MatesList` (assembly) | 1,001 places |
| `Contents/Config-0`, `CMgr`, `ResolvedFeatures`, `FaceTessellations`, `swXmlContents/KeyWords` | a few to hundreds of places |

So the brief's fingerprint ("a hash over everything except the parts that change on a no-op save") would have to leave out the sections that hold the model itself. A real change to a dimension, a mate or a feature would then also be missed. That breaks the rule "never lose a real change", so on this evidence step 2 shouldn't be built as described. I didn't run the opposite test (small real changes), because the no-op half already rules this fingerprint out.

Vault history agrees: the last two versions of 12 of James's files differ in the same core sections. That doesn't prove anything by itself, since those versions may hold real edits.

## Other ways (for Admin and Grant to choose; not built)

| Option | How | Catches real changes? | Cost |
| --- | --- | --- | --- |
| A. Content fingerprint from what Document Manager can read | Hash of references, configurations, custom properties and the BOM/PartNos, per file | Misses dimension, feature and mate edits | Quick; would skip only files whose properties and structure are equal, so not safe on its own |
| B. Fingerprint from SOLIDWORKS | Per configuration: mass properties (volume, area, centre of mass) and feature/dimension values, plus A | Most real edits | Needs SOLIDWORKS at check-in; seconds per file; James's PC would need SwCmd |
| C. Stop the cause | The page warns at check-out ("only check out what you'll change"), and offers **Undo** for files that are checked out but that James says he didn't change. Undo releases without upload | James decides | Cheap, no risk to real changes |
| D. Accept the uploads, make them faster | Parallel uploads (option E from 0.1.9) | Everything uploads | Already designed, not built |

Recommendation: C plus D now (safe, cheap). Consider B later if C isn't enough.
