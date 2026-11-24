# Installs PDM Desk for this Windows user: builds it into %LOCALAPPDATA%\PDM-Desk. No Start menu shortcut of its own
# (Grant, 14 Nov): Start menu > PDM Sessions (C:\code\pdm\start-pdm.cmd) starts it.
# Run again after changes; it stops a running Desk first so the files can be replaced. Needs the .NET 8 SDK.
#   powershell -NoProfile -ExecutionPolicy Bypass -File src\PdmDesk\install.ps1
# Settings: settings.json beside the exe. The template (settings.default.json) is copied there only if the file is
# missing; an existing settings.json is never overwritten.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA 'PDM-Desk'
$data = 'C:\code\pdm\desk'

Get-Process PdmDesk -ErrorAction SilentlyContinue | Stop-Process -Force

dotnet publish (Join-Path $here 'Desk.csproj') -c Release -o $dest --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$settings = Join-Path $dest 'settings.json'
if (-not (Test-Path $settings)) { Copy-Item (Join-Path $here 'settings.default.json') $settings; "Settings: copied the template to $settings" }
else { "Settings: kept $settings" }
try { $folder = (Get-Content -Raw $settings | ConvertFrom-Json).dataFolder; if ($folder) { $data = $folder } } catch { "Settings: couldn't read $settings ($($_.Exception.Message)); the Desk will use its built-in settings" }

foreach ($d in 'tickets', 'status', 'inbox') { New-Item -ItemType Directory -Force (Join-Path $data $d) | Out-Null }

"Installed to $dest"
"Data: $data"
