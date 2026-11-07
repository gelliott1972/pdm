# SOLIDWORKS MCP repos: review (7 Nov 2026)

Grant asked whether either repo could help us automate SOLIDWORKS work: open, save, Close All, exit, rebuild, Pack and Go, delete components, save out virtual parts. A helper read each README and the main source through the GitHub API. Nothing was cloned, built or run.

**Summary:** neither fits our work. Both are built to model new parts, not to prepare existing assemblies. Recommendation: a small tool of our own (Dev's job, Admin to decide).

## eyfel/mcp-server-solidworks ("SolidPilot")

- **What it is:** 479 stars, last commit 7 Oct 2026, AGPL-3.0. Calls itself "early alpha". One open issue: a crash in SOLIDWORKS 2026 SP4.
- **How it connects:** a C# (.NET 4.8) server attaches to the running SOLIDWORKS and listens on `localhost:5000`. A Python adapter sits between Claude and the server.
- **Tools:** 47, mostly sketches, features, drawings and export.

| Our task | Covered? |
| --- | --- |
| Open, save, close a document | Yes |
| Close All | Yes, but it throws away unsaved changes without asking |
| Exit SOLIDWORKS | No |
| Rebuild | No stand-alone command |
| Pack and Go | No |
| Suppress or delete | Named features yes; components unclear |
| Save a virtual part to its own file | No |
| Feature tree | Yes |
| References | Partly (component paths only) |
| Screenshot | No |

**Risks (checked in code):**

- The port has no login, so any program on the PC could drive SOLIDWORKS.
- `save_analysis` writes a `.solidpilot\` folder next to the model, so a model on `P:\` would get one in the vault.
- The adapter starts the server hidden.
- The repo includes a ready-built `.exe`.
- Using its code brings AGPL obligations.

**Verdict:** take ideas only. It hits the same "file already open by name" problem we do and uses `CloseAllDocuments` for it. Its way of attaching to the running SOLIDWORKS is also worth copying.

## alisamsam/Solidworks-MCP

- **What it is:** 198 stars, MIT. One commit in March 2026, nothing since. A demo.
- **How it connects:** Python with pywin32. It may start a new SOLIDWORKS rather than attach to the open one. Claims SOLIDWORKS 2023 to 2025.
- **Tools:** open, save, close and the feature list only, out of our tasks.

**Risks (checked in code):**

- An `execute_python` tool runs any code Claude writes.
- A `.env` file with API keys is committed to the repo, and an open issue reports a leaked key.

**Verdict:** skip.

## Suggested instead

A small C# command-line tool next to DmProbe and DmEdit. It would attach to the running SOLIDWORKS: no server, no port. One command per task:

- close-all and exit
- open, save, rebuild
- Pack and Go into a chosen folder (never the default)
- delete or suppress a component
- save a virtual part to its own file
- list the feature tree
- screenshot

Anything that writes refuses `M:\`, and refuses `P:\` unless the file is checked out.

**Not checked yet:**

- That the SOLIDWORKS API in 2026 covers Pack and Go, saving a virtual part to its own file, and screenshots. The helper went from memory.
- That SOLIDWORKS's COM libraries work from .NET 8.
