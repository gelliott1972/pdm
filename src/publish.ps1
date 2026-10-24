# Builds a release of the PDM app and uploads it to the releases bucket, so installed copies update themselves.
#   powershell -File src\publish.ps1 -Version 0.2.0            build, pack and upload
#   powershell -File src\publish.ps1 -Version 0.2.0 -NoUpload  build and pack only (to try Setup.exe locally)
# Needs: .NET 8 SDK, vpk (dotnet tool install -g vpk --version 1.2.161), and for upload the pdm-admin SSO login
# (aws sso login --profile pdm-admin). Each version must be higher than the last one published.
param(
    [Parameter(Mandatory)][string]$Version,
    [switch]$NoUpload
)
$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'Pdm'
$work    = Join-Path $env:LOCALAPPDATA 'PDM-build'
$publish = Join-Path $work 'publish'
$out     = Join-Path $work 'releases'   # kept between runs so vpk can make small delta updates
$bucket  = (Get-Content (Join-Path $project 'pdm.settings.json') -Raw | ConvertFrom-Json).releasesBucket

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "Building $Version"
dotnet publish $project -c Release -r win-x64 --self-contained -p:Version=$Version -o $publish --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Write-Host 'Packing'
vpk pack --packId PDM --packTitle PDM --packVersion $Version --packDir $publish --mainExe pdm.exe --outputDir $out
if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed.' }

if ($NoUpload) { Write-Host "Packed only. Setup: $out\PDM-win-Setup.exe"; return }

# Packages first, the feed last: an installed copy never sees a version whose files aren't there yet.
Write-Host "Uploading to $bucket"
Get-ChildItem $out -Filter "PDM-$Version-*.nupkg" | ForEach-Object {
    aws s3 cp $_.FullName "s3://$bucket/$($_.Name)" --profile pdm-admin --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "Upload of $($_.Name) failed." }
}
aws s3 cp (Join-Path $out 'PDM-win-Setup.exe') "s3://$bucket/PDM-win-Setup.exe" --profile pdm-admin --only-show-errors
aws s3 cp (Join-Path $out 'releases.win.json') "s3://$bucket/releases.win.json" --profile pdm-admin --only-show-errors
if ($LASTEXITCODE -ne 0) { throw 'Upload of the release feed failed.' }

Write-Host "Published $Version. Setup for a new PC: $out\PDM-win-Setup.exe"
