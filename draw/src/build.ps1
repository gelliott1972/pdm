# Build Draw.exe. Interop DLLs are copied from tools\SwCmd\bin into bin\.
Set-Location $PSScriptRoot
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\Draw.exe Draw.cs Plate.cs Section.cs Layout.cs Copy.cs Assembly.cs Geom.cs Measure.cs Basic.cs General.cs
