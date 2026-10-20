# Brief for Dev: the importer, first run on LS-08A-06 (shaft A)

From Admin, 20 Oct 2026. Report progress in `dev\status.md`; questions in `dev\questions.md`.

## Goal

Bring screen 06 shaft A (`LS-08A-06`) into a **test vault** with every file renamed, in the right folder, numbered, and with all references intact, so James can start drawings in the PDM. Shafts B and C follow, then screen 11 (`LS-08X-11`). Build it for 0806 A first, but don't hard-code that screen.

## Inputs

| Input | Where |
| --- | --- |
| Source files | The prepared Pack and Go from SW: `E:\PDM-work\LS-08A-06-r4` (coming). Until then develop against `E:\PDM-work\LS-08A-06-r3`. |
| Import plan | `tools\importplan\make_plan.py` produces `scratch\import\<screen>-import-plan.xlsx` from a probe inventory. You own and can change both. |
| Inventories | `scratch\inventory\<screen>` (from `tools\DmProbe`) |
| Decisions | `docs\imports\LS-08A-06-decisions.md` (the authority for this import), `docs\pending-spec-changes.md`, `docs\part-numbering.md` |
| Number ledger | `data\rename-ledger.csv` (header only so far) |

## What the import must do

1. **Copy each file into the vault tree under its new name** (folder = place; see SPEC.md "Projects, shared areas and folders", with `STOCK` now `GNRIC`). Files the plan marks "leave out" don't go in.
2. **Write `PartNo`** at the level James uses (file or configuration). Numbers come from the files (James's, the authority); where a file has none, from the sample register, and then also write **`PartNoStatus = Provisional`** at the same level (decision 9).
3. **Repoint every reference** to the new names and folders, including:
   - renamed files (most of them);
   - the retired 2-size interface plate, replaced by the 4-size file, configuration for configuration (decision 1);
   - family files renamed or split: members → `GNRIC-LM` (not split), LCMM → `08X06-LMW`, ties → `GNRIC-BRP` / `GNRIC-BRA` (SW is preparing the split files; agree with SW who repoints what);
   - shared files go into the vault once, at their level, and every user points at that one copy.
4. **Drawings travel with their model** and take its new name.
5. **Record every rename and number** in `data\rename-ledger.csv`, so numbers aren't allocated twice.
6. **Report** what was done and anything that failed, in plain language.

Not in scope: deciding numbers or names (ask), live Dropbox files (never), linked equation files (none left in 0806).

## Prove first

Test 2 left two things unproven, and the importer depends on them. Do them on copies, before building on them:

- Write a custom property with the Document Manager and confirm SOLIDWORKS sees it.
- Repoint a reference with `ReplaceReference` and confirm the assembly opens with the component found and the right configuration.

SPEC.md (Bringing files in, step 2) also mentions driving SOLIDWORKS' own Pack and Go through its API. Compare that with the Document Manager approach and **propose one to Grant before building**.

## Open, ask Grant

- **Test vault location:** suggested `E:\PDM-test\P\`, which can later be mapped as `P:` for Test 3.

## Done when

- Grant opens `LS-08A-06+STRUCTURE` and `08A06-STA-01` from the test vault in SOLIDWORKS: nothing missing, nothing pointing outside the vault, the BOM shows the right PartNos.
- The ledger lists every rename and number, and Admin has reviewed the run.
