# Drawing releases: decisions

Labels R1, R2, … Agreed with Grant 29 Oct 2026 unless marked open. Background: `SPEC.md` "Issuing drawings"; the AutoCAD job in `C:\code\jerry` (PDF picture compare for clouds, PDF annotations with PyMuPDF; `HANDOVER.md`).

| # | Question | Decision | Status |
| --- | --- | --- | --- |
| R1 | Official revision codes | Letters A, B, C (skip I and O), as SPEC and the title block. WDI doesn't mind. | agreed |
| R2 | Sub-versions | No full stops in file names. `A0` is the official release; later internal versions are `A1`, `A2`, … Before the first official release: `P1`, `P2`, then `A0`. The title block REV shows the full code (`A1`); the revision table lists official letters only. | agreed 31 Oct |
| R3 | Release types | Official (new letter): External Review, For Construction, As Built. Internal version (`A1`, `A2`): Internal Check, Internal Review. | agreed 31 Oct |
| R4 | Revision table | Engineer fills it in; the tool checks the released PDF (title block REV = newest table row = file name). Filling it in automatically comes later, with automatic PDF export. | agreed |
| R5 | Auto-cloud | Start on the PDF (reuse the `C:\code\jerry` compare). Then find out what clouds inside SOLIDWORKS would take; may be too big. | agreed |
| R6 | Comments back | Reviewers mark up the PDF with text, usually in Bluebeam. The tool reads the comments into a register (open/closed/response); the next release says which it addressed. | agreed |
| R7 | Where reviews happen | First just get the PDFs (issued and marked up) into a folder in the PDM. Email vs ACC later. | agreed |
| R8 | Where PDFs live | One set per scene: `<Scene>\_Released`, `_Review`, `_Commented`, `_WIP` (e.g. `P:\APEX\SC08\_Released`). A PDF next to a drawing is a draft, on that PC only; releasing moves it, so no second copy is left. `_WIP` details below are proposed. | agreed 31 Oct, `_WIP` details open |
| R9 | James's existing PDFs | All are `P1` (none sent out). A one-off filing run moves them to `_Review`, replacing "list all PDFs as new files". Open: the drawings say REV A with a revision-table row "A, ORIGINAL DESIGN", which R2 and R4 would flag; SW asked 31 Oct. | agreed, 1 point open |
| R10 | Release sets | Releases go as screen or shaft sets, or larger. One action releases a chosen set; the folders stay flat and the PDM records what was in the set. Open: whether each drawing keeps its own letter. | agreed, 1 point open |

`_WIP` (proposed 31 Oct, for Grant's OK): for informal "please have a look", not a release. In the vault, so it syncs. Plain name, no code (`08A06-HBA-01.pdf`), no required note, one per drawing, replaced by the next send. Removed when that drawing is next released. Earlier copies stay in vault history.

Test files: `P:\APEX\SC08\LS-08X-06\ShaftA\08A06-HBA-01.pdf` and `08A06-HBW-01.pdf` (added by Grant 29 Oct, not yet in the vault index). Both are SOLIDWORKS PDFs with title block REV A and one revision-table row "A, ORIGINAL DESIGN, J MCKILLOP, G ELLIOTT"; the text reads cleanly with PyMuPDF; no annotations.
