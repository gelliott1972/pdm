# Brief: re-import 0806 A

From Admin, 22 Oct 2026. Decisions: `docs\imports\LS-08A-06-decisions.md` §12. Report in your own `status.md`.

| # | Who | Step |
|---|---|---|
| 1 | SW with Grant, in r4 (`E:\PDM-work\LS-08A-06-r4`) | End plate `LS-08X-X_TIE_BEAM_END_PLATE`: untick the equations link to screen 11's `Tie_Bar` .txt, keep the equations. |
| 2 | SW with Grant, in r4 | Interface plate `LS-08X-X_TIE_BEAM_INTERFACE_PLATE`: Weight on all 4 configurations = `SW-Mass@@<config>@LS-08X-X_TIE_BEAM_INTERFACE_PLATE.SLDPRT`. |
| 3 | SW with Grant, in r4 | `LS-08A-06_SF04-LOWER`: delete the suppressed floor plate instances. Then re-probe r4; nothing may point outside r4. |
| 4 | Dev, importer | SOLIDWORKS step after the Document Manager step: `SwFixRefs --apply` on every vault file, then a listing as a gate (no link outside the vault). |
| 5 | Dev, importer | Rewrite Weight/Material expressions that name an old file name, at file and configuration level. |
| 6 | Dev, importer | Report (not set) BOM settings on `MDL-`/`NONPART-` files. |
| 7 | Dev, plan | Right-angle tie → `08X06-SFW.SLDPRT` (SF01_DEEP `08X06-SFW-02`, SF04_DEEP `08X06-SFW-03`, Provisional; SF01 and SF04 unnumbered). Cantilever return → `08X06-SFW-04.SLDPRT`, number `08X06-SFW-04`, Provisional. |
| 8 | Admin | Done: 0806 A rows removed from `data\rename-ledger.csv`. |
| 9 | Dev, after 1–7 | Wipe `E:\PDM-test\P`, re-plan from r4, import, SwCheck. Report to Admin. |
| 10 | Grant | Opens `LS-08A-06+STRUCTURE` from `P:\` in SOLIDWORKS. Then App loads it. |

Steps 1–3 and 4–7 run at the same time. Dev tests 4–6 on the current `P:\` before the wipe.
