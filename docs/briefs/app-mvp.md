# Brief for App: the minimum PDM, running today

From Admin, 21 Oct 2026. Report in `app\status.md`; questions to Grant directly, in your window.

## Goal

By tonight, Grant can run the PDM on his desktop, then his laptop as "engineer 2": log in, get the vault down to `P:`, check a file out, edit it in SOLIDWORKS, check it back in, and see on the other machine that it's locked, then updated. Then James (also in NZ) installs it tomorrow and starts drawing 0806 A.

Order: **Grant on the desktop first.** Get one machine working end to end before the second.

## What to build

One command-line app, `pdm`, in C# on .NET 8 (install the SDK first). One S3 bucket in Hong Kong. SSO login. Nothing else today.

| Command | Does |
| --- | --- |
| `pdm login` | Runs the SSO login for the PDM profile (or tells Grant the command to type) |
| `pdm get [folder]` | Downloads what's new or changed from the vault into `P:`; files not checked out by me are set read-only |
| `pdm checkout <file>` | Takes the lock (fails clearly if someone else holds it), makes the local file writable |
| `pdm checkin <file> [-m "note"]` | Uploads the file as a new version, releases the lock, sets it read-only again |
| `pdm undo <file>` | Releases the lock and restores the vault copy |
| `pdm status [folder]` | What I have checked out; what others have checked out (who, since when); what's changed in the vault since my last get |

Design points:

- **Vault keys mirror the `P:` tree:** `APEX/SC08/LS-08X-06/...`. S3 versioning keeps every overwritten file. Block all public access.
- **Locks are S3 objects** under a prefix such as `.pdm/locks/<path>.json` (user, machine, time, note), created with a **conditional write** (`If-None-Match: *`) so two people can't take the same lock. Verify the AWS SDK for .NET supports this before relying on it; if not, say so and propose the fallback.
- **Local state:** a small file under `P:\.pdm\` recording the version or ETag of each file as last fetched, so `get` and `status` know what changed. No database.
- **Settings in a file**, not code: bucket, region, SSO profile, local root (`P:`), user name (from the SSO identity). The map from folders to permissions is for later.
- **Read-only attribute** on files not checked out is the first line of defence; SOLIDWORKS respects it.
- Keep the SPEC's later features out: dashboard, per-folder permissions, Issue, translation, Cognito, DynamoDB.

## AWS setup (with Grant; he does the interactive logins)

1. Confirm the PDM child account under Candelic exists and which SSO profile reaches it. Agree a profile name (`pdm`).
2. Enable region `ap-east-1` in that account (opt-in).
3. Bucket, e.g. `candelic-pdm-apex` (globally unique; add a suffix if taken), region `ap-east-1`, versioning on, public access blocked, default encryption.
4. IAM Identity Center: a permission set `PDM-User` scoped to that bucket (list, get, put, delete objects; no bucket admin), assigned to Grant and James. Grant has admin through `candelic-admin` already.
5. SSO profile on Grant's desktop (and later laptop and James's PC): `aws configure sso` with the PDM account and `PDM-User`.

Ask before creating anything that costs money beyond the bucket, and before anything hard to undo.

## Test, in this order

1. Desktop: `login`, `get` (empty vault), upload the imported 0806 A tree once Dev has it in `E:\PDM-test\P` (a one-off `pdm push` or admin command is fine), `status`.
2. Desktop: `checkout` a part, edit and save in SOLIDWORKS, `checkin`, see the new version in the bucket.
3. Laptop: install from `docs\setup-machine.md`, `login`, `get`, try to `checkout` the file the desktop holds (must fail with who holds it), `checkout` another, `checkin`, `get` on the desktop shows it.
4. Open `LS-08A-06+STRUCTURE` from `P:` on both machines: nothing loaded from outside `P:`.

## Machine setup document

Create and keep `docs\setup-machine.md`: everything a new PC needs, in order, with the exact commands: git clone, .NET 8 SDK, AWS CLI and the SSO profile, `SW_DM_KEY` user environment variable (value never written down), where the Document Manager DLL comes from (the SOLIDWORKS install), `subst P:` (permanent: see SPEC Test 3), Claude Code, SOLIDWORKS Referenced Documents check. Grant's laptop gets set up from it in a few days and James's PC tomorrow.

## Done when

Grant has done tests 1 and 2 on the desktop, and `docs\setup-machine.md` is good enough for the laptop.
