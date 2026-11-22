# Brief: PDM Outputs (App)

Grant, 22 Nov 2026: "go Outputs". Order: the current release first (get fix, Make PDFs, Needs drawing), then Outputs.

## What

One place where everyone gets the files made **from** the vault, as well as the SOLIDWORKS files.

| Output | Made by | Today |
|---|---|---|
| Square-edge STEPs: `<LS>_STR.STEP` (structure, square tube corners, for shape only, not mass), `<LS>_LED.STEP` (DL_APEX tiles), notes `.txt` | SW, `sw/sestp/sestp.py` | `C:\PDM-work\sestp\runs\<LS> <UTC stamp>\` |
| Revit wireframe files | wireframe / apex-admin | `C:\code\apex` |
| Engineering calcs (PDF report + workbook) | calc sessions | `C:\code\apex` |

**Why the PDM:** Dropbox is blocked in mainland China without a VPN. The PDM's Hong Kong S3 storage works from China (Phase 0).

## Stages

1. **Adding outputs: admin only.** `pdm output add <screen> <files...> --pin <UTC> --note "..."` (or similar) with the pdm-admin SSO login. Each output set records the screen, the vault pin it was made from (and, where known, the file/version list, e.g. the sestp notes file), who made it and when. A set is never overwritten; a new pin makes a new set.
2. **Outputs tab on the PDM page:** read-only, download only, with no check-out. Grouped by screen, then by pin (newest first), with each set's pin shown clearly so nobody pairs a new STEP with an old calc. Grant has OK'd read access for engineers' logins to the outputs area. Read-only: engineers cannot add or delete.
3. **Dropbox copy (mirror):** `C:\Users\grant\Dropbox\Unilumin Shared\PDM_Outputs` (exists, empty). The PDM is the master and Dropbox is a read-only mirror. Write only inside that folder, never anywhere else in Dropbox (M:\ / Screen Models stays untouched). Same layout as the tab.

## Rules

- Outputs live outside the vault's file records: they're not check-out files, and not on P:.
- Removing a set: admin only, recorded, nothing hard-deleted from S3 (versioning is on).
- Test on the scratch vault first; read-only on live until Grant's go; Grant types the publish in App's window.
- Ask Admin about anything this brief doesn't settle (no questions to Grant in App's window).
