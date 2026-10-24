# Moving the Claude sessions to the laptop

From Admin, 24 Oct 2026. The laptop already has the repo (`C:\code\pdm`), the app, `P:` = `C:\PDM`. Machine setup: `docs\setup-machine.md`.

## On the desktop, before leaving

| # | Step |
|---|---|
| 1 | Dev's B/C import applied and App's load done (both report to Admin). |
| 2 | Release the desktop's locks: on the page, check in or undo everything the desktop holds (`LS-08A-06`, `MDL-ANCHOR-08X06`), or the laptop sees them as "you on GGPC" and can't edit them. |
| 3 | Admin pushes everything. `git status` clean. |
| 4 | Copy two folders that git doesn't carry (e.g. through Dropbox): `E:\code\pdm\scratch` → `C:\code\pdm\scratch`, and the memory folder `C:\Users\Grant\.claude\projects\E--code-pdm\memory` → on the laptop `C:\Users\<you>\.claude\projects\C--code-pdm\memory`. |
| 5 | App publishing: copy `%LOCALAPPDATA%\PDM-build\releases` to the same place on the laptop (keeps updates small). |
| 6 | Optional: `E:\PDM-work` (r4 and r1 packs, backups) if SW needs them on the laptop. |

## On the laptop

| # | Step |
|---|---|
| 1 | `cd C:\code\pdm` then `git pull`. |
| 2 | Dev tools: `tools\build.ps1` (checks Python with openpyxl and `SW_DM_KEY`). |
| 3 | AWS admin login for loads and publishing: `aws configure sso` profile `pdm-admin`, as on the desktop (`docs\setup-machine.md`). Then `dotnet tool install -g vpk --version 1.2.161`. |
| 4 | Start the four sessions, each in its own window: `C:\code\pdm` (Admin), `C:\code\pdm\dev`, `C:\code\pdm\sw`, `C:\code\pdm\app`. Accept the trust prompt in each. In each, `/rename admin`, `dev`, `sw`, `app`. |
| 5 | First message to each: "Read your CLAUDE.md and your status.md, then tell me where you're up to." |
| 6 | Paths: the work folders change (`E:\PDM-work` → a laptop folder, test vault → `C:\PDM`). Each session updates its own notes when it first needs them. |
