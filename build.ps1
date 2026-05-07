# DiskMonitor 빌드/배포 스크립트
# 사용법:
#   powershell -ExecutionPolicy Bypass -File .\build.ps1            # 단일 EXE publish
#   powershell -ExecutionPolicy Bypass -File .\build.ps1 -Msi       # MSI 설치 패키지까지 빌드

param(
    [switch]$Msi
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "[1/2] DiskMonitor publish (win-x64, single file)..." -ForegroundColor Cyan
dotnet publish "$root\DiskMonitor\DiskMonitor.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

$publishDir = Join-Path $root "DiskMonitor\bin\Release\net8.0-windows\win-x64\publish"
Write-Host "Publish 결과: $publishDir" -ForegroundColor Green

if ($Msi) {
    Write-Host "[2/2] MSI 설치 패키지 빌드..." -ForegroundColor Cyan

    # WiX v4 SDK가 필요합니다. 최초 1회: dotnet tool install --global wix
    if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
        Write-Host "WiX CLI가 없어 설치합니다: dotnet tool install --global wix" -ForegroundColor Yellow
        dotnet tool install --global wix
    }

    dotnet build "$root\Installer\Installer.wixproj" -c Release
    Write-Host "MSI 빌드 완료. 산출물 위치: Installer\bin\Release\" -ForegroundColor Green
}
else {
    Write-Host "MSI까지 만들려면 -Msi 옵션을 사용하세요." -ForegroundColor Yellow
}
