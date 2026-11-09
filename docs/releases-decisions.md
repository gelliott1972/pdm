# Drawing releases: decisions

Labels R1, R2, … Agreed with Grant 29 Oct 2026 unless marked open. Background: `SPEC.md` "Issuing drawings"; the AutoCAD job in `C:\code\jerry` (PDF picture compare for clouds, PDF annotations with PyMuPDF; `HANDOVER.md`).

| # | Question | Decision | Status |
| --- | --- | --- | --- |
| R1 | Official revision codes | **Numbers**: 1, 2, 3, ... for official releases; the client (Disney) sees only numbers. Replaces the 29 Oct letters A, B, C. | changed by Grant 7 Nov (Desk A2) |
| R2 | Sub-versions | **Letters after the number**: before the first official release `0A`, `0B`, `0C`, ...; then release `1`; then `1A`, `1B`, ... before release `2`. A lettered version is an unofficial issue for people to check and comment on before the formal release. No full stops in file names (`08A06-HBA-01_0A.pdf`, `..._1.pdf`). The title block REV shows the full code; the revision table lists official numbers only (as before, now numbers). Replaces P1/P2 and A0/A1 (31 Oct). James set 56 drawings' title-block revision to `0A` on 7 Nov. | changed by Grant 7 Nov (Desk A2) |
| R3 | Release types | Official (new number): External Review, For Construction, As Built. Lettered version (`0A`, `1A`, ...): Internal Check, Internal Review. | agreed 31 Oct |
| R4 | Revision table | Engineer fills it in; the tool checks the released PDF (title block REV = newest table row = file name). Filling it in automatically comes later, with automatic PDF export. | agreed |
| R5 | Auto-cloud | Start on the PDF (reuse the `C:\code\jerry` compare). Then find out what clouds inside SOLIDWORKS would take; may be too big. | agreed |
| R6 | Comments back | Reviewers mark up the PDF with text, usually in Bluebeam. The tool reads the comments into a register (open/closed/response); the next release says which it addressed. | agreed |
| R7 | Where reviews happen | First just get the PDFs (issued and marked up) into a folder in the PDM. Email vs ACC later. | agreed |
| R8 | Where PDFs live | One set per scene: `<Scene>\_Released`, `_Review`, `_Commented`, `_WIP` (e.g. `P:\APEX\SC08\_Released`). A PDF next to a drawing is a draft, on that PC only; releasing moves it, so no second copy is left. `_WIP` details below are proposed. | agreed 31 Oct, `_WIP` details open |
| R9 | James's existing PDFs | All are `0A` (none sent out; was `P1` before R2 changed on 7 Nov). A one-off filing run moves them to `_Review`, replacing "list all PDFs as new files". Open: the drawings say REV A with a revision-table row "A, ORIGINAL DESIGN", which R2 and R4 would flag; SW asked 31 Oct. | agreed, 1 point open |
| R10 | Release sets | Releases go as screen or shaft sets, or larger. One action releases a chosen set; the folders stay flat and the PDM records what was in the set. Open: whether each drawing keeps its own letter. | agreed, 1 point open |

`_WIP` (proposed 31 Oct, for Grant's OK): for informal "please have a look", not a release. In the vault, so it syncs. Plain name, no code (`08A06-HBA-01.pdf`), no required note, one per drawing, replaced by the next send. Removed when that drawing is next released. Earlier copies stay in vault history.

Test files: `P:\APEX\SC08\LS-08X-06\ShaftA\08A06-HBA-01.pdf` and `08A06-HBW-01.pdf` (added by Grant 29 Oct, not yet in the vault index). Both are SOLIDWORKS PDFs with title block REV A and one revision-table row "A, ORIGINAL DESIGN, J MCKILLOP, G ELLIOTT"; the text reads cleanly with PyMuPDF; no annotations.

Grant 7 Nov (Desk A4): no PDFs go into the vault with the models. Release PDFs are made by the vault's release step and live only in the scene's release folders (R8). James's own PDFs next to his drawings stay on his PC as drafts; any that reached the vault are removed by check-out once the vault's 0A set exists.

Grant 7 Nov (Desk A5): a shared part's drawing (e.g. `APEX\GNRIC`) issued with a scene's set is filed in that set's scene folder (e.g. `APEX\SC08\_Review`), so each scene's issue is complete in one place.

Grant 7 Nov (Desk A13, discussed in Admin): **revision table.** One row per formal release, plus a top row for the current issue, which is replaced on each lettered issue until the next formal release locks it in. First formal release is `1`. Example: 0A: [0A]; 0B: [0B] (0A row replaced); release 1: [1]; 1A: [1A, 1]; 1B: [1B, 1]; release 2: [2, 1]. Release check (R4): top row = title block = file name, and every row below the top is a formal release number. James's drawings with one 0A row are correct.

Grant 9 Nov (Admin): **first 0A set = 0806 and 0811 only, not 0804 yet.** Shared drawings used by those models (e.g. `APEX\GNRIC`, `SC08\_Shared`) are included and filed in `APEX\SC08\_Review` (A5). Order: App's dry run (export and checks on scratch copies) and App builds the filing step, tested off the live vault; Grant reviews the report and some PDFs, then gives the go to file into the vault.

Grant 9 Nov (Admin): 0A set go. All 172 drawings (0806, 0811 and their shared drawings) filed into `APEX\SC08\_Review`. Drawings checked out at release time go in from the vault version, recorded as such (GNRIC-MSW-01; models GNRIC-LPP.SLDPRT). GNRIC-LPP-05 passed on Grant's visual check (the automatic check misread its REV as "/ 0A"). Stand-in test vault: delete (Grant's yes).

Grant 9 Nov (Admin): **release packages.** Every release also makes a ZIP of its PDFs and an Excel drawing list (also inside the ZIP), from the set record, written only by the release step; plus a running register `APEX\SC08\SC08_Drawing_Register.xlsx` (every drawing once, latest REV, date, sets). Releases go **per screen**: today's 0A is two sets, `LS-08X-06 0A` and `LS-08X-11 0A`; a shared drawing goes in every set whose models use it (its PDF filed once). Proposed place for ZIPs and lists: `APEX\SC08\_Review\_Sets\` (Grant may tweak).

Grant 9 Nov (Admin): **revision dates.** 0A keeps the dates James typed (internal check; the drawing list carries the issue date). From release 1 on, the release step writes the top revision row's date itself = the issue date, in an unambiguous format (`09-Nov-2026`, Admin's pick from Grant's OK), and the release check confirms date = issue date.

Grant 9 Nov (Admin): **0A error found after filing.** `08B06-HBW-03.SLDDRW` and `08C06-HBW-03.SLDDRW` pointed at ShaftA's `08A06-HBW-03.SLDPRT` (copied from A's drawing, never repointed; B's and C's parts are different). Their 0A PDFs show the wrong part. Filed files are never replaced: once James fixes them, the two go out as **0B**. The release check gets a new rule: drawing number = PartNo shown in its title block (unless accepted with a reason), and no drawing points at another shaft's model.
