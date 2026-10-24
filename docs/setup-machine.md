# Setting up a PC for the PDM

Kept by App. Two parts: **A** is for every engineer's PC (James, and Grant's own). **B** is extra, for PCs Grant also builds and administers from (his desktop and laptop).

Do the steps in order. Lines in `code` are typed into **PowerShell** (Start menu → type `PowerShell` → Enter) unless they say otherwise.

## A. Every engineer's PC

| # | Step | How | Check |
| --- | --- | --- | --- |
| A1 | SOLIDWORKS 2026 | Already installed and activated | Opens normally |
| A2 | .NET 8 runtime | Not needed: the installed app carries its own .NET. | |
| A3 | Document Manager key | Grant gives you the key privately (never by email in plain text, never written in a file). Then: `[Environment]::SetEnvironmentVariable('SW_DM_KEY', '<key>', 'User')`. Close and reopen PowerShell afterwards. | `[bool]$env:SW_DM_KEY` prints `True` (it never prints the key) |
| A4 | The app | Grant sends `PDM-win-Setup.exe` once (Dropbox). Double-click it: it installs for you only (no administrator) to `%LOCALAPPDATA%\PDM` with a Start-menu shortcut **PDM**, then opens the page. After that it updates itself: after login the page shows **Update x: restart** when a new version is ready. | The page shows `v…` next to your name |
| A4a | Windows 11: Smart App Control | The app isn't code-signed yet, so Smart App Control blocks it ("An Application Control policy has blocked this file"). Check: **Settings → Privacy & security → Windows Security → App & browser control → Smart App Control settings**. If it's **On**, ask Grant before changing it. (Grant's laptop: turned off 24 Oct; build 26200.9711, so it can be turned back on later.) | `pdm.exe` starts without that message |
| A5 | Your PDM folder and `P:` | Pick a folder for your copy of the vault, e.g. `D:\PDM`. Make it. Then **right-click PowerShell → Run as administrator** and run `& "$env:LOCALAPPDATA\PDM\current\pdm.exe" map D:\PDM`. This maps `P:` to it permanently (a Windows registry setting, so it survives restarts and every program sees it). **Then restart Windows**: until you do, `P:` only shows in administrator windows. If `P:` is already used, the app says so: stop and tell Grant. | `subst` lists `P:\: => D:\PDM`; after a restart, `P:\` still opens in File Explorer |
| A6 | SOLIDWORKS reference settings | In SOLIDWORKS: **Tools → Options → System Options → File Locations**, choose **Referenced Documents** in "Show folders for". Remove everything so the list is **empty**. Files store full `P:\…` paths, so SOLIDWORKS finds references without the list; an empty list means a wrong path shows up as a missing file instead of being quietly covered. | The list is empty |
| A7 | First login | Start **PDM** from the Start menu: it opens the PDM page in your browser (`http://localhost:5170/`; keep the black window open while you use it). Log in with your email and the temporary password AWS emailed you; choose a new password (12+ characters, with a number). | Your name shows top right |
| A8 | Get the vault | On the page: **Get latest** | Files appear under `P:\`, all read-only |

## B. Extra, for Grant's PCs (build and admin)

| # | Step | How | Check |
| --- | --- | --- | --- |
| B1 | Git | `winget install Git.Git` | `git --version` |
| B2 | .NET 8 SDK | `winget install Microsoft.DotNet.SDK.8` | `dotnet --list-sdks` shows `8.0.x` |
| B3 | The repo | `git clone https://github.com/gelliott1972/pdm.git E:\code\pdm` (private: sign in to GitHub when asked) | `E:\code\pdm\SPEC.md` exists |
| B4 | Build the app | `dotnet build E:\code\pdm\src\Pdm`. It uses the Document Manager DLL from the SOLIDWORKS install (`C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.swdocumentmgr.dll`), so SOLIDWORKS must be installed first. | `E:\code\pdm\src\Pdm\bin\Debug\net8.0-windows\pdm.exe` exists |
| B5 | AWS CLI | `winget install Amazon.AWSCLI` | `aws --version` |
| B6 | Admin login (`pdm-admin`) | Copy these lines into `%USERPROFILE%\.aws\config` (Notepad), unless they're already there: see the box below. Then `aws sso login --profile pdm-admin` and approve in the browser. | `aws sts get-caller-identity --profile pdm-admin` shows account `588966315587` |
| B7 | Claude Code | `irm https://claude.ai/install.ps1 \| iex`, then `claude` and sign in | `claude --version` |

Box for B6 (no secrets in it):

```
[sso-session candelic-admin-sso]
sso_start_url = https://d-c46772e530.awsapps.com/start
sso_region = ap-east-1

[profile pdm-admin]
sso_session = candelic-admin-sso
sso_account_id = 588966315587
sso_role_name = AdministratorAccess
region = ap-east-1
```

## Publishing an update (Grant's PC)

Needs B2, B6 and the packaging tool once: `dotnet tool install -g vpk --version 1.2.161`. Then, with the version one higher than the last:

`powershell -File C:\code\pdm\src\publish.ps1 -Version 0.1.3`

It builds the app with .NET included, packs it, and uploads it to the `pdm-releases` bucket. Installed copies offer it at their next login. A fresh `PDM-win-Setup.exe` for new PCs is left in `%LOCALAPPDATA%\PDM-build\releases\`. Keep that folder: it lets each update be a small download. Publishing from a different PC: copy that folder across first (done for the laptop, 24 Oct).

## What lives where

| Thing | Where | Secret? |
| --- | --- | --- |
| AWS ids (bucket, tables, login pool) | `src\Pdm\pdm.settings.json` | No |
| Document Manager key | `SW_DM_KEY` user environment variable | **Yes**: never in git, files or chat |
| Engineer passwords | Only in each person's head; Cognito holds them | **Yes** |
| Login tokens | In memory while `pdm.exe` runs; nothing saved to disk | Yes |
| What this PC last fetched | `P:\.pdm\state.json` | No |
| The installed app | `%LOCALAPPDATA%\PDM\current\` | No |
| Releases | S3 `pdm-releases` (private; engineers read-only) | No |
