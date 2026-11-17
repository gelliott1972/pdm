# The 27 parts left on the Dropbox profiles, and 08X04-SFP-03 (prep for Grant, 18 Nov 2026)

SW, overnight 17-18 Nov. Scratch copies only; nothing written to `P:\`. Data: `scratch\profiles\the27\`.

## In short

- **Why they were left:** repointing a part to the vault profile rebuilds the profile sketch. 59 drawings show three of
  that sketch's own dimensions (H_leg, V_leg, Thickness: the tube or angle size), and those three went dangling in every one.
- **Can it be fixed automatically? Yes, for all 27.** On scratch copies of all 59 drawings, a new tool (`tools\sw-macros\SwRedim.cs`):
  1. deleted the 3 dangling dimensions;
  2. inserted the model's dimensions into that view only;
  3. kept the 3 with the same names, moved them to the old positions, and deleted everything else the insert added.

  Every drawing came back with the same values, the same dimension count and 0 dangling (59 of 59).
- **What still differs on paper** (each original and re-done drawing printed to PDF and compared):
  - The cut-list labels change, as the repoint intends: RHS80x40x4 -> RHS80x40x8, RHS100x100x5 -> SHS100x100x5,
    RSA -> EA, "PLATE, 5 x 32 x 72" -> "END CAP 72x32x5". 29 drawings, 52 of the 59 are released 0A.
  - **Decimal places, 4 drawings:**

    | Drawing | Printed before | Printed after |
    | --- | --- | --- |
    | 08XXX-HBA-01 | 6.3 | 6 (wrong) |
    | 08X04-DBP-01 | 40 / 8 | 40.0 / 8.0 |
    | 08X06-DBP-01 and -02 | 40 / 8 / 80 | 40.0 / 8.0 / 80.0 |

    The precision comes from the profile sketch in the library: the vault copy's sketch carries different decimal places
    from the old embedded one. The fix is to set the decimals on those dimensions explicitly; not yet tried.
  - **Colour:** the three dimensions printed grey before (driven by the library profile) and black after. A colour
    override could match it; not yet tried.

## Per part

| Part | Drawings (released 0A) | Values back, 0 dangling | Printed text | Auto-fix |
| --- | --- | --- | --- | --- |
| GNRIC-BRA | 3 (3) | 3 of 3 | cut-list labels only | yes |
| GNRIC-BRP | 7 (7) | 7 of 7 | 6 same; GNRIC-BRW-16 labels | yes |
| GNRIC-LM | 24 (24) | 24 of 24 | 19 same; LWP-20..24 labels | yes |
| GNRIC-MSW-01 | 1 (1) | 1 of 1 | label RSA50x50x5 -> EA50x50x5 | yes |
| 08A04-HBP-01, -HBP-02 | 1 each (0) | yes | same | yes |
| 08A04-HBW-01 | 1 (0) | yes | labels RHS40x40x4 -> SHS40x40x4, end cap | yes |
| 08X04-BRP-01 (drawing 08X04-DBP-01) | 1 (0) | yes | **40 / 8 -> 40.0 / 8.0** | yes, then set decimals |
| 08X04-HBW-02, -04, -06 | 1 each (0) | yes | labels RHS80x40x4 -> RHS80x40x8 (-06 also end cap) | yes |
| 08X06-DBP (drawings -01, -02) | 2 (2) | 2 of 2 | **40 / 8 / 80 -> 40.0 / 8.0 / 80.0** | yes, then set decimals |
| 08A11, 08B11, 08C11-HBP-02 | 1 each (1) | yes | label RHS100x100x5 -> SHS100x100x5 | yes |
| 08A11, 08B11, 08C11-HBW-01 and -HBW-03 | 1 each (1) | yes | SHS label and end cap | yes |
| 08B11-DBP-01, 08X11-DBP-01 | 1 each (1) | yes | same, identical print | yes |
| 08BXX-HBW-01, 08XXX-HBW-01 | 1 each (1) | yes | SHS label and end cap | yes |
| 08BXX-MSP-01 | 1 (1) | yes | same | yes |
| 08XXX-HBA-01 | 1 (1) | yes | **6.3 -> 6** | yes, then set decimals |

## How long

About an hour of SOLIDWORKS for all 27, run overnight like 16-17 Nov:
- repoint (seconds a part)
- SwRedim on the 59 drawings (about 10 s each)
- the same before/after checks
- PDFs for Grant to compare

That needs 27 parts and 59 drawings checked out and in.

## Risks and choices for Grant

1. **52 of the 59 drawings are released 0A.** Changing them (labels, and the 3 dimensions re-made) means a re-issue. Grant's call: re-issue now, or wait until each drawing next changes.
2. **Decimals** on 4 drawings need setting by hand or by the tool (next step if Grant wants it).
3. **Colour:** grey to black on the profile dimensions. Accept it, or try a colour override.
4. The dimensions are re-made objects. Anything a person added to them by hand (a note, a break) beyond position,
   precision, text, arrows and witness lines would not be carried over. None was seen in the PDF text.

## 08X04-SFP-03 (was 08C04-SFP-04)

Volumes to 4 decimals (`scratch\profiles\the27\sfp03\`):

| Repointed to | Long member | Short member (45 degree end) |
| --- | --- | --- |
| (as it is, Dropbox profile) | 488,846.3758 mm³ | 93,436.1589 mm³ |
| older vault copy `historical_files_accidental_rename\RHS 80x40x8.SLDLFP`, size RHS80x40x8 | same | 93,436.1427 mm³ (-0.016 mm³, 1 in 6 million) |
| current vault `rhs.sldlfp` | -5.2% | -5.4% (the file has no RHS80x40x8 size, so a different section is used) |

The other 28 parts on RHS80x40x8 came out exactly the same with the older copy. Only this part differs, by a rounding-level
amount, probably because of its mitred end. Last night's check rounds to 0.1 mm³ and this sits on the edge, so it refused.

Options:
- (a) Accept the 0.016 mm³ as Grant's exception and repoint to the older copy like the other 28.
- (b) Leave it on the Dropbox profile.
- (c) Fix the library first: give `rhs.sldlfp` a correct RHS80x40x8 size (its "RHS150x100x8" draws 80x40x8; survey section 1). That's a library change, Grant's call.

## Files

- Data: `scratch\profiles\the27\`: map.tsv, detail.txt (which dimensions dangle), redim2\ (result.tsv, pdf-compare.tsv, PDFs), sfp03\.
- Scripts: `sw\profiles\the27-*.py`.
- Tools: `tools\sw-macros\SwRedim.cs`. `SwFix.cs` got `--vol-digits=N` (stricter only).
