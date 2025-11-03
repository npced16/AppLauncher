# App Launcher

자동 업데이트 기능이 있는 C# WPF 런처 애플리케이션입니다.

## 주요 기능

- **자동 버전 체크**: 시작 시 원격 서버에서 최신 버전 확인
- **자동 업데이트**: 구버전 감지 시 자동으로 업데이트 진행
- **시작프로그램 등록**: Windows 시작 시 자동 실행
- **깔끔한 UI**: 모던한 다크 테마 UI
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
  "versionCheckUrl": "https://example.com/version.txt",
  "updateDownloadUrl": "https://example.com/update.zip",
  "localVersionFile": "version.txt",
  "targetDirectory": "C:\\Program Files\\YourApp"
}
```

### 설정 항목 설명

- **targetExecutable**: 실행할 대상 프로그램의 전체 경로
- **workingDirectory**: 대상 프로그램의 작업 디렉토리 (선택사항)
- **versionCheckUrl**: 버전 정보를 확인할 URL
  - 텍스트 형식: `1.0.5`
  - JSON 형식: `{"version": "1.0.5"}`
- **updateDownloadUrl**: 업데이트 파일(ZIP)을 다운로드할 URL
- **localVersionFile**: 로컬 버전 파일 경로
- **targetDirectory**: 업데이트 파일을 압축 해제할 대상 디렉토리

## 서버 설정

### 1. 버전 파일 (version.txt 또는 version.json)

**텍스트 형식 (version.txt):**
```
1.0.5
```

**JSON 형식 (version.json):**
```json
{
  "version": "1.0.5"
}
```

### 2. 업데이트 ZIP 파일

업데이트할 파일들을 ZIP으로 압축합니다. ZIP 파일 내부 구조는 대상 디렉토리와 동일해야 합니다.

예시:
```
update.zip
├── YourApp.exe
├── YourApp.dll
├── config.json
└── ...
```

## 동작 방식

1. **시작**: 런처가 실행되면 시작프로그램에 자동 등록
2. **버전 체크**: 원격 서버에서 최신 버전 확인
3. **업데이트 판단**:
   - 구버전: 자동 업데이트 진행
   - 최신 버전: 바로 프로그램 실행
4. **프로그램 실행**: 대상 프로그램 실행
5. **런처 종료**: 대상 프로그램이 실행되면 런처 자동 종료

## 시작프로그램 등록 해제

프로그램 코드에서 다음 메서드를 호출하여 시작프로그램 등록을 해제할 수 있습니다:

```csharp
AppLauncher.App.UnregisterStartup();
```

또는 Windows 설정에서 수동으로 제거:
- Windows 11: 설정 > 앱 > 시작 앱
- Windows 10: 설정 > 앱 > 시작 프로그램

## 요구사항

- .NET 8.0 이상
- Windows 10/11

## 라이선스

MIT License

## 참고사항

- 첫 실행 시 `launcher_config.json`이 자동 생성됩니다.
- 설정 파일을 환경에 맞게 수정해야 합니다.
- 업데이트 서버는 HTTPS를 권장합니다.
- ZIP 파일 내부 구조가 올바른지 확인하세요.
