# Desk answer notices for one session: prints one line per new notice in <desk>\notices\<session>\, then moves the
# file into seen\ so it is told once. Run it with Claude Code's Monitor tool (each printed line wakes the session):
#   powershell -NoProfile -ExecutionPolicy Bypass -File "C:/Users/grant/AppData/Local/PDM-Desk/desk-notices.ps1" -Session admin
# The data folder: -Desk, else dataFolder in settings.json beside this script, else C:\code\pdm\desk.
# The full notice (ticket, title, choice, choiceLabel, code, note, by, at, ...) stays in seen\<file>.json.
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')][string]$Session,
    [string]$Desk,
    [ValidateRange(1, 3600)][int]$Every = 5,
    [switch]$Once
)
$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

if (-not $Desk) {
    $Desk = 'C:\code\pdm\desk'
    $settings = Join-Path $PSScriptRoot 'settings.json'
    try { $f = (Get-Content -Raw -Encoding UTF8 $settings | ConvertFrom-Json).dataFolder; if ($f) { $Desk = $f } } catch { }
}
$dir = Join-Path $Desk "notices\$Session"
$seen = Join-Path $dir 'seen'
Write-Output "Watching Desk notices for $Session in $dir (every $Every s)."

while ($true) {
    if (Test-Path -LiteralPath $dir) {
        $new = @(Get-ChildItem -LiteralPath $dir -File | Where-Object { $_.Extension -eq '.json' } | Sort-Object Name)
        foreach ($file in $new) {
            $text = $null
            try { $text = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8) } catch { continue } # being written or gone: next round
            try {
                New-Item -ItemType Directory -Force -Path $seen | Out-Null
                Move-Item -LiteralPath $file.FullName -Destination (Join-Path $seen $file.Name) -Force
            } catch { continue } # another watch took it, or it's held open: it isn't printed here
            try { $line = 'Desk: ' + ($text | ConvertFrom-Json).summary }
            catch { $line = "Desk: notice $($file.Name) isn't readable JSON; see notices\$Session\seen\$($file.Name)." }
            Write-Output $line
        }
    }
    if ($Once) { break }
    Start-Sleep -Seconds $Every
}
