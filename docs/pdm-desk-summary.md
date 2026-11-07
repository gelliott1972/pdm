# PDM Desk: why and what (7 Nov 2026)

## The problem

Grant runs four Claude Code sessions on one laptop, each with its own job. Admin settles decisions with Grant and keeps the docs. Dev builds the tools. SW guides Grant's SOLIDWORKS work. App builds the PDM app. Questions and updates came from all four windows at once, often arriving while Grant was typing. It was hard to keep track of which window asked what, which list was current, and what was waiting on him.

## What we built

| Part | What it is |
| --- | --- |
| PDM Desk page | A local web page (`127.0.0.1:5180`, Start menu shortcut) with a small local server. Open questions are shown as tickets, blocking ones first. Grant picks an option or types a note, and the ticket moves to a "Sent" list. A status panel shows what each session is doing and what it's waiting on. A note box sends something to Admin |
| Storage | Plain JSON files in one folder (`C:\code\pdm\desk\`): `tickets\`, `status\`, `inbox\`. Every session can read and write them with no API or login. Each file is written whole (temp file, then rename) |
| Ticket types | **Decision** (options plus a recommendation, with an "Accept recommendation" button), **Info** (dismiss) and, coming next, **SOLIDWORKS job**: Start opens the right file and configuration in SOLIDWORKS through our command-line tool, then Next / Back step through the instructions, and Done asks the owning session to check the work |
| Rules for sessions | Written into each session's CLAUDE.md. Never ask Grant in your own window: post a ticket. Keep your own status file current at each stage change. Watch for your answered tickets, act, then mark them done. Send Admin one message per finished stage, not per finding |
| Admin's role | Sorts the tickets, removes duplicates and closes questions that are already decided. Otherwise it stays quiet: it writes to Grant only when he asks, when something blocks, or when a stage finishes |
| SwCmd | A command-line tool that drives the running SOLIDWORKS (open, close all, save, replace, delete, check). It refuses to write to the live Dropbox, and to the vault unless the file is checked out. The Desk's SOLIDWORKS jobs will call it |

## Why it's built this way

- **Local and file-based:** it has to open files in SOLIDWORKS on Grant's PC, which a web-hosted page can't do. Plain files keep it simple for every session to use.
- **Tickets, not chat:** Grant answers when he's ready, nothing changes on screen while he types, and each question sits in one place with its options.
- **Status panel:** "where are we up to?" without asking anyone.

## Limits today

- It works for Claude Code sessions on one PC that share that folder.
- Sessions only see Grant's answers when they check the folder. Nothing pushes to them.
- Only the latest check-in per file is kept elsewhere in the PDM. The Desk keeps answer history per ticket.
- No sign-in. It relies on listening only on the local machine.

## Open questions for using it with Claude Desktop and more instances

- Can Desktop sessions read and write the same folder, or would they need a local MCP server for the Desk?
- How do sessions on other machines take part? That would need shared storage or a server.
- How does each session get told an answer is in, instead of polling?

## Where things are

| Item | Path |
| --- | --- |
| Brief | `docs\briefs\desk.md` |
| File formats | `app\desk.md` |
| Code | `src\PdmDesk\` |
| Data | `C:\code\pdm\desk\` (not in git) |
| SOLIDWORKS tool | `tools\SwCmd\` |
