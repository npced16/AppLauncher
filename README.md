# AppLauncher

HBOT 챔버 소프트웨어 자동 실행 및 원격 업데이트 관리 프로그램

## 프로젝트 구조

```
AppLauncher/
├── Features/                           # 주요 기능 모듈
│   ├── AppLaunching/
│   │   └── ApplicationLauncher.cs      # 대상 프로그램 자동 실행
│   ├── MqttControl/
│   │   ├── MqttService.cs              # MQTT 클라이언트 서비스
│   │   └── MqttMessageHandler.cs       # MQTT 명령 처리 (업데이트, 위치 변경)
│   ├── TrayApp/
│   │   └── TrayApplicationContext.cs   # 시스템 트레이 앱
│   └── VersionManagement/
│       ├── LabViewUpdater.cs           # 챔버 소프트웨어 업데이트
│       ├── LauncherUpdater.cs          # 런처 자체 업데이트
│       ├── PendingUpdate.cs            # 업데이트 정보 모델
│       └── PendingUpdateManager.cs     # 업데이트 예약 관리
├── Presentation/                       # UI 레이어
│   └── WinForms/
│       ├── MqttControlForm.cs          # MQTT 제어 창
│       ├── MqttSettingsForm.cs         # MQTT 설정 창
│       ├── LauncherSettingsForm.cs     # 런처 설정 창
│       ├── UpdateProgressForm.cs       # 업데이트 진행 화면 (전체화면)
│       └── MainForm.cs                 # 메인 폼
├── Shared/                             # 공통 유틸리티
│   ├── Configuration/
│   │   └── ConfigManager.cs            # 설정 파일 관리
│   ├── HardwareInfo.cs                 # 하드웨어 정보 수집
│   ├── TaskSchedulerManager.cs         # 작업 스케줄러 관리
│   └── VersionInfo.cs                  # 버전 정보 관리
├── Properties/
│   └── AssemblyInfo.cs                 # 어셈블리 메타데이터
├── Program.cs                          # 진입점 및 초기화
├── AppLauncher.csproj                  # 프로젝트 파일
├── app.manifest                        # 관리자 권한 설정
├── app_icon.ico                        # 앱 아이콘
└── README.md                           # 이 파일
```

## 배포 경로 구조

### 프로그램 파일 (업데이트 시 교체)
```
C:\Program Files\AppLauncher\
  └─ AppLauncher.exe
```

### 데이터 파일 (업데이트해도 보존)
```
C:\ProgramData\AppLauncher\
  ├─ Data\                       (설정 파일)
  │   ├─ launcher_config.json    (런처 설정)
  │   └─ labview_version.txt     (설치된 버전)
  ├─ Downloads\                  (다운로드 임시 파일)
  │   ├─ [zip 파일]
  │   └─ Volume\                 (압축 해제된 설치 파일)
  ├─ Logs\                       (설치 로그)
  │   └─ install_log_*.txt
  └─ Backup\                     (설정 백업)
      └─ setting_*.ini
```

### 사용자 문서 폴더
```
C:\Users\[사용자]\Documents\HBOT\Setting\
  └─ setting.ini                 (HBOT Operator 설정 파일)
```

## 빌드 명령어

### 배포용 (단일 실행 파일)
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=false
```

### 개발/테스트용
```bash
dotnet watch run
```

## 주요 기능

### 1. 챔버 소프트웨어 자동 실행
- 시스템 시작 시 HBOT Operator 자동 실행
- 프로세스 모니터링 및 자동 재시작
- 작업 스케줄러를 통한 자동 시작

### 2. 원격 업데이트 관리 (MQTT)
- **챔버 소프트웨어 업데이트**: ZIP 파일 다운로드 → 압축 해제 → 자동 설치
- **런처 자체 업데이트**: EXE 파일 다운로드 → 교체 → 재시작 시 적용
- **위치 변경**: MQTT Location 설정 변경

### 3. 안전한 업데이트 프로세스
- 메타데이터 검증 (ProductName, CompanyName)
- 설정 파일 자동 백업 및 복원
- fonts_install 프로세스 자동 정리
- 구버전 파일 자동 정리

## 업데이트 플로우

### 챔버 소프트웨어 업데이트
```mermaid
flowchart TD
    Start([MQTT 명령 수신]) --> Download[파일 다운로드]
    Download --> Extract[압축 해제]
    Extract --> Validate[메타데이터 검증]
    Validate -->|성공| Backup[설정 백업]
    Validate -->|실패| Manual[실패]
    Backup --> Install[PowerShell 자동 설치]
    Install --> Monitor[fonts_install 모니터링]
    Monitor --> Restore[설정 복원]
    Restore --> Reboot[시스템 재시작]
```

### 런처 업데이트
```mermaid
flowchart TD
    Start([MQTT 명령 수신]) --> Download[EXE 다운로드]
    Download --> Validate[메타데이터 검증]
    Validate -->|성공| Replace[파일 교체]
    Validate -->|실패| Fail[실패]
    Replace --> Rename[구버전 .old로 이름 변경]
    Rename --> Notify[토스트 알림]
    Notify --> Wait[재시작 대기]
    Restart[컴퓨터 재시작]
    Restart --> Cleanup[.old 파일 삭제]
    Cleanup --> Success([새 버전 실행])
```

## MQTT 명령어

### 챔버 소프트웨어 업데이트
```json
{
  "command": "LABVIEW_UPDATE",
  "URL": "https://example.com/update.zip",
  "version": "1.0.0.285"
}
```

### 런처 업데이트
```json
{
  "command": "LAUNCHER_UPDATE",
  "URL": "https://example.com/AppLauncher.exe"
}
```

### 위치 변경
```json
{
  "command": "LOCATION_CHANGE",
  "location": "Seoul"
}
```

### 상태 확인
```json
{
  "command": "STATUS"
}
```

## 설정 파일 (launcher_config.json)

```json
{
  "targetExecutable": "C:\\Program Files (x86)\\HBOT Operator\\HBOT Operator.exe",
  "localVersionFile": "version.txt",
  "mqttSettings": {
    "broker": "server3.ibexserver.com",
    "port": 1883,
    "location": ""
  }
}
```

## 개발 환경

- **.NET 9.0** (Windows Forms)
- **MQTTnet** 4.3.7.1207
- **Newtonsoft.Json** 13.0.3

## 라이선스

본 프로젝트는 HBOT 챔버 시스템 관리를 위한 내부 도구입니다.
