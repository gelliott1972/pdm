# APEX Desk (was PDM Desk): file formats (for every session)

Briefs: `docs\briefs\desk.md`, `desk-v2.md`; stage 2: `C:\code\apex\docs\briefs\apex-desk-stage2.md` (APEX-A71). Code: `src\PdmDesk\`. Owner: App.
The page is named from settings ("APEX Desk"). The exe, its folder, the data folder, the variables (`PDM_DESK`, `PDM_DESK_PORT`, `PDM_DESK_DIR`, `PDM_SWCMD`), the `X-Desk` header and `/api/ping` keep their PDM names.

## Running it

| What | Where |
| --- | --- |
| Start | Start menu → **PDM Sessions** (`C:\code\pdm\start-pdm.cmd` runs `%LOCALAPPDATA%\PDM-Desk\PdmDesk.exe`), or open that exe directly. It opens `http://127.0.0.1:5180/`; if the Desk is already running, it just opens the page. |
| Program | `%LOCALAPPDATA%\PDM-Desk\PdmDesk.exe` (no window; keeps running until logoff or until it's stopped) |
| Install / update | `powershell -NoProfile -ExecutionPolicy Bypass -File src\PdmDesk\install.ps1` (stops a running Desk, rebuilds; makes no Start menu shortcut). It doesn't start the Desk again: PDM Sessions or the exe does |
| Settings | `%LOCALAPPDATA%\PDM-Desk\settings.json` beside the exe, or the file named by `DESK_SETTINGS`. Read at start: a change needs a Desk restart, and open tabs reload. `install.ps1` copies the template `src\PdmDesk\settings.default.json` there only if the file is missing, never over it. No file or a missing field: the built-in values (as the PDM Desk was). An unreadable file: built-in values, and the reason on the page. `PDM_DESK` and `PDM_DESK_PORT` (tests) win over the file |
| Data | `C:\code\pdm\desk\` (gitignored; settings `dataFolder`): `tickets\`, `status\`, `inbox\`, `activity\`, `sw\` |
| Safety | Listens on 127.0.0.1 only; answers only requests whose Host is 127.0.0.1/localhost; changes need the `X-Desk: 1` header, so other web pages can't post to it |
| Test copy | `PDM_DESK=C:\code\pdm\scratch\desk-test`, `PDM_DESK_PORT=5181`, never the live folder |

### Settings fields

| Field | Built-in value | What |
| --- | --- | --- |
| `deskName` | `"PDM Desk"` | The page title, heading, tab title and banners (installed: `"APEX Desk"`) |
| `dataFolder`, `port` | `C:\code\pdm\desk`, `5180` | |
| `listenAddresses`, `allowedHosts`, `auth.mode`, `user` | `["127.0.0.1"]`, `["127.0.0.1","localhost"]`, `"none"`, `"grant"` | Remote-ready plumbing (stage 2 item 8): see "Remote later" below. Only `none` and loopback today |
| `fileRoots` | `C:\code\pdm\scratch\`, `C:\code\pdm\draw\` | The only folders the Desk serves files from (draft PDFs, attachments). Admin agrees any new root; never `C:\code\apex` or `data\` |
| `register` | none | `workflow`, `screenTypes`, `screens`: the APEX register CSVs the board reads (never writes) |
| `sessionGroups` | one group "PDM": admin, dev, sw, app, draw | `[{ name, sessions: [{ id, label, prefix?, remote?, via? }] }]`: the session list, in order, with headings |
| `steps` | none | Board columns in order: `{ key, label, kind?, ruleBefore? }`. `kind`: `pin`, `gate`, or none (depends on the model). `ruleBefore` draws a light rule before the column |
| `gates` | none | `{ key, label, outcomes: [{ key, label, passes?, noteRequired? }] }`, or a code gate `{ key, label, "input": "code", codes: [{ code, label, passes?, noteRequired? }] }` (WDI, APEX-A78: while `codes` is empty, any whole number is taken) |
| `noticeRoutes` | none | `{ "owner": "session" }`: answer notices for an owner with no session on this PC go to that session (installed: `{"revit-fiona": "apex-admin"}`). See "Answer notices" below |

Without `register`, `steps` and `gates` the board and gate answers aren't shown.

## Writing files

- One JSON object per file, UTF-8.
- **Write whole files:** write `<name>.json.tmp` (or any name not ending in `.json`), then rename it over `<name>.json`. The Desk reads only `*.json`.
- File name = id: `tickets\D16.json` has `"id": "D16"`. Ids use letters, digits, `.`, `_` and `-` only.
- Times are UTC ISO, e.g. `2026-11-07T03:10:00Z`. The page shows them in local time.
- Body and option notes are plain text. Line breaks are kept; `**bold**` and `` `code` `` are shown as such.
- No secrets.

## `tickets\<id>.json`

| Field | Values | Notes |
| --- | --- | --- |
| `id` | `"D16"`, `"apex-admin-14"` | Same as the file name. The id rule: `<session prefix><number>`, the prefix being the session's `prefix` in settings; for the APEX sessions that is the full session name and a dash (`apex-admin-1`, `calc-08a-11-2`). Existing PDM ids (`A`, `SW`, `DR`, `DW`) stay. If the owner has a prefix and the id doesn't start with it, the page says so under Sessions; nothing is refused. Never rename an existing ticket |
| `type` | `decision`, `info`, `sw-job`, `draft-review`, `gate` | `sw-job`, `draft-review` and `gate` have their own sections below |
| `screen` | `"LS-08A-06"` | Optional: the register's `ls` value (never part of the id). A second chip on the card, in History and on `/review`; clicking it shows only that screen's open tickets |
| `decision` | `"APEX-A62"` | Optional: the decision the ticket produced, shown in its History row |
| `attachments` | `[{ "key": "summary", "label": "Check summary", "path": "C:\\…\\summary.pdf" }]` | Optional: links on the card, served by id at `/api/attachment?id=<id>&key=<key>` only if the full path is under a `fileRoots` folder, exists and ends `.pdf`, `.png` or `.jpg`. The path never goes to the browser |
| `ref` | `"D16"` | Optional chip before the title |
| `title` | text | One line |
| `body` | text | Short context |
| `options` | `[{ "key": "A", "label": "…", "note": "…" }]` | `decision` only. Leave it out (or empty) for a free-text answer |
| `recommended` | an option `key` | Marks it and shows **Accept recommendation** |
| `priority` | `blocking`, `later` | Blocking tickets are listed first, then oldest first |
| `owner` | a session id: `admin`, `dev`, `sw`, `app`, `draw`, `apex-admin`, … | The session that acts on the answer |
| `createdAt` | UTC ISO | |
| `status` | `open` → `answered` → `done`; `open` → `discuss` → `answered`; `info`: `open` → `dismissed` | The page sets `answered`, `discuss` or `dismissed`. The owner sets `done` after acting |
| `answer` | `{ "choice": "A", "note": "…" }` | Set by the page. `choice` is `""` when Grant only typed a note |
| `answeredAt` | UTC ISO | Set by the page |
| `snoozedUntil`, `snoozedAt`, `snoozedBy` | UTC ISO, UTC ISO, `"grant"` | Snooze (Grant, 28 Nov): set by the page's Snooze menu on an open card (1, 2, 4, 8 hours or 1 day; `POST /api/snooze {id, hours}`). Until `snoozedUntil` the ticket is out of the list, the counts, the draft Review count and blocking notices, listed under "Snoozed (N)" with Wake now (`hours` 0: `snoozedUntil` removed, `wokenAt` set). Status and answer are untouched. A session's own check should show it as "snoozed until <time>", not waiting. An owner that rewrites its ticket keeps these fields; removing `snoozedUntil` wakes it |
| `history` | `[{ choice, note, answeredAt }]` | Set by the page when Grant uses **Change answer**: the earlier answers, oldest first |
| `thread` | `[{ "from": "grant", "text": "…", "at": "…" }]` | `decision` only: the discussion, oldest first. The page adds Grant's comments; the owner adds its replies (`from` = its role) |

**Other, let's discuss** (built 7 Nov): every decision ticket offers it as a last choice with a comment box. Grant's comment is added to `thread` as `{ "from": "grant", … }` and the status becomes `discuss`. The ticket stays in the open list, marked "Waiting for <owner> to reply", sorted last and not counted as waiting for Grant.

- **The owner watches** for `status: "discuss"` with the last `thread` entry from `grant` (the same way as `answered`; compare the entry's `at` to know what's new).
- **To reply**, it appends `{ "from": "<its role>", "text": "…", "at": "<UTC ISO>" }` to `thread` and leaves `status` as `discuss`. Write the whole file atomically (temp file, then rename), as for `done`. The owner may also update `body`, `options` or `recommended` if the discussion changes them, and should say so in the reply.
- **The page** shows the thread on the card with "<Owner> replied. Reply again, or pick an option to decide." Grant can reply again (another `grant` entry), or pick an option and Send, which sets `answered` as usual and keeps the thread.
- Plain words in replies, no bare codes, short. One question per ticket; if the discussion splits, open a new ticket.

**Change answer:** Grant can change an answer from the Sent list, even after the owner set `done`. The ticket goes back to `status: "answered"` with the new `answer`, and the old one moves to `history`. So owners should watch for `answered` and compare `answeredAt`, not assume each ticket is answered only once.

Example:

```json
{
  "id": "D16", "type": "decision", "ref": "D16",
  "title": "Finish for SFA brackets",
  "body": "James changed SFA/HBA to HDG & PAINTED on 6 Nov. Keep it?",
  "options": [
    { "key": "A", "label": "Keep HDG & PAINTED", "note": "Matches the drawings" },
    { "key": "B", "label": "Back to HDG", "note": "As before 6 Nov" }
  ],
  "recommended": "A", "priority": "blocking", "owner": "admin",
  "createdAt": "2026-11-07T03:10:00Z", "status": "open"
}
```

## `status\<session>.json`

`{ "name": "dev", "state": "working", "doing": "…", "waitingOn": "…", "updatedAt": "…" }`, where `state` is `working`, `waiting`, `blocked` or `idle`. Each session writes only its own file, at each stage change.

Optional, for a status written on another session's behalf (Revit (Fiona), written by apex-admin): `"relayedBy": "apex-admin"` (the row reads "via APEX Admin" with the file's time, and its pill always comes from `state`) and `"withOthers": "With Fiona: 3 open, 1 blocking"` (shown under "Waiting on").

The page lists the sessions from settings `sessionGroups`, a heading per group, in that order (installed: PDM, then APEX). A session with a status or activity file that is in no group is shown last under "Other"; nothing is hidden. A session with an activity file (the PDM hooks) takes its pill from it, as before. One with no activity file takes the pill from its status file (Working, Waiting, Blocked, Idle) with "as of <time>". A session marked `remote` with no files reads "Messages only (through <via>)".

## `inbox\<id>.json` (written by the page)

`{ "id": "20261107-110227-5706", "text": "…", "createdAt": "…" }`: Grant's note to Admin. Admin may add `"readAt"` once it's read; the page then shows "read by Admin" under the note.

## `sw-job` tickets (phase 2, built 7 Nov)

Same common fields (`id, type: "sw-job", ref, title, body, priority, owner, createdAt, status`), plus:

| Field | Example | Notes |
| --- | --- | --- |
| `files` | `[{ "path": "P:\\APEX\\GNRIC\\GNRIC-LM.SLDPRT", "configuration": "250+3000+250_SHS_40X40" }]` | Opened in order on **Start**. Full paths to .SLDPRT, .SLDASM or .SLDDRW files that exist; nothing under `.pdm`. `configuration` is optional; the last file opened is left active |
| `closeAll` | `true` (default) | Start first closes all documents **without saving**, after a confirm on the page. `false`: only opens |
| `steps` | `["Open the cut list…", "Set Finish to HDG…"]` | Shown one at a time with Back / Next (`**bold**` and `` `code` `` work). A step can also be a PDM step (below) |
| `files` | `[]` | May be empty for a job that only has PDM steps; then there's no Start button |
| `actions` | `{ "1": { "at", "type", "ok", "by", "files": [{ "path", "ok", "why", "log" }], "log": [...] } }` | Set by the page: the last result of each PDM step, by step index |
| `step` | `0` | The step showing; the page updates it on Next/Back, so a reload keeps the place |
| `startedAt` | UTC ISO | Set by the page on Start |
| `run` | `{ "at", "ok", "problems", "message", "output" }` | Set by the page after Start: `ok` false means it stopped (refused, SOLIDWORKS error or timeout); `problems` counts calls that finished with problems (exit 1); `output` is SwCmd's full report (last 12000 characters) |
| `answer` | `{ "choice": "done", "note": "" }` or `{ "choice": "problem", "note": "…" }` | **Done** and **Problem** set `status: "answered"`; the owner checks the work, then sets `done`. Change answer works as for decisions (`history`) |

Status flow: `open` → (Start) `started` → (Done / Problem) `answered` → (owner checked) `done`. Start can be pressed again while `started`.

**What Start runs** (agreed with Dev 7 Nov; SwCmd at `C:\code\pdm\tools\SwCmd\bin\SwCmd.exe`, or the `PDM_SWCMD` variable): `SwCmd.exe close-all` (never `--exit`; skipped when `closeAll` is false), then `SwCmd.exe open "<file>" [--config "<name>"]` for each file in order. Exit 0 is clean, and 1 means done with problems (carries on); 2 or 3 stops the job. The first call gets 5 minutes (it may have to start SOLIDWORKS) and later ones get 3; on a timeout the Desk stops SwCmd and leaves SOLIDWORKS alone. One job runs at a time. Arguments are passed directly, never through a shell, and tickets never carry commands. Nothing in these calls saves.

## PDM steps in a `sw-job` (Desk v2, 7 Nov)

A step can be an object instead of text:

```json
{ "text": "Check out the three fastener files.",
  "action": { "type": "check-out", "paths": ["P:\\_Shared\\_Fasteners\\FAST-HEX-8.8-HDG.SLDPRT", "_Shared/_Fasteners/FAST-NUT-8.8-HDG.SLDPRT"] } }
```

| Field | Values |
| --- | --- |
| `action.type` | `download-latest`, `check-out`, `check-in` |
| `action.paths` | Vault files, as `P:\...` or vault keys (`APEX/...`). Each must be in the vault's file list. Nothing outside `P:\`, no `.` or `..`, never `.pdm` |
| `action.note` | Required for `check-in`: the check-in note |

When Grant presses **Next** on such a step (or **Run** if it's the last step), the Desk asks the PDM app that's running on this PC (`http://localhost:5170/`) to do it, as the engineer logged in there. The Desk has no AWS access of its own. Check-in asks for a confirm on the page first. Afterwards the Desk re-reads PDM's file list and checks every file:

| Action | Counts as done when |
| --- | --- |
| download-latest | the PC has the vault's latest (files checked out to you are left alone, as Download latest does) |
| check-out | it's checked out to you on this PC |
| check-in | it's no longer checked out to you (checked in, or released with no changes) |

It moves to the next step only if every file is done. Otherwise it stays on the step, lists each file that isn't done and why (for example "checked out by James", with PDM's own message), and Next tries again. The result is saved in the ticket's `actions`. If PDM isn't running, or nobody is logged in, the page says "Open PDM and log in, then press Next again." Back/Next can't skip past a PDM step that hasn't succeeded.

## `draft-review` tickets (Grant's D8, 15 Nov; brief `docs\briefs\desk-drafts.md`)

One ticket per Draw draft, owner `draw`, id prefix `DR` (e.g. `DR12.json`). Fields agreed with Draw on 15 Nov. Common fields as above (`id, type, owner, priority, body, createdAt, status`), plus:

| Field | Value | Notes |
| --- | --- | --- |
| `batch` | `"1"` | The page groups drafts by it, under one header per batch |
| `batchTitle` | `"Batch 1: LED members GNRIC-LMW, 10 drawings"` | The header text (from any ticket of the batch) |
| `partNo` | `"GNRIC-LMW-01"` | Shown first in the title |
| `title` | the model's description | Shown after the PartNo |
| `version` | `1`, `2`, … | Shown as "Draft v2" |
| `pdf` | full path to the newest version's PDF | Served by the Desk at `/api/pdf?id=<id>` only if it's under a settings `fileRoots` folder (`C:\code\pdm\scratch\`, `C:\code\pdm\draw\`) and ends `.pdf`. The page is only told that there is one. Each version gets its own file (e.g. `GNRIC-LMW-01 v2.pdf`) |
| `body` | the sheet's CHECK flags, as on the sheet, and one "needs a look" line | |
| `drawing` | full path to the `.SLDDRW` | Optional (for a later "Open in SOLIDWORKS") |
| `postedAt` | UTC ISO | When this version was posted (shown instead of `createdAt`) |
| `thread` | as for decisions | Review comments (`from: "grant"`) and Draw's replies (`from: "draw"`) |
| `answer` | `{ "choice": "accept" \| "redo", "note": "…" }` | Set by the page. `redo` always has a note |
| `history` | `[{ "version", "choice", "note", "answeredAt" }]` | Earlier answers: Change answer on the same version, and Draw's move of the redo answer when it posts the next version |

What the page does:

| Grant presses | Ticket afterwards |
| --- | --- |
| **Open PDF** / **Show PDF here** | Nothing changes (the PDF opens in a new tab, or on the card) |
| **Accept** (note optional) | `status: "answered"`, `answer.choice: "accept"`, `answeredAt` |
| **Send comment** | Comment added to `thread`, `status: "discuss"`. The card stays, marked "Waiting for Draw to reply" |
| **Redo** (note required) | `status: "answered"`, `answer.choice: "redo"` with the note |
| **Accept all remaining (n)** on the batch header, then **Yes** | Every `open` or `discuss` draft of that batch as Accept |
| **Change answer** (History) | The card reopens; the new answer replaces the old, which moves to `history` |
| **Undo** (Review screen) | Only while `status` is `answered` on the same `version`: `answer` and `answeredAt` removed, `status` back to `open` (or `discuss` if there's a thread). Refused once Draw has set `done` or posted a new version |

**Review screen** (`/review`, Grant 15 Nov): the big **Review drafts (n waiting)** button at the top of the Desk opens it. One draft at a time: the PDF fills the window (the browser's own viewer, fit to width, zoomable), a side bar shows PartNo, description, "3 of 10", batch, the body lines (CHECK lines in amber), "Redone after" and the thread. **Accept** (key A) or a note and **Redo** (Enter); both save through the same `/api/draft` and move to the next waiting draft. Back / Next (arrow keys) move without answering; U undoes the last answer. No comments on this screen. It lists drafts waiting for Grant (`open`, or `discuss` with Draw's reply last), by batch then PartNo; drafts Draw posts while it's open join the end. Ends with "All done: n accepted, m to redo".

What Draw does: read the ticket again just before acting on an answer (Grant can Undo for a short while). After Accept, set `done` once noted for loading. After Redo (or a comment that needs a change), post the next version on the same ticket: `version` + 1, `pdf` = the new file, `postedAt`, `status: "open"`, and move the answer into `history` (`{version, choice, note, answeredAt}`) with `answer` and `answeredAt` removed; the thread stays. To reply to a comment, append `{ "from": "draw", "text", "at" }` and leave `status: "discuss"`. Write the whole file atomically (temp file, then rename).

## `gate` tickets (stage 2, item 6; posted by apex-admin)

One ticket per screen and gate (green light, internal sign-off, WDI code, RSE sign-off). Common fields as above, plus:

| Field | Example | Notes |
| --- | --- | --- |
| `gate` | `"green-light"` | A `key` from settings `gates` |
| `screen` | `"LS-08A-06"` | Required |
| `pin` | `"2026-11-23T05:05:47Z"` | Required: the pin the summary was written at |
| `title`, `body` | the summary and apex-admin's recommendation | As other tickets |
| `recommended` | `"accept"` | An outcome key; marks that button |
| `attachments` | as above | Optional |
| `priority` | normally `blocking` | |

The card shows a "Gate" pill, the screen chip, the pin, the body, the attachments, one button per outcome from settings (for the WDI code gate: one per listed code, or a box for a whole number while the list is empty), a note (required where the outcome says `noteRequired`) and "Other, let's discuss" (the thread, as for decisions). If the screen's steel (`steel_since` on the board) is later than the ticket's `pin`, it says "The steel has changed since this was asked" and asks for a confirm before a passing answer.

Set by the page (`POST /api/gate { id, choice, code?, note, confirmed }`): `answer: { "choice": "accept", "code"?: 2, "note": "", "pin": "<the ticket's pin>", "screenPin": "<the screen's pin when answered>", "by": "grant" }`, `answeredAt`, `status: "answered"`. An earlier answer moves to `history` with the same fields plus its `answeredAt`. Change answer works from History.

**What the answer does and doesn't do:** it writes the ticket only. The Desk never writes the register. apex-admin sees the answer, updates the gate's row in `workflow.csv` (state, pin, date, `by` Grant) and what follows, then sets the ticket `done`. Until then the board shows the answer over the register's cell (the latest gate ticket for that screen and gate, by `createdAt`):

| Ticket | Register row | Board cell |
| --- | --- | --- |
| `open` or `discuss` | not `done` | "Waiting for you" (amber); clicking it opens the ticket |
| `answered`, passing outcome | not yet `done` at that pin | "Accepted, not yet in the register" (green outline) |
| `answered`, other outcome | unchanged | "Not accepted, not yet in the register" (amber outline) |
| `answered`, a code with no meaning set yet | not yet `done` at that pin | "Code 2, not yet in the register" (grey outline) |
| `done` | whatever it says | The register's cell, nothing added |
| `answered` for more than 15 minutes | | Same cell, plus the line "LS-08A-06 Green light: answered, register not updated" |

## The board: "Where things are up to" (stage 2, item 5)

`GET /api/board` (the main page asks every 10 seconds). Read from settings `register`: `workflow.csv` (`ls, step, state, pin, steel_since, date, by, note`), `screen-types.csv` (`ls`, `description`, and `vault_version`, of which only the leading "vault as of yyyy-mm-dd hh:mm:ssZ" is read) and `screens.csv` (`vm`, `ls`). They are opened read-only, with sharing, and never written. Only apex-admin edits `C:\code\apex\data\`.

- One row per screen type that has rows in `workflow.csv`, in `screen-types.csv` order; "n screen types not started" lists the rest. Columns are settings `steps`.
- A cell shows a word and colour (Done, Running, Waiting, Blocked; "–" not started) and the pin as local time. Hover or click shows the full UTC pin, date, by and note.
- **Out of date** (grey, amber dashed border, old pin struck through): a step with no `kind` whose state is done, running or waiting, whose pin is earlier than the screen's `steel_since` (or the pin row's pin when that's blank). Compared as times. Gates never go out of date: given on older steel they keep their state and are marked "given on older steel" (APEX-A75), and apex-admin asks again with a new gate ticket.
- Not guessed: an unknown step or state, a pin that isn't a UTC time, or a pin row that disagrees with `vault_version` each give a line under the board, and the cell shows "?".
- A file missing, held open by another program or not parsing: the last good board stays, with a line saying so (file name only).

## Notices (stage 2, item 7)

**Notices: off/on** in the header. Off until Grant presses it once (that click lets the browser play sound and asks for the notice permission). Then a new blocking ticket waiting for Grant, or a session turning to "Needs you", gives one short sound and a desktop notice (the Desk's name; id, screen, title). Clicking it brings the tab forward. Nothing on the first load; ids already announced are kept in the browser, so a reload doesn't repeat them. Limits: it works only while a Desk tab is open, and desktop notices need a page the browser counts as secure (127.0.0.1 is; a plain `http://` address over Tailscale later isn't).

## Answer notices (24 Nov, Admin): the owner is told when Grant answers

When Grant's action on the Desk sets a ticket to `answered` or `discuss`, the Desk writes one notice for the owning session. The actions are answer, gate, discuss, draft Accept/Redo/comment, Accept all remaining (one notice per ticket) and SOLIDWORKS job Done/Problem. A second discuss comment gives a second notice. Undo, dismiss and the owner's own edits give none.

- **Where:** `notices\<session>\<yyyyMMddTHHmmssfff>Z-<ticket id>.json`, written whole (temp file, then rename). `<session>` is the ticket's `owner`, or its `noticeRoutes` entry (`revit-fiona` goes to `apex-admin`). An owner that isn't a plain session name (`a-z`, `0-9`, `-`) gets no notice and a line on the page's problems line; the answer itself is still saved.
- **Fields:** `ticket`, `title`, `type`, `owner`, `session`, `status` (`answered` or `discuss`), `choice`, `choiceLabel` (the option's label, the gate outcome's label, `Code 3`, `Accepted`, `Redo`, `Done`, `Problem`), `code` (code gates), `note` (the answer's note, or the discuss comment), `by`, `at` (UTC), `screen`, `gate`, `summary` (one line, at most 400 characters).
- **Never** in `/api` output, and no file paths in it. Only the counts of unread ones are (`noticeLag`, below).
- **Watch (every session runs this once, with the Monitor tool; persistent):**

  `powershell -NoProfile -ExecutionPolicy Bypass -File "C:/Users/grant/AppData/Local/PDM-Desk/desk-notices.ps1" -Session <your session id>`

  It prints `Watching Desk notices for <session> ...`, then, every 5 seconds, one line per new notice, e.g. `Desk: A16 (Green light for LS-08A-06) answered by grant: Green light. Note: ...`. Each notice is moved into `notices\<session>\seen\` before it is printed, so it is told once, also with two watches running. The full notice stays in `seen\` to read. Options: `-Desk <folder>` (default: the installed settings' `dataFolder`), `-Every <seconds>`, `-Once` (one pass, then exit).
- **After a notice:** read the ticket as before, act, set `done`.
- **Housekeeping:** at start the Desk deletes `seen\` notices more than 30 days old.

### Keeping the watch running (26 Nov, Admin)

After the 26 Nov morning restart no session started its watch again, and answers sat unread for hours. Three layers now:

| Layer | What it does |
| --- | --- |
| SessionStart hook (startup, resume, clear, compact) | `python -I "$LOCALAPPDATA/PDM-Desk/desk_notices_hook.py" <session>` adds context telling Claude to start the watch now with Monitor (the exact command, `timeout_ms` 1800000, re-arm on every expiry) and how many answers are already waiting. After compact or clear it says to leave a watch that is still running |
| UserPromptSubmit backstop (same command, synchronous, 5 s timeout) | Quiet unless a notice has waited 5 minutes or more. Then: "N answers to your tickets unread, the oldest for M min, so your Desk notice watch isn't running. Start it again now and act on them", with the command |
| The Desk page | `/api/state` field `noticeLag`: `[{ session, count, oldestMinutes }]` for each session whose oldest unread notice is 10 minutes old or more (`notices\<session>\*.json`, not `seen\`). The page shows an amber line under the header, e.g. "APEX Admin hasn't picked up 3 answers (oldest 25 min)". Like the cards, it doesn't change while Grant is typing |

The hook script is installed beside the Desk by `install.ps1` (source `src\PdmDesk\desk_notices_hook.py`). It reads the data folder as the watch does (`PDM_DESK`, else `dataFolder` in settings.json beside it, else `C:\code\pdm\desk`), never fails the hook (errors swallowed, exit 0) and prints nothing for an unknown event or a bad session name. The hooks go in each session's `.claude\settings.json` (PDM: admin, dev, sw, app, draw), set up by Admin.

## Remote later (stage 2, item 8: built, switched off)

- The Desk listens on each of settings `listenAddresses`, and always on 127.0.0.1 (Start again uses it). It answers only Hosts in `allowedHosts`; every POST needs `X-Desk: 1`.
- Then one point, `Authorise`, gives each request its user or refuses it. In `auth.mode` `"none"`, the only mode built, the user is settings `user` ("grant") and nothing is refused, except that the SOLIDWORKS and PDM-step endpoints (`/api/sw/*`) only answer a connection from this PC.
- **Fail closed:** a listen address other than this PC while `auth.mode` is `"none"`, any other mode, or an address that isn't an IP address, and the Desk refuses to start, writing the reason to `desk\desk-error.txt`. A good start deletes that file.
- No local paths go to the browser: `/api/state` has no `root`. `pdf` and `drawing` are sent as `true` (the PDF is served by id), `path` is removed from attachments, and error and problems text lose their folders. sw-job `files[].path` and step `paths` stay (vault paths Grant reads; those jobs are this-PC-only).

**Before remote is ever switched on**, these free-text fields can still carry local paths and should be checked or filtered: ticket `body` text; sw-job `run.output` (SwCmd's report); status files' `doing`, `waitingOn` and `withOthers`; activity files' `detail`; the SOLIDWORKS lock's `purpose`. Also needed then: a sign-in mode (not built), HTTPS or a page the browser counts as secure for notices, and a decision on the sw-job `P:\` paths.

## Who has SOLIDWORKS: Pause and Hand back (Desk v2, 7 Nov)

Files in `desk\sw\` (format from Dev's SwCmd: `lock.json` `{holder, purpose, since, kind: "command" | "round", pid}`, `pause.json` `{requestedAt, by}`).

| Desk does | How |
| --- | --- |
| Shows who has it | The strip under the header: free, a session (what for, since when), "waiting for <session> to reach a safe point…" or "You have SOLIDWORKS". A one-command lock whose SwCmd process has gone is shown as free |
| Pause automatic work | Writes `pause.json`. SwCmd then refuses the sessions' commands (exit 4), except save / close / close-all for a round's holder, so it can reach a safe point |
| Start on a job | `SwCmd lock --as grant --purpose <ticket id>` first. Exit 5 (a session holds it) means "Press Pause automatic work, wait for You have SOLIDWORKS, then Start again". Then `close-all --as grant` and `open … --as grant` |
| Hand back (or Done / Problem on a job) | Deletes `pause.json` and runs `SwCmd unlock --as grant` |
