# CLAUDE.md

This repo is a lightweight PDM (check-out / check-in, locking and versioning) for a small SOLIDWORKS team spread across NZ, Hong Kong and mainland China. **Read `SPEC.md` first**: it explains what we're building, why, and the principles behind it. Treat it as intent rather than a rulebook; if something in it doesn't fit what you find, say so and suggest a better way.

## Where we are

**Phase 0: proving tests.** Before building the app, prove the three things most likely to sink it:

1. **China access:** upload to and download from an S3 bucket (with Cognito login) in the Hong Kong region works reliably from mainland China. Joey will run the test from his network, so it needs to be something he can run easily.
2. **Reading SOLIDWORKS files:** the Document Manager library, with our key, lists the references of a real APEX assembly and drawing, and can read and write a custom property.
3. **The `P:` drive:** a permanent SUBST mapping works with SOLIDWORKS: it survives a restart, is visible to all programs, and assemblies open with references intact.

Phase 0 is done when each of these has a short written result (worked / didn't / what we learned) and anything that changes the design is reflected in `SPEC.md`.

**Start by proposing a plan** for Phase 0, including your choice of language for the local app and why, and wait for Grant to agree before building.

## How to work here

- **Small steps, explained simply.** Grant prefers to go step by step, with plain-language explanations and tables where they fit. Summarise at the workflow level; show code detail only when asked.
- **Check before asserting.** Where a platform or API capability matters (AWS, Document Manager, SOLIDWORKS behaviour), verify it rather than assume.
- **Keep `SPEC.md` current.** When a decision changes the design, propose the edit to `SPEC.md` as part of the change.

## A few firm lines

- **Test data only.** Work against the test vault Grant sets up, never the live APEX files.
- **No secrets in the repo.** The Document Manager key and any AWS credentials stay out of git (use environment variables or the Windows user secret store, and keep them out of logs).
- **AWS access via SSO only.** Use short-lived credentials through Grant's IAM Identity Center login, never root or permanent access keys.
- **Ask before anything hard to undo**, such as deleting buckets or data, changing the AWS Organization, or altering permissions for real users.

## Environment

- **Build machine:** Grant's NZ PC, Windows, with SOLIDWORKS 2026 installed. Claude Code runs natively on Windows (not WSL), because the app depends on Windows COM, SUBST and file attributes.
- **Second machine:** Grant's laptop with SOLIDWORKS, used as "engineer 2" for two-person locking tests. Builds move between machines through this private GitHub repo.
- **AWS:** a dedicated child account under Candelic, Hong Kong region (`ap-east-1`, opt-in).
