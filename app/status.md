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
| App: engine | `src\Pdm`, builds. `pdm.exe` = log in once, then `get`, `status`, `checkout`, `checkin -m`, `undo`. Check-in of a file not yet in the vault adds it (needed for new drawings). Locks are conditional writes on the file's index row. Local state in `P:\.pdm\state.json`. **Not yet tested end to end** (needs Grant's login). |
| App: loader | `pdm.exe load [--dry]`, admin, uses `pdm-admin`. Re-runnable: keeps ids and locks, uploads only new or changed files. Dry run 23 Oct: 57 files (28 parts, 29 assemblies, 72 MB), 105 PartNos, 107 references all inside the vault, 0 outside, no PartNo clashes. Real load 23 Oct: 57 objects in `pdm-apex`, 57 rows in `pdm-files`, 105 in `pdm-partnos`, all 57 local files read-only; a re-run dry finds 0 changed. |
| App: `P:` | `pdm.exe map <folder>`, run as administrator: DOS Devices registry entry plus an immediate `subst`. Run on the desktop 23 Oct: registry `P:` = `\??\E:\PDM-test\P`, `subst` shows it. Restart survival not yet checked. |
| Document Manager from .NET 8 | Works: `pdm.exe inspect <file>` read references and PartNos from `LS-08A-06+STRUCTURE`, a SKU (`DL_APEX` on `P:\`) and a fastener family. |
| Setup document | `docs\setup-machine.md`: engineer PC (A) and Grant's build/admin PC (B). |

## Next

1. Grant: tests 1 and 2 with `pdm.exe`.
2. Page (local web dashboard), after the engine is proven.

## Open questions

- None open. Decided 23 Oct: the app goes to engineers as a single folder Grant shares (Dropbox or OneDrive); Referenced Documents list empty (forces paths to be right).

## Notes

- New accounts in this organisation: Identity Center and `candelic-admin` are in `ap-east-1` (opt-in), so a new account can't be reached until `ap-east-1` is enabled in it. Step in via `--region ap-southeast-2` to enable it.
- Trusted access for Account Management is not enabled in the organisation; not changed.
- 0806 A import: an earlier pointer to stale "component" paths in 4 SKUs was probably wrong; that field is old in nearly every assembly. Grant's SOLIDWORKS Find References on the re-import shows every path on `P:\`.
- Today `P:` is a temporary `subst P: E:\PDM-test\P`. The desktop's vault is loaded from it, so it becomes the desktop's working copy.
