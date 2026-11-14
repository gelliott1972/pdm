# Countdown window for Grant's video, then makes the PLP-06 draft.
$host.UI.RawUI.WindowTitle = 'Draw: GNRIC-PLP-06'
try { $host.UI.RawUI.BufferSize = New-Object Management.Automation.Host.Size(90, 300); $host.UI.RawUI.WindowSize = New-Object Management.Automation.Host.Size(90, 22) } catch {}
Write-Host ''
Write-Host '  Making the drawing for GNRIC-PLP-06 (floor plate 350x200) from the model' -ForegroundColor Cyan
Write-Host ''
for ($i = 10; $i -ge 1; $i--) { Write-Host ("  Starting in {0,2}" -f $i); Start-Sleep -Seconds 1 }
Write-Host '  Go' -ForegroundColor Green
Write-Host ''
$t = 'C:\code\pdm\scratch\draw-test\PLP-06'
& 'C:\code\pdm\draw\src\bin\Draw.exe' make-plate "$t\GNRIC-PLP.SLDPRT" 'FP-350x200' "$t\GNRIC-PLP-06.SLDDRW" 'C:\code\pdm\scratch\draw-test\_Templates\Unilumin-A3.DRWDOT' |
  Where-Object { $_ -notmatch '^\s+(reference|view )' } | ForEach-Object { Write-Host "  $_" }
Write-Host ''
Write-Host '  Done. Everything in magenta is the machine''s work, waiting to be checked.' -ForegroundColor Cyan
Write-Host '  This window closes in 60 seconds.'
Start-Sleep -Seconds 60
