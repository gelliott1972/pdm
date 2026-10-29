# Drawing releases: decisions

Labels R1, R2, … Agreed with Grant 29 Oct 2026 unless marked open. Background: `SPEC.md` "Issuing drawings"; the AutoCAD job in `C:\code\jerry` (PDF picture compare for clouds, PDF annotations with PyMuPDF; `HANDOVER.md`).

| # | Question | Decision | Status |
| --- | --- | --- | --- |
| R1 | Official revision codes | Letters A, B, C (skip I and O), as SPEC and the title block. WDI doesn't mind. | agreed |
| R2 | Sub-versions | No full stops in file names. `A0` is the official release; later internal versions are `A1`, `A2`, … Open: what versions before the first official release are called; whether the title block REV shows `A` or `A1`. | agreed, 2 points open |
| R3 | Release types | Internal Check, Internal Review, External Review, For Construction, As Built. Open: which make a new letter (official) and which a sub-version. | agreed, 1 point open |
| R4 | Revision table | Engineer fills it in; the tool checks the released PDF (title block REV = newest table row = file name). Filling it in automatically comes later, with automatic PDF export. | agreed |
| R5 | Auto-cloud | Start on the PDF (reuse the `C:\code\jerry` compare). Then find out what clouds inside SOLIDWORKS would take; may be too big. | agreed |
| R6 | Comments back | Reviewers mark up the PDF with text, usually in Bluebeam. The tool reads the comments into a register (open/closed/response); the next release says which it addressed. | agreed |
| R7 | Where reviews happen | First just get the PDFs (issued and marked up) into a folder in the PDM. Email vs ACC later. | agreed |
| R8 | Where PDFs live | Today James keeps them next to the models. Open. | open |
| R9 | James's existing PDFs | Need an app update so he can check them all in. | agreed, App asked 29 Oct |

Test files: `P:\APEX\SC08\LS-08X-06\ShaftA\08A06-HBA-01.pdf` and `08A06-HBW-01.pdf` (added by Grant 29 Oct, not yet in the vault index). Both are SOLIDWORKS PDFs with title block REV A and one revision-table row "A, ORIGINAL DESIGN, J MCKILLOP, G ELLIOTT"; the text reads cleanly with PyMuPDF; no annotations.
