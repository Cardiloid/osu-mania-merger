param(
    [string]$Destination = (Join-Path $PSScriptRoot "..\tools\ffmpeg.exe")
)

$ErrorActionPreference = "Stop"
$url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$zip = Join-Path $env:TEMP "osu-mania-merger-ffmpeg.zip"
$extract = Join-Path $env:TEMP "osu-mania-merger-ffmpeg"

Write-Host "Downloading ffmpeg essentials ..."
Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing

if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $extract -Force

$exe = Get-ChildItem $extract -Recurse -Filter ffmpeg.exe | Select-Object -First 1
if (-not $exe) { throw "ffmpeg.exe not found in archive" }

$parent = Split-Path $Destination -Parent
if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Path $parent | Out-Null }
Copy-Item $exe.FullName $Destination -Force

Write-Host "ffmpeg -> $Destination"
