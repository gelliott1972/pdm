@echo off
rem Opens one Windows Terminal window with five tabs (Admin, Dev, SW, App, Draw),
rem each continuing its last Claude Code conversation in its own folder.
rem Works from wherever the repo lives (uses this file's folder).

set "ROOT=%~dp0"
set "ROOT=%ROOT:~0,-1%"
set "CC=claude --continue --dangerously-skip-permissions"

rem Start PDM Desk (if it's already running this just opens its page).
set "DESK=%LOCALAPPDATA%\PDM-Desk\PdmDesk.exe"
if exist "%DESK%" start "" "%DESK%"

wt -w new ^
  new-tab --title Admin --suppressApplicationTitle -d "%ROOT%" %CC% ; ^
  new-tab --title Dev   --suppressApplicationTitle -d "%ROOT%\dev" %CC% ; ^
  new-tab --title SW    --suppressApplicationTitle -d "%ROOT%\sw" %CC% ; ^
  new-tab --title App   --suppressApplicationTitle -d "%ROOT%\app" %CC% ; ^
  new-tab --title Draw  --suppressApplicationTitle -d "%ROOT%\draw" %CC% ; ^
  focus-tab -t 0
