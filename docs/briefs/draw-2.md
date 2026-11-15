# Brief: Draw 2, first batch of all outstanding drawings

From Admin, 15 Nov 2026. Grant wants Draw ready to process all outstanding drawings the way the LED members are now done. Owner: **Draw**. Follows `docs\briefs\draw-1.md` (all four steps done; LED members and the BOM rule signed off by Grant 15 Nov).

## 1. What counts as outstanding

Use the same rule as `pdm coverage` and `docs\research\auto-drawings.md` section 1: one item per PartNo; leave out FAST-, MDL- and K-beam skeletons, and PartNos that exist only as As Welded configurations. "Drawn" means a drawing in the vault is named by the PartNo or its title block shows it. Work it out yourself from `P:\` with Document Manager (read-only). (`pdm coverage` needs Grant's AWS login, which had expired at 15 Nov; if Grant logs in, compare your list with it.)

Write the list first to `draw\outstanding.md` (PartNo, file, configuration, type, folder) and send Admin the counts by type before drawing anything. 13 Nov there were 81; 0804 has changed since (plates job).

## 2. Order (one batch per type, one locked SOLIDWORKS round per batch)

| Batch | Type | Why this order |
| --- | --- | --- |
| 1 | LED members (LMW) | Proven and signed off |
| 2 | Plates and packers (PLP) | Proven (PLP-06, PLP-01) |
| 3 | Brackets (BKP) and small assemblies (BKA, MSA) | One bracket and one assembly tried |
| 4 | Single members and simple weldments (SFP, HBP, BRP, HBW, BRW, SFW, MSW) | New: expect fixes on the first runs |
| 5 | Subframe and structure assemblies (SFA, STA) | Slowest (about 3.5 min each); twins drawn fresh, never copied |

Stop after each batch and report to Admin: drawn, failed, what needed fixing, time taken. Grant reviews each batch before the next one starts. If a family needs a new rule, draft one example, let Grant look at it, then run the rest.

## 3. Where to work

**Copies only, in `C:\code\pdm\scratch\draw-test\batch-<n>-<type>\`.** No vault pilot yet: putting drafts into the vault, and how James receives them there, is the next brief (with James, from Monday 16 Nov). Copy models out of `P:\` read-only; never write to `P:\` or `C:\PDM-work`.

## 4. How the drafts are marked and handed over (for now)

- Everything Draw places is on layer DRAFT (magenta), the revision row shows **0A*** (Grant's D5 is still open; keep 0A* until he decides), and model problems are flagged in magenta on the sheet, as in draw-1.
- Each batch folder has the `.SLDDRW` files, a PDF of each, and a `batch.md` list: PartNo, drawing file, status (ready / needs a look / failed), and one line on anything James or Grant should check (flags, guesses, model problems).
- Model problems in vault files (wrong description, missing material, wrong weight, etc.) go into one Desk ticket per batch for Grant, not one per part. SW fixes them later through the normal check-out.

## 5. Rules carried over

Never save a dangling dimension (D1). Check every dimension value against the model, and every hole and slot appears. Dimension assemblies too (D2). Welds stay with James. BOM sorted by part number, then description, on every assembly (Grant 15 Nov). SOLIDWORKS 2026 only, through SwCmd `--as draw`, batched, Close All between sets. Keep `draw\status.md` and the drawing-standards skill current.
