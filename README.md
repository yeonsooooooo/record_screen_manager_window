# DiskMonitor (디스크 자동 정리)

지정한 폴더가 위치한 드라이브의 사용량을 주기적으로 모니터링하다가, 설정한 한도를
초과하면 그 폴더(하위 폴더 포함) 안에서 **가장 오래된 파일 1개**를 자동으로 삭제하는
Windows 전용 WPF 데스크톱 앱입니다.

## 동작 요약

- 사용자가 UI에서 다음을 입력합니다.
  - **감시 폴더 경로** — 가장 오래된 파일을 찾아 삭제할 대상 폴더
  - **모니터링 주기(분)** — 1 ~ 1440분 사이 정수
  - **한도 기준** — 다음 둘 중 하나
    - 사용률(%) 이상이면 삭제 (예: 90%)
    - 남은 공간(MB) 미만이면 삭제 (예: 1024MB)
  - **실행 ON / OFF** — 토글 버튼
- ON 상태에서는 즉시 1회 검사를 수행하고, 이후 주기마다 다시 검사합니다.
- 한도 조건이 충족되면 **정확히 1개**의 가장 오래된(LastWriteTimeUtc 기준) 파일을 삭제합니다.
- 한 번의 검사에서 1개만 삭제하므로, 여전히 공간이 부족하면 다음 주기에서 또 1개를 지웁니다.
- 설정과 ON/OFF 상태는 `%APPDATA%\DiskMonitor\settings.json`에 저장되어 재실행 시 복원됩니다.

## 요구 사항

- Windows 10 / 11 (x64)
- 빌드 시: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- (선택) MSI 만들 때: WiX Toolset v4 — `dotnet tool install --global wix`

## 빌드 & 실행

### 1) 개발 중 실행

```powershell
dotnet run --project DiskMonitor\DiskMonitor.csproj
```

### 2) 단일 EXE publish (자체 포함, .NET 런타임 설치 불필요)

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

산출물: `DiskMonitor\bin\Release\net8.0-windows\win-x64\publish\DiskMonitor.exe`
이 EXE 한 개만 복사하면 다른 PC에서도 그대로 실행됩니다.

### 3) MSI 설치 패키지 만들기

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Msi
```

산출물: `Installer\bin\Release\DiskMonitorSetup.msi`

해당 MSI를 더블클릭하면 마법사를 통해 `C:\Program Files\DiskMonitor\`에 설치되고,
시작 메뉴에 `DiskMonitor` 바로가기가 생성됩니다.

## 사용 시 주의

- 삭제는 **휴지통을 거치지 않는 영구 삭제**입니다. 중요한 파일이 들어있는 폴더는
  지정하지 마세요.
- 한도 기준은 폴더가 위치한 **드라이브 전체** 사용량을 기준으로 판단합니다.
  (예: D:\Recordings 를 감시하면 D 드라이브 전체 사용량을 봅니다.)
- 가장 오래된 파일은 `File.GetLastWriteTimeUtc` 기준으로 결정됩니다.
- 시스템 속성이 붙은 파일은 검사에서 제외됩니다.

## 폴더 구조

```
DiskMonitor/                 WPF 앱
  App.xaml(.cs)
  MainWindow.xaml(.cs)       UI + 타이머 모니터링
  DiskMonitorLogic.cs        용량 검사 / 가장 오래된 파일 탐색·삭제
  AppSettings.cs             설정 영구 저장
  ThresholdKind.cs           한도 종류 enum
  app.manifest               DPI / OS 매니페스트
  DiskMonitor.csproj
Installer/                   WiX v4 MSI 설치 패키지
  Installer.wixproj
  Product.wxs
build.ps1                    publish + MSI 빌드 스크립트
```
# record_screen_manager_window
