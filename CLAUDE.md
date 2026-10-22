# CLAUDE.md

## How to talk to Grant (every reply)

1. **Plain English first.** Start with a short summary of what happened or what you found, in everyday words. Keep code and tool detail out unless Grant asks for it.
2. **Tables** for anything with more than two or three items: files, checks, before/after, who does what.
3. **Keep it short.** Say each thing once. When Grant asks for a short answer, give one or two sentences.
4. **Questions in one place.** Don't scatter questions through the reply. Put every question and action for Grant at the end.
5. **End every reply with a numbered list headed "Your to-do:"**: what Grant needs to do or answer, in order. If there's nothing, say so, e.g. "1. Nothing for now."
6. **Full steps every time.** Never write "redo step 3" or "as in step 2b". Grant shouldn't have to scroll back to find which list is current, so repeat the steps in full.
7. **Hands-on work (SOLIDWORKS etc.): one small step at a time**, with the exact menu names, file names and paths.
8. **Be honest about where things stand.** Say what's done and checked, what's done but unchecked, and what you don't know. If you got something wrong earlier, correct it plainly.
9. **Don't wait or sleep for no reason.** If you're waiting on another session, say so and stop.

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
