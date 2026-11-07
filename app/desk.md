# PDM Desk: file formats (for every session)

Brief: `docs\briefs\desk.md`. Code: `src\PdmDesk\`. Owner: App.

## Running it

| What | Where |
| --- | --- |
| Start | Start menu → **PDM Desk** (opens `http://127.0.0.1:5180/`). If it's already running, the shortcut just opens the page. |
| Program | `%LOCALAPPDATA%\PDM-Desk\PdmDesk.exe` (no window; keeps running until logoff or until it's stopped) |
| Install / update | `powershell -NoProfile -ExecutionPolicy Bypass -File src\PdmDesk\install.ps1` (stops a running Desk, rebuilds, remakes the shortcut) |
| Data | `C:\code\pdm\desk\` (gitignored): `tickets\`, `status\`, `inbox\` |
| Safety | Listens on 127.0.0.1 only; answers only requests whose Host is 127.0.0.1/localhost; changes need the `X-Desk: 1` header, so other web pages can't post to it |

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
| `id` | `"D16"` | Same as the file name |
| `type` | `decision`, `info`, `sw-job` | `sw-job` is phase 2 (the page shows it read-only for now) |
| `ref` | `"D16"` | Optional chip before the title |
| `title` | text | One line |
| `body` | text | Short context |
| `options` | `[{ "key": "A", "label": "…", "note": "…" }]` | `decision` only. Leave it out (or empty) for a free-text answer |
| `recommended` | an option `key` | Marks it and shows **Accept recommendation** |
| `priority` | `blocking`, `later` | Blocking tickets are listed first, then oldest first |
| `owner` | `admin`, `dev`, `sw`, `app` | The session that acts on the answer |
| `createdAt` | UTC ISO | |
| `status` | `open` → `answered` → `done`; `info`: `open` → `dismissed` | The page sets `answered` or `dismissed`. The owner sets `done` after acting |
| `answer` | `{ "choice": "A", "note": "…" }` | Set by the page. `choice` is `""` when Grant only typed a note |
| `answeredAt` | UTC ISO | Set by the page |
| `history` | `[{ choice, note, answeredAt }]` | Set by the page when Grant uses **Change answer**: the earlier answers, oldest first |

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

`{ "name": "dev", "state": "working", "doing": "…", "waitingOn": "…", "updatedAt": "…" }`, where `state` is `working`, `waiting`, `blocked` or `idle`. Each session writes only its own file, at each stage change. The page always shows Admin, Dev, SW and App cards ("No status yet" until a file exists).

## `inbox\<id>.json` (written by the page)

`{ "id": "20261107-110227-5706", "text": "…", "createdAt": "…" }`: Grant's note to Admin. Admin may add `"readAt"` once it's read; the page then shows "read by Admin" under the note.

## `sw-job` tickets (phase 2, built 7 Nov)

Same common fields (`id, type: "sw-job", ref, title, body, priority, owner, createdAt, status`), plus:

| Field | Example | Notes |
| --- | --- | --- |
| `files` | `[{ "path": "P:\\APEX\\GNRIC\\GNRIC-LM.SLDPRT", "configuration": "250+3000+250_SHS_40X40" }]` | Opened in order on **Start**. Full paths to .SLDPRT, .SLDASM or .SLDDRW files that exist; nothing under `.pdm`. `configuration` is optional; the last file opened is left active |
| `closeAll` | `true` (default) | Start first closes all documents **without saving**, after a confirm on the page. `false`: only opens |
| `steps` | `["Open the cut list…", "Set Finish to HDG…"]` | Shown one at a time with Back / Next (`**bold**` and `` `code` `` work) |
| `step` | `0` | The step showing; the page updates it on Next/Back, so a reload keeps the place |
| `startedAt` | UTC ISO | Set by the page on Start |
| `run` | `{ "at", "ok", "problems", "message", "output" }` | Set by the page after Start: `ok` false means it stopped (refused, SOLIDWORKS error or timeout); `problems` counts calls that finished with problems (exit 1); `output` is SwCmd's full report (last 12000 characters) |
| `answer` | `{ "choice": "done", "note": "" }` or `{ "choice": "problem", "note": "…" }` | **Done** and **Problem** set `status: "answered"`; the owner checks the work, then sets `done`. Change answer works as for decisions (`history`) |

Status flow: `open` → (Start) `started` → (Done / Problem) `answered` → (owner checked) `done`. Start can be pressed again while `started`.

**What Start runs** (agreed with Dev 7 Nov; SwCmd at `C:\code\pdm\tools\SwCmd\bin\SwCmd.exe`, or the `PDM_SWCMD` variable): `SwCmd.exe close-all` (never `--exit`; skipped when `closeAll` is false), then `SwCmd.exe open "<file>" [--config "<name>"]` for each file in order. Exit 0 is clean, and 1 means done with problems (carries on); 2 or 3 stops the job. The first call gets 5 minutes (it may have to start SOLIDWORKS) and later ones get 3; on a timeout the Desk stops SwCmd and leaves SOLIDWORKS alone. One job runs at a time. Arguments are passed directly, never through a shell, and tickets never carry commands. Nothing in these calls saves.
