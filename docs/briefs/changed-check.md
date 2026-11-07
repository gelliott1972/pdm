# Brief: check in only files that really changed

From Admin, 7 Nov 2026, Grant's request. Owner: **App**. Runs in parallel with 0804 and the release step.

## The problem

James often checks out more than he needs, then uses Save All. SOLIDWORKS rewrites every open file, so every file's bytes change, even where nothing in the model changed. The PDM already compares files by MD5 (`Vault.cs`) and only releases byte-identical ones, but after Save All almost nothing is byte-identical. So everything is uploaded, and syncing takes a long time.

## What Grant wants

- Check-in works out which files **genuinely** changed.
- By default it checks in only those. Files that were only re-saved are listed separately, unticked, with an option to tick and check them in anyway.
- Re-saved-only files are released without uploading, so they cost nothing to sync.

## Step 1: find out what a no-change re-save changes (report before building)

On scratch copies only (never `P:\`), using Dev's `tools\SwCmd` (`open`, `save`) when SOLIDWORKS is free; ask SW, since 0804 is using SOLIDWORKS:

1. Take a few real files of each kind (part, assembly with configurations, drawing, a family part). Copy them, open and save each with no change, and repeat once.
2. Compare before and after at the structure level. SOLIDWORKS files are containers of internal streams: list which streams or sections change on a no-op save (likely save history, timestamps, the preview image), and whether anything else does.
3. Check the opposite case: make a small real change (a dimension, a custom property, a configuration, a component) and confirm the fingerprint below does change.

## Step 2: the fingerprint (only if step 1 shows it's reliable)

- A "content fingerprint" per file: a hash over everything except the parts that change on a no-op save. Stored in the vault index beside the MD5 for each version.
- Check-in: same MD5 means released, as now. Different MD5 but the same fingerprint means "re-saved only": unticked by default, released without upload, and the local copy is noted as matching that vault version. Different fingerprint means a real change, ticked as now.
- The page shows the three groups with counts. A file that can't be fingerprinted counts as changed, so it's never wrongly skipped.

## Rules

- When in doubt, treat the file as changed. Never lose a real change.
- No change to how files are stored, only what is uploaded.
- Report step 1's findings to Admin before building step 2; Admin takes them to Grant on the Desk if a choice is needed.
