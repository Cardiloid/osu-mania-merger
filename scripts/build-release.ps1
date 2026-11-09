param(
    [string]$Version = "1.7.0",
    [string]$FfmpegPath = "",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $OutDir) { $OutDir = Join-Path $root "release" }

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) {
    $candidate = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path $candidate) { $dotnet = $candidate }
}
if (-not $dotnet) { throw "dotnet SDK 8 not found. Install .NET 8 SDK first." }

$publish = Join-Path $root "publish"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "Publishing self-contained win-x64 ..."
& $dotnet publish (Join-Path $root "src\OsuManiaMerger.csproj") `
    -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

if (-not $FfmpegPath) {
    $FfmpegPath = Join-Path $root "tools\ffmpeg.exe"
    if (-not (Test-Path $FfmpegPath)) {
        & (Join-Path $PSScriptRoot "get-ffmpeg.ps1") -Destination $FfmpegPath
    }
}
if (-not (Test-Path $FfmpegPath)) { throw "ffmpeg.exe not found: $FfmpegPath" }

$stage = Join-Path $OutDir "OsuManiaMerger"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item (Join-Path $publish "OsuManiaMerger.exe") $stage
Copy-Item $FfmpegPath (Join-Path $stage "ffmpeg.exe")
Copy-Item (Join-Path $root "packaging\Start.bat") $stage
Copy-Item (Join-Path $root "packaging\README-release.txt") (Join-Path $stage "README.txt")
Copy-Item (Join-Path $root "samples") (Join-Path $stage "samples") -Recurse
New-Item -ItemType Directory -Path (Join-Path $stage "output") | Out-Null
Set-Content -LiteralPath (Join-Path $stage "output\README.txt") -Value "Generated .osz files go here by default." -Encoding UTF8

$zip = Join-Path $OutDir "OsuManiaMerger-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal

Write-Host ""
Write-Host "Release ready:"
Write-Host "  $zip"
Write-Host "  version: $Version"
