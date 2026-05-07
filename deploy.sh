#!/usr/bin/env bash
# 로컬 코드 기준으로 DiskMonitor MSI(DiskMonitorSetup.msi)를 생성합니다.
# WPF + WiX는 Windows(또는 WSL에서 powershell.exe로 Windows 쪽 빌드)에서만 동작합니다.
#
# 사용:
#   ./deploy.sh
#
# 산출물: Installer/bin/Release/DiskMonitorSetup.msi

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

to_windows_path() {
  local p="$1"
  if command -v cygpath >/dev/null 2>&1; then
    cygpath -w "$p"
  elif command -v wslpath >/dev/null 2>&1; then
    wslpath -w "$p"
  else
    printf '%s' "$p"
  fi
}

run_powershell_build() {
  local build_ps1_win
  build_ps1_win="$(to_windows_path "$SCRIPT_DIR/build.ps1")"

  if command -v powershell.exe >/dev/null 2>&1; then
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$build_ps1_win" -Msi
    return
  fi

  case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*)
      local ps="/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
      if [[ -x "$ps" ]]; then
        "$ps" -NoProfile -ExecutionPolicy Bypass -File "$build_ps1_win" -Msi
        return
      fi
      ;;
  esac

  echo "deploy.sh: powershell.exe를 찾을 수 없습니다. Windows 또는 WSL에서 실행하세요." >&2
  exit 1
}

case "$(uname -s)" in
  Darwin)
    echo "deploy.sh: macOS에서는 WPF 응용 프로그램과 WiX MSI를 빌드할 수 없습니다." >&2
    echo "  Windows PC(또는 WSL2에서 이 저장소를 /mnt/c/... 경로로 둔 뒤 ./deploy.sh)에서 실행하세요." >&2
    exit 1
    ;;
esac

run_powershell_build

MSI="$SCRIPT_DIR/Installer/bin/Release/DiskMonitorSetup.msi"
if [[ ! -f "$MSI" ]]; then
  echo "deploy.sh: MSI 파일이 예상 위치에 없습니다: $MSI" >&2
  exit 1
fi

echo "배포용 MSI 준비 완료:"
echo "  $MSI"
