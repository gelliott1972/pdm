# Brief: PDM Desk v2 (clean dashboard, SOLIDWORKS in use, PDM steps)

From Admin, 7 Nov 2026, Grant's requests. Owners: **App** (Desk), **Dev** (SwCmd lock). Builds on `docs\briefs\desk.md` and `app\desk.md`.

Grant: "What I want is a clean dashboard that allows me to clearly see what I need to do."

## 1. Layout (App)

| Area | Shows |
| --- | --- |
| Top: **Your to-do** | Open tickets for Grant only, blocking first. Each card shows its **ID** (e.g. `SW5`) as the first thing, so it matches what Admin says. One line of context; details expand on click |
| **SOLIDWORKS** strip | Who is using SOLIDWORKS right now (a session, or Grant), what for, and since when; or "Free". A **Pause automatic work** button (see 2). When Grant holds it: "You have SOLIDWORKS", and a **Hand back** button |
| **Sessions**, compact | One row each: name, state pill (Working / Waiting / Idle / Needs you), open tickets it owns (count), **last heard** ("9 min ago", from `desk\activity`). Click a row to expand: doing, waiting on, latest activity detail |
| **History**, collapsed | Answered, done and dismissed tickets, newest first, closed by default; open to see or change an answer |
| Note to Admin | Small, at the bottom |

Keep it quiet: no large headers, no repeated text. Light and dark as now.

## 2. SOLIDWORKS in use, and pausing it safely (Dev + App)

One SOLIDWORKS on the PC, shared by Grant and the sessions. Files in `C:\code\pdm\desk\sw\`:

| File | Written by | Meaning |
| --- | --- | --- |
| `lock.json` `{holder, purpose, since}` | SwCmd (each command, released after), a session for a multi-step round, or the Desk when Grant presses Start on a sw-job | Who has SOLIDWORKS |
| `pause.json` `{requestedAt}` | The Desk (Grant's Pause button) | Grant wants SOLIDWORKS |

Rules:
- **SwCmd** (Dev): before every command, if `pause.json` exists and the caller isn't Grant's Desk job, refuse with a new exit code 4 "paused: Grant has asked for SOLIDWORKS" and touch nothing. A command already running finishes. SwCmd writes and clears `lock.json` around each command (holder = the `--as <role>` argument, default `unknown`).
- **A session doing a round** (SW, Dev, App): holds `lock.json` for the round. On exit code 4 or on seeing `pause.json`, it stops at the next **safe point**: nothing unsaved in SOLIDWORKS (save what its plan says to save, or close-all without saving if the step isn't complete; never leave a half-done change open), then `close-all`, clears its lock, and sets its status to "paused for Grant". It resumes when `pause.json` is gone.
- **The Desk** (App): Pause writes `pause.json` and shows "Waiting for <session> to reach a safe point…" until the lock clears, then "You have SOLIDWORKS". A sw-job Start takes the lock as `grant` (and refuses if a session holds it, offering Pause). **Hand back** (or Done/Problem on the job) clears Grant's lock and `pause.json`.
- Nothing here guards against Grant using SOLIDWORKS directly without the Desk. That's fine; the strip just shows "Free" or who has it.

## 3. PDM actions as sw-job steps (App)

A step may be `{text, action: {type: "download-latest" | "check-out" | "check-in", paths: [...], note?}}`. On **Next**, the Desk calls the PDM app's local endpoints as the logged-in engineer (no AWS in the Desk), shows the per-file result, and moves on only on success; the result is kept in the ticket. Check-in asks for a confirm on the page. PDM not running or not logged in: "Open PDM and log in, then press Next again". Paths must be in the vault index under `P:\`; never `.pdm`. First live test: SW5 (check out FAST-HEX, FAST-NUT and FAST-WSH-8.8-HDG).

## Order

1. Ticket IDs on cards, and the SOLIDWORKS strip showing who has it (read-only), today.
2. PDM action steps (for SW5).
3. Pause / Hand back, with Dev's SwCmd lock and exit code 4.
4. Layout clean-up (compact sessions, collapsed history).
The changed-check job (`docs\briefs\changed-check.md`) waits until these are done.
