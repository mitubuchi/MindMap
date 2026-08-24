# Package the self-contained build into a portable ZIP.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File installer/package-zip.ps1
#
# Publish the self-contained build first:
#   dotnet publish src/MindMap/MindMap.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64

param(
    [string]$Version = "1.5.0"
)

$ErrorActionPreference = "Stop"

# Base everything on the repository root (one level up from this script).
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "publish/win-x64"
$outputDir = Join-Path $root "installer/Output"
$zipPath = Join-Path $outputDir "MindMap-$Version-win-x64.zip"

if (-not (Test-Path (Join-Path $publish "MindMap.exe"))) {
    throw "No self-contained build in publish/win-x64. Run 'dotnet publish' first."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

# Stage under a 'MindMap' folder so the ZIP extracts into one clean directory.
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("MindMap-zip-" + [guid]::NewGuid())
$stageApp = Join-Path $staging "MindMap"
New-Item -ItemType Directory -Force -Path $stageApp | Out-Null

try {
    Copy-Item -Path (Join-Path $publish "*") -Destination $stageApp -Recurse -Force

    # 同梱している第三者ソフトウェアの表示。SharpVectors が BSD-3-Clause なので
    # バイナリで再配布する側に表示義務がある。
    Copy-Item -Path (Join-Path $root "THIRD-PARTY-NOTICES.txt") -Destination $stageApp -Force

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path $stageApp -DestinationPath $zipPath -CompressionLevel Optimal

    $sizeMb = (Get-Item $zipPath).Length / 1MB
    Write-Output ("wrote {0} ({1:N1} MB)" -f $zipPath, $sizeMb)
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}
