# Brief for App: the PDM basics

From Admin, 21 Oct 2026 (replaces the earlier command-line version). Report in `app\status.md`; questions to Grant directly, in your window. Keep messages to Grant short.

## The workflow we're building

The user opens the PDM page, logs in, says "I want to work on `LS-08A-06`", the app locks the files that assembly needs (with care around shared folders), they work in SOLIDWORKS, then check it back in. That's it.

## The basics (agreed with Grant, 21 Oct 2026)

| # | Basic | What it is |
| --- | --- | --- |
| 1 | Login | Cognito user pool, email and password per person, short-lived AWS credentials from it (identity pool). One permission switch for now: **may edit shared folders** (a Cognito group). Admin creates users. |
| 2 | Vault | S3 bucket in `ap-east-1`, versioning on, public access blocked, keys mirror the `P:` tree. |
| 3 | Index | DynamoDB: one record per file (path, current version, PartNo per configuration, references, lock holder, time, note) and a PartNo register. S3 wins; an admin command rebuilds the index from S3 with a Document Manager scan. Locks are conditional writes on the file record. |
| 4 | `P:` | The user picks a root folder; the app maps it to `P:` with a permanent SUBST (survives restart, visible to all programs; warn if `P:` is taken). |
| 5 | Get latest | Sync the vault down. Files not checked out by me are read-only. |
| 6 | Check out an assembly | The user picks an assembly; the app shows its tree from the index (sub-assemblies, parts, skeletons, part links) with tick boxes: files in the same screen folder ticked; shared files (`_Shared`, `GNRIC`, `SC08\_Shared`, a screen's `_Shared`) unticked, marked "used by n other files", and only tickable if the user may edit shared folders; files someone else holds greyed with who and since when. Check out takes the ticked files in one go. Folder and single-file check-out also exist, for drawings and loose parts. |
| 7 | Check in / undo | Check-in shows the files I hold under that assembly with what changed marked, takes a note, uploads new versions, updates the index (references and PartNos re-read with the Document Manager), releases the locks, sets read-only. Undo releases and restores. |
| 8 | Dashboard | A local web page served by the app: login, the project tree with status, who has what and since when, the check-out and check-in screens above. |

**UI:** plain and quiet. Short labels, no explanatory paragraphs, nothing that looks AI-generated. A file tree, status colours, a few buttons. Grant will say what's missing.

**Not in the basics:** per-folder permissions beyond the one switch, projects and subprojects, Issue, translation, conflict copies, next-free-number (the register makes it possible later).

## Stack

- C# on .NET 8 (install the SDK: `winget install Microsoft.DotNet.SDK.8`, ask Grant), one local app that serves the page on localhost and does the file work (Document Manager is COM, so it runs on the PC).
- AWS SDK for .NET: Cognito, S3, DynamoDB.
- Settings in a file, not code: region, bucket, table, user pool and identity pool ids, local root.

## AWS setup (with Grant; he does the interactive logins)

Grant's own admin access is SSO (`candelic-admin` and the PDM account's profile; agree the name). **Engineers never get IAM or SSO access**: they log in through Cognito only.

1. Confirm the PDM child account and its SSO profile. Enable `ap-east-1`.
2. Bucket (e.g. `candelic-pdm-apex`, suffix if taken): versioning, encryption, block public access.
3. DynamoDB table(s) in `ap-east-1`, on-demand billing.
4. Cognito user pool (email login, admin-created users, MFA off for now) and identity pool; authenticated role limited to the bucket and table; a `shared-editors` group mapped to a role that may also write shared prefixes. Users: Grant, James.
5. Record ids in the settings file (not secrets). Nothing permanent on PCs.

Ask before anything that costs beyond these, and before anything hard to undo.

## Build order

1. AWS: 1–4 above.
2. Engine, driven from a test command line first: login, get, lock, check in, index read and write, SUBST.
3. Import bridge: a command that takes Dev's imported tree in `E:\PDM-test\P` plus the plan or ledger and loads S3 and the index (PartNos and references read with the Document Manager).
4. Page: login, tree, check out an assembly, check in.
5. Tests: desktop end to end on 0806 A; then laptop as engineer 2 (one lock held by each machine, the other machine sees it); then James.

## Machine setup document

Create and keep `docs\setup-machine.md`: everything a new PC needs, in order, with exact commands: git clone, .NET 8 SDK, the app and its settings, `SW_DM_KEY` user environment variable (value never written down), the Document Manager DLL from the SOLIDWORKS install, Claude Code, SOLIDWORKS Referenced Documents check. Grant's laptop and James's PC are set up from it.

## Done when

Grant has checked `LS-08A-06` out and in from the page on the desktop, and the laptop sees the lock while it's held.
