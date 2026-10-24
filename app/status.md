# App status

Updated 24 Oct 2026. Working to the revised brief (Cognito, DynamoDB, local page).

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
| Cognito login pool | `pdm-users` (`ap-east-1_8pzjtaAIo`): email login, admin-created users only, MFA off, password 8+ chars with a lowercase letter and a number (was 12; Grant 24 Oct), deletion protection on. App client `pdm-app`, no secret, SRP login, 30-day refresh. |
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
| Page: add new files (24 Oct, Grant: critical to MVP) | **Published as 0.1.5, 24 Oct** (Grant); first real use: James's `08A06-HBA-01.SLDDRW`. It failed (E_FAIL): the Document Manager can't list a drawing's configurations (Phase 0 never read a drawing). Fixed (skip configurations for drawings; tested on a SOLIDWORKS sample drawing on the laptop): **published as 0.1.6, 24 Oct** (Grant). Drawings don't claim a PartNo (they carry their model's, per SPEC). SOLIDWORKS files (.SLDPRT/.SLDASM/.SLDDRW) under `P:\` that aren't in the vault show as **new** (green) in the tree and in Check in, ticked, same note box; shared ones only for shared editors; never opened while listing. Check in adds them (parts, assemblies, drawings in that order; PartNo clash check; notes for references outside the vault shown as "n notes: see Details"). A file SOLIDWORKS holds open: "close it in SOLIDWORKS first"; one file failing no longer stops the rest. Checked: laptop `P:\` 93 files, 0 shown as new by mistake. |
| Bug: empty file list (24 Oct, James) | Cause: after saving a checked-out file in SOLIDWORKS, the change check read the file without sharing, Windows refused (SOLIDWORKS holds it open), and the whole file list failed; the page put the error on the hidden login form, so the tree was just empty. Vault index was fine (93 rows; James holds 18). Fix **published as 0.1.4, 24 Oct** (Grant via Admin; tested on the laptop, James's PC to update): read with sharing; a file that still can't be read counts as changed; list errors show on the page. Workaround until published: Save All and close the files in SOLIDWORKS, then Refresh. |
| Locks per PC | 24 Oct: a check-out belongs to a person **on one PC** (email + machine name). Needed because Grant is the same user on desktop and laptop; also right for anyone with two PCs. Elsewhere it shows as "you on GGPC" and can't be checked in or undone there. |
| Page: 24 Oct requests | **Published as 0.1.3, 24 Oct** (94 KB delta from 0.1.2), after Grant tried the test build; Grant and James testing. Tree: files line up with folders at the same level. Settings cog: email, version, change password (Cognito, current + new twice). Check in tab turns red with a count while I have changed files not checked in. New **Changes** tab: files newer in the vault than on this PC (who, when, note; "not on this PC" for ones never fetched) with Get latest. The 30 s refresh no longer redraws the right panel once it's been typed or ticked in (it was wiping notes and ticks). |
| Page: shared counts | 24 Oct (Grant): shared files show "used by n other assemblies", counting only assemblies outside what's being checked out. |
| **Done-when** | **Met 24 Oct.** Grant checked out `LS-08A-06+STRUCTURE` (18 files) from the page on the desktop; the laptop (C:\PDM, Get latest 57/57, references all on `P:\`) showed the 18 locks on GGPC; checked in from the page, all released. |
| Setup document | `docs\setup-machine.md`: engineer PC (A) and Grant's build/admin PC (B). |

## Next

1. ~~Load 0806 B and C~~: **done 24 Oct** on the desktop. 36 new files (B 19, C 17); vault now 93 files, 138 PartNos, 237 references, 0 outside, no clashes; all 93 read-only on the desktop; re-run dry finds 0 changed. The laptop needs **Get latest** to fetch the 36.
2. ~~Updater~~: **done 24 Oct** (built on the desktop). Bucket `pdm-releases` (ap-east-1, private, versioned, SSE-S3); `read-releases` inline policy (GetObject, ListBucket) on `pdm-engineer` and `pdm-shared-editor` (Grant approved). `src\publish.ps1` publishes with `pdm-admin`. Tested: laptop installed 0.1.0 from `Setup.exe`, was offered 0.1.1 (68 KB delta), restarted on it. **0.1.2 published from the laptop 24 Oct** (first publish there; the releases folder carried over, 93 KB delta from 0.1.1): re-checks at every login, and after an update the page says "Updated to v0.1.2" once. Desktop (GGPC) updated itself 0.1.1 → 0.1.2 (Grant checked). James's PC installed 0.1.2 from Setup.exe 24 Oct. Next: send James `Setup.exe` when his PC is ready; CI later.
3. Later: Grant's first real check-out on 08B/C (grout and anchors not needed now, per Admin 24 Oct).

## Open questions

- None open. Decided 23 Oct: the app goes to engineers as a single folder Grant shares (Dropbox or OneDrive); Referenced Documents list empty (forces paths to be right).

- Code signing: Smart App Control (Windows 11) blocks the unsigned app; the laptop has it off for now. Azure Artifact Signing isn't open to NZ companies; a commercial certificate (~US$200–500/yr, unverified) is the fix if James's PC has it on. Waiting on James's setting.

## Handover to the laptop (24 Oct)

All sessions move to the laptop after the 08B/C load; one clone (`C:\code\pdm`), Admin keeps pushing.

| Laptop fact | Value |
| --- | --- |
| Repo | `C:\code\pdm`, built with `dotnet build C:\code\pdm\src\Pdm` |
| `P:` | `C:\PDM` (registry DOS Devices; survives restarts), holds the vault from Get latest |
| Smart App Control | Off (build 26200.9711, so it can be turned back on) |
| `SW_DM_KEY` | Set (user environment variable) |
| `pdm-admin` SSO | **Needed** for publishing and loads: setup B5 and B6 |
| Machine name | Not GGPC; locks are per PC, so anything checked out on GGPC must be released there before it's switched off |

**Updater: agreed with Grant 24 Oct, nothing built yet.**

| Part | Decision / plan |
| --- | --- |
| Library | Velopack (`vpk` CLI to pack; `UpdateManager` in the app). Installs per user under `%LocalAppData%`, Start-menu shortcut, applies on restart. |
| Source | New private S3 bucket in `ap-east-1` (proposed `pdm-releases`: versioning, public access blocked, SSE-S3). Velopack has no S3 source: write a small `IUpdateSource` that reads the release feed and packages with the user's Cognito credentials (`Session.Credentials`). Same route to China as the vault. |
| Permissions | Add read-only (`s3:GetObject`, `s3:ListBucket`) on the releases bucket to `pdm-engineer` and `pdm-shared-editor`. **This changes real users' roles: tell Grant before applying** (it's additive, read-only). Publishing uses `pdm-admin` only. |
| App | Self-contained build (.NET bundled; setup step A2 drops out). Version number in `Pdm.csproj`. Check after login; download in the background; page shows "Update ready" with a **Restart** button; never restart mid-operation. |
| First install | `Setup.exe` from `vpk pack`, shared once by Dropbox. Replaces the "single folder" plan in setup A4; update `docs\setup-machine.md`. |
| Publishing, stage 1 | A script (under `src\`) that builds, packs and uploads with `pdm-admin`. |
| Publishing, stage 2 | Later: GitHub Action. Grant OK to put the SOLIDWORKS Document Manager interop DLL in the private repo. The GitHub-to-AWS (OIDC) trust is to be decided when needed. |
| Signing | Not yet. Smart App Control blocks the unsigned app on Windows 11 (see Open questions). `vpk` can sign once a certificate exists. |

Other things the laptop session should know:
- `pdm.exe` with no arguments = the page on `http://localhost:5170/` (`PagePort` in `pdm.settings.json`); `pdm.exe shell` = command line; `load [--dry]`, `map <folder>`, `inspect <file>`.
- A running `pdm.exe` locks `bin\...\pdm.exe`, so the build fails while it runs; build to another folder with `-o` to check code, or ask Grant to close it. The page HTML (`page\index.html`) is read per request: copy it into `bin\Debug\net8.0-windows\page\` to update without a restart.
- `P:` mapping: an administrator-window `subst` isn't seen by normal windows; `map` now says to restart.

## Notes

- New accounts in this organisation: Identity Center and `candelic-admin` are in `ap-east-1` (opt-in), so a new account can't be reached until `ap-east-1` is enabled in it. Step in via `--region ap-southeast-2` to enable it.
- Trusted access for Account Management is not enabled in the organisation; not changed.
- 0806 A import: an earlier pointer to stale "component" paths in 4 SKUs was probably wrong; that field is old in nearly every assembly. Grant's SOLIDWORKS Find References on the re-import shows every path on `P:\`.
- Desktop `P:` = `E:\PDM-test\P` (registry, made permanent 23 Oct). The vault was loaded from it.
