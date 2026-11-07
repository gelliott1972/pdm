# Brief: PDM app, multi-select check-out and removing files from the vault

From Admin, 7 Nov 2026, Grant's request. Owner: **App**. Goes out in the next app version.

## 1. Multi-select on the left

In the tree on the left, select several files (Ctrl-click, Shift-click for a range, and a tick box per row if that reads better) and check them all out together, through the same check-out list as today (with its tick all / none and its progress bar).

## 2. Remove a file from the vault ("Remove from vault")

Why: James has two drawings that were mistakes and asks for them to go:

| File | Replaced by |
| --- | --- |
| `08X06-DBP.SLDDRW` | `08X06-DBP-01.SLDDRW` and `08X06-DBP-02.SLDDRW` |
| `GNRIC-BRW-20.SLDDRW` | `GNRIC-BRW-16.SLDDRW` (it also points at a `GNRIC-BRW-20.SLDPRT` that isn't in the vault) |

Both are currently checked out by James.

How it should work:
- **Soft removal, never a hard delete.** The file is marked removed in the index with who, when and a required reason. It disappears from the tree and is removed from `P:\` on each PC at its next Download latest (nothing local is lost without the same "you have changes" checks as today). Every version stays in S3, so it can be restored. A "Show removed" view with **Restore** brings it back.
- **Checks before removing:** the person removing it has it checked out (or it's free and they check it out as part of the action); no other vault file references it (show the where-used list and refuse if there is one; drawings normally have none); never `.pdm`.
- **Who may remove:** anyone, as with check-in; it's a recorded action and reversible. Tell Admin if you think it should be Grant only.
- Releases (`_Review` etc.) are untouched by this.

## Then

Once it's in a published version, James removes the two drawings himself (he has them checked out), or Grant does after James releases them. Publishing still needs Grant to type "publish x.y.z" in the pdm-app window.

Order in App's queue: after the Desk PDM steps and Pause / Hand back, before the Desk layout tidy-up and the changed-check work.
