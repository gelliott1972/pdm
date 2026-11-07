# Brief: PDM Desk (Grant's one place for decisions, SOLIDWORKS jobs and status)

From Admin, 7 Nov 2026. Agreed with Grant the same day. Owner: **App** (page and local server). **Dev** supplies the SOLIDWORKS tool the Desk calls (phase 2).

## Why

Grant is losing track across four windows, with messages arriving while he types. The Desk replaces that: sessions post tickets instead of messaging him, he answers when he's ready, and SOLIDWORKS jobs open the right file for him.

## What Grant sees

A local page (`localhost`, its own port, separate from the PDM page; Start menu shortcut **PDM Desk**). Clean, modern, simple; works in light and dark.

| Area | Content |
| --- | --- |
| Tickets (main) | Open tickets, blocking first, then by age. Answered ones disappear from the main list (a small "Sent" list shows the last few, with "Change answer") |
| Status panel | One card per session (Admin, Dev, SW, App): state (working / waiting / blocked / idle), what it's doing, what it's waiting on, last update time |
| Note to Admin | A small box: Grant types a note; it becomes an inbox file for Admin |

## Ticket types

| Type | Shows | Grant's actions |
| --- | --- | --- |
| `decision` | Ref (e.g. D16), title, short context, options (label + one-line note), recommended option marked | Pick an option and/or type a note, Send. Quick "Accept recommendation" button |
| `sw-job` (phase 2) | Title, why, files, steps one at a time | **Start** (Desk calls Dev's tool: close all without saving after a confirm on the page, open the file(s), set the configuration), **Next** / **Back** through steps, **Done** (marks it for the owner session to check), **Problem** (note back to the owner) |
| `info` | A short note | Dismiss |

## Storage: plain files, so every session can read and write

`C:\code\pdm\desk\` (not in git; add to `.gitignore`):

| Path | Written by | Content |
| --- | --- | --- |
| `tickets\<id>.json` | The session that asks (Admin tidies) | `id, type, ref, title, body, options[{key,label,note}], recommended, priority (blocking/later), owner (admin/dev/sw/app), createdAt, status (open/answered/done/dismissed), answer{choice,note}, answeredAt`, and for `sw-job`: `files[{path,configuration}], steps[text], step` |
| `status\<session>.json` | Each session, itself | `name, state, doing, waitingOn, updatedAt` |
| `inbox\<id>.json` | The page | Grant's note to Admin: `text, createdAt` |

Write each file whole: write to a temp file, then rename, so nobody reads half a file. The page polls every few seconds or uses a file watcher pushed over SSE; either is fine.

## Rules for sessions (Admin tells each session)

- Never message Grant for a decision; post a ticket. Admin may merge or reorder tickets.
- Keep your own `status\` file current at each stage change, not per finding.
- Watch `tickets\` for your own answered tickets (Monitor on the folder), act, then set `status: done`.

## Safety

- The server listens on `127.0.0.1` only.
- The page never runs arbitrary commands: `sw-job` Start calls a fixed set of the tool's commands with the ticket's file paths.
- Dev's tool refuses writes to `M:\`, and to `P:\` unless the file is checked out; never touches `P:\.pdm`.
- No secrets in tickets or status.

## Phases

1. **Now (App):** page, server, `decision` and `info` tickets, status panel, note to Admin. Seed it with a test ticket and your own status; tell Admin when it's up and how Grant starts it.
2. **After Dev's tool can open/close/set configuration (App + Dev):** `sw-job` tickets with Start / Next / Back / Done / Problem.

Done-when for phase 1: Grant opens PDM Desk from the Start menu, answers a test ticket, and Admin reads the answer from the file.
