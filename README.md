# App Launcher

자동 업데이트 기능이 있는 C# WPF 트레이 런처 애플리케이션입니다.

## 주요 기능

- **트레이 앱 방식**: 시스템 트레이에서 백그라운드로 실행 (UI 없음)
- **자동 버전 체크**: 시작 시 원격 서버에서 최신 버전 확인
- **자동 업데이트**: 구버전 감지 시 자동으로 업데이트 진행
- **작업 스케줄러 등록**: Windows 로그온 시 관리자 권한으로 자동 실행 (UAC 프롬프트 없음)
- **상태 확인**: 트레이 아이콘 우클릭 또는 더블클릭으로 상태 창 표시
- **자동 종료**: 대상 프로그램 실행 후 런처 자동 종료
- **커스텀 아이콘**: 로켓 모양의 커스텀 아이콘 적용

## 빌드 방법

```bash
dotnet restore
dotnet build
```

또는 Visual Studio에서 솔루션을 열고 빌드합니다.

**Release 빌드:**
```bash
dotnet build --configuration Release
```

빌드된 실행 파일 위치:
- Debug: `bin\Debug\net9.0-windows\AppLauncher.exe`
- Release: `bin\Release\net9.0-windows\AppLauncher.exe`

## 설정 방법

`launcher_config.json` 파일을 수정하여 설정합니다:

```json
{
  "targetExecutable": "C:\\Program Files\\YourApp\\YourApp.exe",
  "workingDirectory": "C:\\Program Files\\YourApp",
  "versionCheckUrl": "https://example.com/version.json",
  "localVersionFile": "version.txt"
}
```

### 설정 항목 설명

- **targetExecutable**: 실행할 대상 프로그램의 전체 경로
- **workingDirectory**: 대상 프로그램의 작업 디렉토리 (선택사항)
- **versionCheckUrl**: 버전 정보를 확인할 URL (JSON 형식)
- **localVersionFile**: 로컬 버전 파일 경로 (없으면 자동 생성됨, 기본값: 1.0.0)

## 서버 설정

### 버전 JSON 파일 (version.json)

서버에 다음과 같은 형식의 JSON 파일을 호스팅합니다:

```json
{
  "version": "1.0.5",
  "downloadUrl": "https://example.com/YourApp-1.0.5.exe"
}
```

- **version**: 최신 버전 번호
- **downloadUrl**: 업데이트 EXE 파일의 다운로드 URL

## 실행 방법 및 동작 순서

### 첫 실행 (관리자 권한으로 실행)

1. **AppLauncher.exe를 관리자 권한으로 실행**
   - 파일 우클릭 → "관리자 권한으로 실행" 클릭
   - 또는 `app.manifest`에서 `requireAdministrator`로 설정되어 있어 자동으로 UAC 프롬프트 표시

2. **작업 스케줄러 자동 등록**
   - 작업 스케줄러에 등록되어 있지 않으면 자동으로 등록
   - "시작 프로그램에 등록되었습니다" 메시지 표시
   - 다음 로그인부터 자동으로 시작됨

3. **트레이 아이콘 표시**
   - 시스템 트레이에 로켓 아이콘 표시
   - UI 창은 표시되지 않음 (백그라운드 실행)

4. **버전 체크 및 업데이트**
   - 백그라운드에서 원격 서버의 `version.json` 확인
   - 로컬 버전 파일(`version.txt`)이 없으면 자동 생성 (1.0.0)
   - 버전 비교 수행

5. **업데이트 판단 및 실행**
   - **업데이트 필요 시 (원격 버전 > 로컬 버전)**:
     1. `downloadUrl`에서 EXE 파일 다운로드
     2. 임시 폴더에 저장
     3. 다운로드한 EXE 실행
     4. 5초 후 런처 자동 종료

   - **최신 버전인 경우**:
     1. `launcher_config.json`의 `targetExecutable` 실행
     2. 5초 후 런처 자동 종료

### 이후 실행 (자동 시작)

1. **Windows 로그온 시 자동 실행**
   - 작업 스케줄러를 통해 자동으로 시작
   - UAC 프롬프트 없이 관리자 권한으로 실행됨

2. **백그라운드 실행**
   - 트레이 아이콘만 표시
   - 자동으로 버전 체크 및 업데이트 진행

3. **자동 종료**
   - 대상 프로그램 실행 후 5초 뒤 자동 종료

### 트레이 아이콘 기능

- **더블클릭**: 상태 창 표시
- **우클릭 메뉴**:
  - **상태 보기**: 현재 진행 상황을 보여주는 창 열기
  - **시작프로그램 등록 해제**: 작업 스케줄러에서 제거
  - **종료**: 런처 즉시 종료

## 시작프로그램 등록 해제

### 방법 1: 트레이 메뉴 사용
1. 트레이 아이콘 우클릭
2. "시작프로그램 등록 해제" 클릭
3. 확인 메시지에서 "예" 클릭

### 방법 2: 작업 스케줄러에서 수동 제거
1. `Win + R` → `taskschd.msc` 입력
2. 작업 스케줄러 라이브러리에서 "AppLauncher_Startup" 찾기
3. 우클릭 → 삭제

### 방법 3: 코드에서 호출
```csharp
AppLauncher.App.UnregisterStartup();
```

## 동작 흐름도

```
[첫 실행 - 관리자 권한]
    ↓
[작업 스케줄러 등록 확인]
    ↓
[미등록 시 → 작업 스케줄러 등록]
    ↓
[트레이 아이콘 표시]
    ↓
[버전 체크 (version.json)]
    ↓
[업데이트 필요?]
    ├─ Yes → [EXE 다운로드] → [다운로드한 EXE 실행] → [5초 후 종료]
    └─ No  → [대상 프로그램 실행] → [5초 후 종료]

[이후 로그온]
    ↓
[작업 스케줄러가 자동 시작 (관리자 권한, UAC 없음)]
    ↓
[위와 동일한 흐름]
```

## 요구사항

- .NET 9.0
- Windows 10/11
- 관리자 권한 (첫 실행 시)

## 프로젝트 구조

```
AppLauncher/
├── App.xaml.cs                  # 애플리케이션 시작 로직, 작업 스케줄러 등록
├── TrayApplicationContext.cs    # 트레이 아이콘 및 메뉴 관리
├── ApplicationLauncher.cs       # 메인 런처 로직 (버전 체크 → 업데이트 또는 실행)
├── VersionChecker.cs            # 버전 비교 및 원격 JSON 파싱
├── BackgroundUpdater.cs         # 업데이트 파일 다운로드
├── ConfigManager.cs             # launcher_config.json 관리
├── TaskSchedulerManager.cs      # Windows 작업 스케줄러 등록/해제
├── MainWindow.xaml              # 상태 표시 UI (선택적)
├── app.manifest                 # 관리자 권한 요구 설정
├── app_icon.ico                 # 로켓 아이콘
└── launcher_config.json         # 런처 설정 파일
```

## 참고사항

- 첫 실행 시 반드시 **관리자 권한**으로 실행해야 작업 스케줄러에 등록됩니다
- `launcher_config.json`이 없으면 자동 생성되므로, 생성 후 수정해야 합니다
- 로컬 버전 파일(`version.txt`)이 없으면 자동으로 `1.0.0`으로 생성됩니다
- 업데이트 서버는 HTTPS를 권장합니다
- 서버의 `version.json`에는 반드시 `version`과 `downloadUrl` 필드가 포함되어야 합니다
- 업데이트 파일은 EXE 형식으로 제공되며, 다운로드 후 자동으로 실행됩니다
- 작업 스케줄러를 통해 등록되므로 UAC 프롬프트 없이 자동 시작됩니다
- 런처는 백그라운드에서 실행되며 UI 창은 기본적으로 표시되지 않습니다
- 트레이 아이콘 더블클릭 시 상태 창을 볼 수 있습니다

## 라이선스

MIT License
