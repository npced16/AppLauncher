# App Launcher

자동 업데이트 기능이 있는 C# WPF 트레이 런처 애플리케이션입니다.

## 주요 기능

- **트레이 앱 방식**: 시스템 트레이에서 백그라운드로 실행 (UI 없음)
- **자동 버전 체크**: 시작 시 원격 서버에서 최신 버전 확인
- **자동 업데이트**: 구버전 감지 시 자동으로 업데이트 진행
- **시작프로그램 등록**: Windows 시작 시 자동 실행
- **상태 확인**: 트레이 아이콘 우클릭 또는 더블클릭으로 상태 창 표시
- **자동 종료**: 대상 프로그램 실행 후 런처 자동 종료

## 빌드 방법

```bash
dotnet restore
dotnet build
```

또는 Visual Studio에서 솔루션을 열고 빌드합니다.

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
- **localVersionFile**: 로컬 버전 파일 경로

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

## 동작 방식

1. **시작**: 런처가 실행되면 시작프로그램에 자동 등록
2. **트레이 아이콘 표시**: 시스템 트레이에 아이콘 표시 (UI 창 없음)
3. **버전 체크**: 백그라운드에서 원격 서버의 최신 버전 확인 (JSON 파싱)
4. **업데이트 판단**:
   - **구버전**: 서버에서 제공한 downloadUrl로 EXE 파일 다운로드 → 다운로드한 EXE 실행 → 런처 종료
   - **최신 버전**: 설정된 대상 프로그램 실행 → 5초 후 런처 종료
5. **자동 종료**: 프로그램이 실행되면 5초 후 런처 자동 종료

### 트레이 아이콘 기능

- **더블클릭**: 상태 창 표시
- **우클릭 메뉴**:
  - 상태 보기: 현재 진행 상황을 보여주는 창 열기
  - 종료: 런처 즉시 종료

## 시작프로그램 등록 해제

프로그램 코드에서 다음 메서드를 호출하여 시작프로그램 등록을 해제할 수 있습니다:

```csharp
AppLauncher.App.UnregisterStartup();
```

또는 Windows 설정에서 수동으로 제거:
- Windows 11: 설정 > 앱 > 시작 앱
- Windows 10: 설정 > 앱 > 시작 프로그램

## 요구사항

- .NET 9.0
- Windows 10/11

## 라이선스

MIT License

## 참고사항

- 첫 실행 시 `launcher_config.json`이 자동 생성됩니다.
- 설정 파일을 환경에 맞게 수정해야 합니다.
- 업데이트 서버는 HTTPS를 권장합니다.
- 서버의 version.json에는 반드시 `version`과 `downloadUrl` 필드가 포함되어야 합니다.
- 업데이트 파일은 EXE 형식으로 제공되며, 다운로드 후 자동으로 실행됩니다.
- 트레이 아이콘은 Windows 기본 애플리케이션 아이콘을 사용합니다.
- 런처는 백그라운드에서 실행되며 UI 창은 기본적으로 표시되지 않습니다.
- 업데이트가 필요한 경우 다운로드한 EXE를 실행하고 런처가 종료됩니다.
