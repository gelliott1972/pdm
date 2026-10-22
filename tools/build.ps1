# build.ps1: builds every C# tool into its bin\ folder, with the SOLIDWORKS interop DLLs it needs copied in.
# Needs only what Windows and SOLIDWORKS install: the .NET Framework 4 compiler and SOLIDWORKS' api\redist folder.
# Usage: .\build.ps1 [-SwFolder 'C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS']
param([string] $SwFolder)
$ErrorActionPreference = 'Stop'

$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { Write-Host "No C# compiler at $csc (.NET Framework 4.x, 64-bit)."; exit 1 }

# SOLIDWORKS install folder: newest version in the registry, unless given.
if (-not $SwFolder) {
    $SwFolder = Get-ChildItem 'HKLM:\SOFTWARE\SolidWorks' -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -match '^SOLIDWORKS \d{4}$' } | Sort-Object PSChildName -Descending |
        ForEach-Object { (Get-ItemProperty "$($_.PSPath)\Setup" -ErrorAction SilentlyContinue).'SolidWorks Folder' } |
        Where-Object { $_ } | Select-Object -First 1
}
$redist = Join-Path "$SwFolder" 'api\redist'
if (-not $SwFolder -or -not (Test-Path $redist)) { Write-Host "SOLIDWORKS api\redist not found. Pass -SwFolder."; exit 1 }
Write-Host "SOLIDWORKS: $SwFolder"

# Tool -> interop DLLs it references.
$dm = @('SolidWorks.Interop.swdocumentmgr.dll')
$sw = @('SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swconst.dll')
$tools = [ordered]@{ DmEdit = $dm; DmProbe = $dm; DmToolbox = $dm; Importer = $dm; SwCheck = $sw; SwFixRefs = $sw }

$failed = 0
foreach ($t in $tools.Keys) {
    $bin = Join-Path $PSScriptRoot "$t\bin"
    New-Item -ItemType Directory -Force $bin | Out-Null
    $refs = @(foreach ($d in $tools[$t]) { Copy-Item (Join-Path $redist $d) $bin -Force; "/r:$(Join-Path $bin $d)" })
    & $csc /nologo /platform:x64 "/out:$(Join-Path $bin "$t.exe")" @refs (Join-Path $PSScriptRoot "$t\$t.cs")
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $t"; $failed++ } else { Write-Host "Built: $t" }
}

# Python plan tools: check openpyxl is there. ('Continue': PowerShell 5.1 stops on a native tool's stderr under 'Stop'.)
$ErrorActionPreference = 'Continue'
python -I -c "import openpyxl" 2>$null
if ($LASTEXITCODE -ne 0) { Write-Host 'Python with openpyxl not found (needed by importplan): pip install openpyxl'; $failed++ }
if (-not [Environment]::GetEnvironmentVariable('SW_DM_KEY', 'User') -and -not $env:SW_DM_KEY) {
    Write-Host 'Note: SW_DM_KEY (Document Manager key) is not set; the Dm tools and Importer need it.'
}
if ($failed) { Write-Host "$failed problem(s)."; exit 1 }
Write-Host 'All tools built.'
