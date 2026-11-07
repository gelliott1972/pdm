# Installs PDM Desk for this Windows user: builds it into %LOCALAPPDATA%\PDM-Desk and adds a Start menu shortcut "PDM Desk".
# Run again after changes; it stops a running Desk first so the files can be replaced. Needs the .NET 8 SDK.
#   powershell -NoProfile -ExecutionPolicy Bypass -File src\PdmDesk\install.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA 'PDM-Desk'
$data = 'C:\code\pdm\desk'

Get-Process PdmDesk -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet publish (Join-Path $here 'Desk.csproj') -c Release -o $dest --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

foreach ($d in 'tickets', 'status', 'inbox') { New-Item -ItemType Directory -Force (Join-Path $data $d) | Out-Null }

$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'PDM Desk.lnk'
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut($lnk)
$s.TargetPath = Join-Path $dest 'PdmDesk.exe'
$s.WorkingDirectory = $dest
$s.Description = 'PDM Desk: tickets and session status'
$s.Save()

"Installed to $dest"
"Start menu: $lnk"
"Data: $data"
