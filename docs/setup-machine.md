# Setting up a PC for the PDM

Kept by App. Two parts: **A** is for every engineer's PC (James, and Grant's own). **B** is extra, for PCs Grant also builds and administers from (his desktop and laptop).

Do the steps in order. Lines in `code` are typed into **PowerShell** (Start menu → type `PowerShell` → Enter) unless they say otherwise.

## A. Every engineer's PC

| # | Step | How | Check |
| --- | --- | --- | --- |
| A1 | SOLIDWORKS 2026 | Already installed and activated | Opens normally |
| A2 | .NET 8 runtime | `winget install Microsoft.DotNet.Runtime.8` (not needed if the SDK from B2 is installed) | `dotnet --list-runtimes` shows `Microsoft.NETCore.App 8.` |
| A3 | Document Manager key | Grant gives you the key privately (never by email in plain text, never written in a file). Then: `[Environment]::SetEnvironmentVariable('SW_DM_KEY', '<key>', 'User')`. Close and reopen PowerShell afterwards. | `[bool]$env:SW_DM_KEY` prints `True` (it never prints the key) |
| A4 | The app | Grant shares the app as one folder (Dropbox or OneDrive). Copy it to `C:\PDM-app` and run `C:\PDM-app\pdm.exe` from there. *The package itself is still to be built.* Grant's PCs build it from source instead (B4). | `pdm.exe` runs and shows its usage |
| A5 | Your PDM folder and `P:` | Pick a folder for your copy of the vault, e.g. `D:\PDM`. Make it. Then **right-click PowerShell → Run as administrator** and run `pdm.exe map D:\PDM`. This maps `P:` to it permanently (a Windows registry setting, so it survives restarts and every program sees it). If `P:` is already used, the app says so: stop and tell Grant. | `subst` lists `P:\: => D:\PDM`; after a restart, `P:\` still opens in File Explorer |
| A6 | SOLIDWORKS reference settings | In SOLIDWORKS: **Tools → Options → System Options → File Locations**, choose **Referenced Documents** in "Show folders for". Remove everything so the list is **empty** (`P:\` alone is also acceptable). Files store full `P:\…` paths, so SOLIDWORKS finds references without the list; a folder outside `P:\` here could make it open a stray copy instead. | Nothing listed outside `P:\` |
| A7 | First login | Run `pdm.exe`. Log in with your email and the temporary password AWS emailed you; choose a new password (12+ characters, with a number). | It says "Logged in as …" |
| A8 | Get the vault | At the `pdm>` prompt: `get` | Files appear under `P:\`, all read-only |

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

## What lives where

| Thing | Where | Secret? |
| --- | --- | --- |
| AWS ids (bucket, tables, login pool) | `src\Pdm\pdm.settings.json` | No |
| Document Manager key | `SW_DM_KEY` user environment variable | **Yes**: never in git, files or chat |
| Engineer passwords | Only in each person's head; Cognito holds them | **Yes** |
| Login tokens | In memory while `pdm.exe` runs; nothing saved to disk | Yes |
| What this PC last fetched | `P:\.pdm\state.json` | No |
