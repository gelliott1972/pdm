# App status

Updated 23 Oct 2026. Working to the revised brief (Cognito, DynamoDB, local page).

## Grant must do

| # | What | How | Why |
| --- | --- | --- | --- |
| 1 | Test checkout and check-in | After the load: run `pdm.exe` in a normal PowerShell window (it asks for your password, so Claude can't run it) | Brief tests 1 and 2 |

## Done

| Item | Result |
| --- | --- |
| .NET 8 SDK | Installed by Grant |
| Organisation choice | Child account under Candelic, not a new organisation (Grant agreed) |
| PDM account | `PDM`, `588966315587`, root email `grant+pdm@candelic.com` (Grant set root password and MFA; console only, no access keys) |
| Hong Kong region | Enabled in the PDM account |
| Grant's admin access | `AdministratorAccess` on PDM for `candelic-admin`; profile `pdm-admin` (reuses the `candelic-admin-sso` session) |
| Bucket | `pdm-apex`, `ap-east-1`: versioning on, all public access blocked, SSE-S3 encryption with bucket key |
| DynamoDB | `pdm-files` (key `fileId`, a GUID; index `by-path` on `path`) and `pdm-partnos` (key `partNo` → `fileId`, `path`, `config`, `status`). On-demand, point-in-time recovery on (Grant: keep). GUID key is Grant's idea: renames change one field, references are by `fileId`. Each S3 object carries its `fileId` as metadata (`pdm-file-id`) so a rebuild keeps the ids. |
| Cognito login pool | `pdm-users` (`ap-east-1_8pzjtaAIo`): email login, admin-created users only, MFA off, password 12+ chars, deletion protection on. App client `pdm-app`, no secret, SRP login, 30-day refresh. |
| Cognito identity pool | `pdm` (`ap-east-1:e6866600-7344-486c-b450-f4aca45bdb49`), logged-in only. Role from token; default `pdm-engineer`. |
| Roles | `pdm-engineer`: list and read the vault, upload except `_Shared/*`, `*/_Shared/*`, `APEX/GNRIC/*`; read and write both tables. `pdm-shared-editor`: same plus shared folders. No delete. Credentials last 1 hour. |
| Group | `shared-editors` → `pdm-shared-editor`. Grant and James are members. Shared files still default to read-only and unticked in the app (Grant). |
| Users | Grant (password set, login tested) and James (temporary password emailed 22 Oct, expires after 7 days). |
| Admin role | None in Cognito for now: admin jobs run with `pdm-admin` SSO (Grant agreed). |
| App: engine | `src\Pdm`, builds. `pdm.exe` = log in once, then `get`, `status`, `checkout`, `checkin -m`, `undo`. Check-in of a file not yet in the vault adds it (needed for new drawings). Locks are conditional writes on the file's index row. Local state in `P:\.pdm\state.json`. Tested 23 Oct on the desktop from the command line: check-out of `LS-08A-06+STRUCTURE` (lock in the index, file writable), edit in SOLIDWORKS, check-in with note (new S3 version, index updated, lock released, read-only again). Undo and the second-PC lock not yet tested. |
| App: loader | `pdm.exe load [--dry]`, admin, uses `pdm-admin`. Re-runnable: keeps ids and locks, uploads only new or changed files. Dry run 23 Oct: 57 files (28 parts, 29 assemblies, 72 MB), 105 PartNos, 107 references all inside the vault, 0 outside, no PartNo clashes. Real load 23 Oct: 57 objects in `pdm-apex`, 57 rows in `pdm-files`, 105 in `pdm-partnos`, all 57 local files read-only; a re-run dry finds 0 changed. |
| App: `P:` | `pdm.exe map <folder>`, run as administrator: DOS Devices registry entry plus an immediate `subst`. Run on the desktop 23 Oct: registry `P:` = `\??\E:\PDM-test\P`, `subst` shows it. Restart survival not yet checked. |
| Document Manager from .NET 8 | Works: `pdm.exe inspect <file>` read references and PartNos from `LS-08A-06+STRUCTURE`, a SKU (`DL_APEX` on `P:\`) and a fastener family. |
| App: page | Built 24 Oct, **not yet tried by Grant**. `pdm.exe` with no arguments serves `http://localhost:5170/` and opens it. Login (with first-login new password), folder tree with status, check out an assembly (tree with tick boxes: same screen folder ticked; shared unticked with "used by n files", tickable only for shared editors; others' files greyed with who and since), folder check-out (double-click a folder), check in / undo with note and changed marks, who has what, get latest. Refreshes every 30 s so another PC's locks show. Only answers localhost requests carrying an `X-PDM` header. Command line moved to `pdm.exe shell`. Checked: builds, serves the page, refuses requests without the header, rejects a bad login. |
| Locks per PC | 24 Oct: a check-out belongs to a person **on one PC** (email + machine name). Needed because Grant is the same user on desktop and laptop; also right for anyone with two PCs. Elsewhere it shows as "you on GGPC" and can't be checked in or undone there. |
| Page: shared counts | 24 Oct (Grant): shared files show "used by n other assemblies", counting only assemblies outside what's being checked out. |
| **Done-when** | **Met 24 Oct.** Grant checked out `LS-08A-06+STRUCTURE` (18 files) from the page on the desktop; the laptop (C:\PDM, Get latest 57/57, references all on `P:\`) showed the 18 locks on GGPC; checked in from the page, all released. |
| Setup document | `docs\setup-machine.md`: engineer PC (A) and Grant's build/admin PC (B). |

## Next

1. **Desktop (this App session):** load 0806 B and C into the vault once Dev has imported them into `E:\PDM-test\P` and SwCheck passes: `pdm.exe load --dry`, show Grant, then the real load.
2. **Laptop (new App session):** the updater (agreed 24 Oct): Velopack, releases in a private S3 bucket in `ap-east-1` read by logged-in engineers through a custom update source, .NET bundled in, "Update ready / Restart" on the page. Stage 1: a publish script run with `pdm-admin`. Stage 2 later: GitHub Action (SOLIDWORKS interop DLL in the private repo: Grant OK; GitHub-to-AWS trust: decide when needed).
3. Grant's first real check-out on the laptop after the load (SW guides): 08B06-STA-01, 08C06-STA-01, both tops, MDL-ANCHOR-08X06.

## Open questions

- None open. Decided 23 Oct: the app goes to engineers as a single folder Grant shares (Dropbox or OneDrive); Referenced Documents list empty (forces paths to be right).

- Code signing: Smart App Control (Windows 11) blocks the unsigned app; the laptop has it off for now. Azure Artifact Signing isn't open to NZ companies; a commercial certificate (~US$200–500/yr, unverified) is the fix if James's PC has it on. Waiting on James's setting.

## Handover to the laptop (24 Oct)

- Development moves to Grant's laptop (repo `C:\code\pdm`, `P:` = `C:\PDM`, Smart App Control off). The desktop keeps the 08B/C import and load.
- Two clones now: who pushes laptop commits needs agreeing with Admin (today Admin pushes from the desktop).
- The laptop needs `pdm-admin` (setup B6) before publishing updates.
- Page: one result line under the tabs (Details for the full list), done 24 Oct.

## Notes

- New accounts in this organisation: Identity Center and `candelic-admin` are in `ap-east-1` (opt-in), so a new account can't be reached until `ap-east-1` is enabled in it. Step in via `--region ap-southeast-2` to enable it.
- Trusted access for Account Management is not enabled in the organisation; not changed.
- 0806 A import: an earlier pointer to stale "component" paths in 4 SKUs was probably wrong; that field is old in nearly every assembly. Grant's SOLIDWORKS Find References on the re-import shows every path on `P:\`.
- Today `P:` is a temporary `subst P: E:\PDM-test\P`. The desktop's vault is loaded from it, so it becomes the desktop's working copy.
