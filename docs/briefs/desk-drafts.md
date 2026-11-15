# Brief: reviewing Draw's drafts on the PDM Desk, then loading them into the vault

From Admin, 15 Nov 2026. Agreed with Grant the same day (D8). Owners: **App** (Desk page and server, the vault load), **Draw** (tickets, drafts, acting on answers). Follows `docs\briefs\draw-2.md`.

## What Grant asked for

"Load each batch after I review it. Use the PDM Desk for this: a link to the PDF, and Accept, Review comments and Redo options. This makes it easy for me."

## What Grant sees: a new Desk ticket type `draft-review`

One ticket per drawing, grouped under its batch (e.g. "Batch 2: plates, 7 drawings").

| Part | Content |
| --- | --- |
| Header | PartNo, description, batch, version of the draft (v1, v2...) |
| PDF | The draft's PDF shown on the page (or opened in a new tab with one click), served by the Desk from Draw's folder. The newest version only |
| Notes from Draw | The "needs a look" line from `batch.md`: flags, guesses, model problems |
| **Accept** | This draft is good to go into the vault |
| **Review comments** | A comment box. Grant's comments are added to the ticket's thread for Draw. The status becomes `discuss`, and Draw replies or changes the drawing |
| **Redo** | A comment box (what's wrong). Draw remakes the drawing and posts it as the next version on the same ticket |
| Batch | **Accept all remaining** for the batch, after a confirm |
| Optional | **Open in SOLIDWORKS** (the existing sw-job route), if cheap |

Draw sees the answers in the ticket files as now, acts on them, and sets `done`.

## After a batch is accepted: into the vault

1. **The drawing must point at the vault model.** Drafts are made against copies in `scratch\draw-test`. For loading, Draw remakes or repoints each accepted drawing against the model's own `P:\` path (models opened read-only, nothing saved into `P:\`), confirms zero dangling and every value unchanged, and puts the drawing and its PDF in a load folder per batch (e.g. `scratch\draw-test\load\batch-2\`, with the `P:\` folder structure under it).
2. **App loads it.** App copies the drawings into their `P:\` folders (next to their models, named by PartNo as James's are) and runs `pdm load --note "Draw draft, batch n, reviewed by Grant <date>"`. The drafts arrive as `0A*`, with magenta DRAFT items, for James to accept, remove the *, fill DRAWN and check in.
3. **Release refuses an unsigned draft** (Grant's D5, already asked of App): a * in any REV, an empty DRAWN, or unaccepted magenta/DRAFT items.
4. `pdm load` uses the admin SSO login. If it has expired, App asks Grant via a Desk ticket to run `aws sso login`.

## Order

App builds the `draft-review` ticket type and the release checks first; Draw posts batch 1 (GNRIC-LMW) as the first real use. Until the Desk can show it, Draw keeps going with Grant in its window as now.

## Not in this brief

The automatic learning from James's corrections (later), a "Drafts waiting" list on the PDM page (later; the `0A*` and magenta are the markers for now).
