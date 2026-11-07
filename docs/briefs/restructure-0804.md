# Brief: bring 0804 into line with 0806 and 0811, and move files in the vault

From Admin, 7 Nov 2026 (Saturday evening NZ), Grant's decisions in Admin. Owners: **SW** (SOLIDWORKS restructure), **Dev** (moves, repointing, rehearsal, run), **App** (move in the PDM and sync). **Deadline: done and loaded before James's first Download latest on Monday morning NZ.** Grant: "Let's sort out the PDM first"; the 0A PDF release waits until this is done.

## Why

0804 went into the vault on 7 Nov without the structure assembly decided in D4 ("plus a new `08A04-STA-01` etc. as 0811"); Admin missed that it never reached the plan. 0806 and 0811 have, per shaft, top `LS-08x-0n+STRUCTURE` = `08x0n-STA-01` (all the steel, numbered, with a drawing) + the screen. Grout and anchors go into the STA-01 (0811 D7), so 0804 needs it.

## Target layout (Grant 7 Nov)

```
APEX\SC08\LS-08X-04\
  LS-08A-04+STRUCTURE, LS-08B-04+STRUCTURE, LS-08C-04+STRUCTURE   tops, no PartNo
  08A04-STA-01, 08B04-STA-01, 08C04-STA-01                          new, all of that shaft's steel
  LS-08A-04, LS-08B-04, LS-08C-04                                   complete screens stay at the top, as the others
  Screen_Subassemblies\    the 7 walls and roofs (LS-08A-04 LH/RH Side Wall, Rear Wall, Roof; LS-08BC-04 LH/RH Side Wall, Rear Wall; LS-08B-04 Roof; LS-08C-04 Roof)
  ShaftA\ ShaftB\ ShaftC\
  _Shared\                 also 08Z04-SFP-01 (from ShaftBC)
```

| # | Decision (Grant, 7 Nov) |
| --- | --- |
| S1 | Each top = `08x04-STA-01` + its complete screen, as 0806/0811. The STA-01 holds every steel component the top holds today (subframes, K-beam, ties, bolts, packers), positions and mates kept |
| S2 | B and C: `LS-08B-04_WITH_SCREEN` and `LS-08C-04_WITH_SCREEN` are retired; their steel goes into the STA-01, their screen (`LS-08B-04`, `LS-08C-04`) into the top. Reverses R7/D27 |
| S3 | Complete screens stay at the top level; walls and roofs move into `LS-08X-04\Screen_Subassemblies\` |
| S4 | No `ShaftBC` folders: files shared by B and C go in the set's `_Shared`. 0804: `08Z04-SFP-01`. **0811 too**: the 11 files in `LS-08X-11\ShaftBC\` move to `LS-08X-11\_Shared\` |
| S5 | The PDM must handle the moves for everyone ("make sure that the PDM sync deals with moving this") |

STA-01 properties as 0806/0811: PartNo at the Default configuration, `Provisional`, no file-level PartNo (0811 D22, D25); Description and Finish in James's pattern from `08A06-STA-01` / `08A11-STA-01` (Dev proposes, Admin checks). No drawings now; James draws the STA-01s later.

## App: move in the PDM (needed first)

- A **move** (path change) of a vault file: one recorded action with who, when and a note; the file keeps its history and PartNos; no new upload when the bytes are unchanged.
- **Download latest on every PC** applies it: the local file at the old path is moved to the new one (or removed and the new one fetched), with the same "you have local changes" protection as today; empty old folders are removed (`ShaftBC`). A file checked out by anyone can't be moved.
- Moves usually come with changed files (the assemblies that reference the moved file are re-saved with the new paths); one check-in can carry both, or moves first then a check-in. App's call; say which.
- A batch form for Dev's run (a CLI, as `pdm load`), dry run first.
- Retiring the two `_WITH_SCREEN` files: Remove from vault (0.1.9), with a note.
- If any of this needs a new app version for James and Joey before Monday, say so early; publishing needs Grant.

## SW: the SOLIDWORKS restructure (on scratch copies first)

- In each top, select every component except the screen and use **Form New Subassembly**, saved as `08x04-STA-01.SLDASM` in `LS-08X-04\`. B and C: dissolve `_WITH_SCREEN` (its steel into the STA-01, its screen into the top). Check every top configuration.
- Check: SwCheck clean, suppression identical component by component to today's tops (SwSuppDump, new paths mapped), mass the same.
- With SwCmd under the lock; batch the SOLIDWORKS work.

## Dev: moves, repointing, rehearsal, run

- Plan the round as one list: new files (3 STA-01), changed files (3 tops, the 3 screens and every assembly referencing a moved file), moved files (7 walls and roofs, `08Z04-SFP-01`, 0811's 11 `08Z11` files, and their drawings' references), retired files (2 `_WITH_SCREEN`).
- Repoint references to moved files (as the importer and SwFixRefs do), resave in SOLIDWORKS, gate: no link outside the vault, nothing left pointing at an old path. Ledger rows for the moves and the STA-01s.
- **Full rehearsal on a scratch copy of `P:\`**, including App's move and sync on a second test copy, before the real run. Then Grant's go on the Desk.
- Never write to `P:\` outside the agreed run; nobody may have these files checked out (James has only `GNRIC-BRW-20.SLDDRW` and `08X06-DBP.SLDDRW` out; check both are untouched).

## Order

1. App: move + sync design (a few lines to Admin), then build. In parallel SW and Dev: restructure and plan on scratch.
2. Dev: rehearsal of the whole round (SOLIDWORKS, moves, PDM), with App.
3. Admin: Desk go for Grant.
4. Real run, then a second PC's sync checked (Grant's laptop, or a test copy), before Monday.

Stage-end messages to Admin only. Questions for Grant as Desk tickets, plain words.
