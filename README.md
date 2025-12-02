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
│       └── PendingUpdateManager.cs     # 업데이트 예약 관리
├── Presentation/                       # UI 레이어
│   └── WinForms/
│       ├── MqttControlForm.cs          # MQTT 제어 창
│       ├── LauncherSettingsForm.cs     # 런처 설정 창 (MQTT 정보 포함)
│       ├── UpdateProgressForm.cs       # 업데이트 진행 화면 (전체화면)
│       └── MainForm.cs                 # 메인 폼
├── Shared/                             # 공통 유틸리티
│   ├── Configuration/
│   │   └── ConfigManager.cs            # 설정 파일 관리
│   ├── Logger/
│   │   ├── DebugLogger.cs              # 디버그 로깅
│   │   └── FileLogger.cs               # MQTT 로그 파일 관리
│   ├── Services/
│   │   ├── ServiceContainer.cs         # 전역 서비스 컨테이너
│   │   ├── FontInstallMonitor.cs       # fonts_install 프로세스 모니터링
│   │   └── UninstallSWService.cs       # 소프트웨어 제거 서비스
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
  ├─ Logs\                       (로그 파일 - 90일 자동 보관)
  │   ├─ MQTT_YYYYMMDD.log       (MQTT 통신 로그)
  │   └─ install_log_*.txt       (설치 로그)
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
- **통신 로그 기록**: 모든 MQTT 활동을 파일로 자동 기록 (90일 보관)

### 3. 업데이트 프로세스
- 메타데이터 검증 (ProductName, CompanyName)
- 설정 파일 자동 백업 및 복원
- fonts_install 프로세스 자동 정리
- 구버전 파일 자동 정리

### 4. MQTT 로그 관리
- **자동 파일 기록**: 연결, 메시지 수신/전송, 오류 등 모든 이벤트를 파일로 기록
- **날짜별 로그 파일**: `MQTT_YYYYMMDD.log` 형식으로 일별 파일 생성
- **자동 정리**: 90일(3개월) 이전 로그 파일 자동 삭제
- **Tray 앱 독립**: 앱이 종료되어도 백그라운드에서 계속 기록
- **로그 위치**: `C:\ProgramData\AppLauncher\Logs\`

## LabVIEW 업데이트 파일 구조

### ZIP 파일 구조 (HBOT.Operator.zip)
```
HBOT.Operator.zip
├── HBOT Operator.exe          # 실행 파일 (메타데이터 검증 대상)
├── HBOT Operator.ini          # 설정 파일
├── HBOT Operator.aliases      # 별칭 파일
├── data/                      # 사운드 리소스
│   ├── 1.wav ~ 8.wav         # 사운드 파일
│   ├── Alarm.wav
│   ├── medium alarm.wav
│   └── lvsound2.dll
└── Volume/                    # 설치 패키지
    ├── setup.exe             # 설치 실행 파일
    ├── setup.ini
    ├── nidist.id
    ├── bin/                  # 설치 컴포넌트 (p0 ~ p83, dp)
    ├── license/              # 라이선스 파일
    └── supportfiles/         # 지원 파일
```

**압축 해제 후 처리 과정:**
1. `HBOT Operator.exe` 메타데이터 검증
   - ProductName: "HBOT Operator"
   - CompanyName: "Ibex Medical Systems"
2. 검증 성공 시 `Volume/setup.exe` 실행
3. 검증 실패 시 기존 설치된 HBOT Operator 실행

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

## MQTT 통신 데이터 포맷

### 토픽 구조
- **명령 수신 토픽**: `device/{clientId}/commands`
- **상태 발행 토픽**: `device/{clientId}/status`
- **설치 상태 토픽**: `device/{clientId}/installStatus`

### 자동 상태 업데이트
런처는 다음과 같이 자동으로 상태를 전송합니다:
1. **연결 시**: MQTT 브로커 연결 성공 시 즉시 `"connected"` 상태 전송
2. **주기적**: 연결 유지 중 1분마다 `"running"` 상태 전송

---

### 1. STATUS - 상태 확인 요청

서버에서 클라이언트의 현재 상태를 요청합니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "STATUS"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

**응답** (`device/{clientId}/status`)
```json
{
  "status": "response",
  "payload": {
    "labviewStatus": {
      "processName": "HBOT Operator",
      "status": "running",
      "responding": true,
      "memoryMB": 245,
      "threadCount": 18,
      "startTime": "2025-01-21T09:30:15",
      "runningTime": "01:23:45",
      "totalProcessorTime": "00:12:34"
    },
    "location": "Seoul",
    "hardwareUUID": "ABC123-DEF456-GHI789",
    "launcher": {
      "version": "1.2.1"
    },
    "targetApp": {
      "version": "1.0.0.285"
    }
  },
  "timestamp": "2025-01-21T10:54:00"
}
```

**LabVIEW가 실행 중이 아닐 때**
```json
{
  "status": "response",
  "payload": {
    "labviewStatus": {
      "processName": null,
      "status": "stopped",
      "responding": null,
      "memoryMB": null,
      "threadCount": null,
      "startTime": null,
      "runningTime": null,
      "totalProcessorTime": null
    },
    "location": "Seoul",
    "hardwareUUID": "ABC123-DEF456-GHI789",
    "launcher": {
      "version": "1.2.1"
    },
    "targetApp": {
      "version": "1.0.0.285"
    }
  },
  "timestamp": "2025-01-21T10:54:00"
}
```
</details>

---

### 2. LABVIEW_UPDATE_IMMEDIATE - 챔버 소프트웨어 즉시 업데이트

런처를 즉시 재시작하여 업데이트를 진행합니다. 업데이트 완료 후 시스템이 재부팅됩니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "LABVIEW_UPDATE_IMMEDIATE",
  "URL": "https://example.com/hbot_update_v1.0.0.285.zip",
  "version": "1.0.0.285"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

**설치 진행 상태** (`device/{clientId}/installStatus`)
```json
{
  "status": "download_start",
  "message": "파일 다운로드 시작",
  "timestamp": "2025-01-21T10:55:00"
}
```
```json
{
  "status": "download_complete",
  "message": "파일 다운로드 완료",
  "timestamp": "2025-01-21T10:56:30"
}
```
```json
{
  "status": "extract_start",
  "message": "압축 해제 시작",
  "timestamp": "2025-01-21T10:56:35"
}
```
```json
{
  "status": "extract_done",
  "message": "압축 해제 완료",
  "timestamp": "2025-01-21T10:57:00"
}
```
```json
{
  "status": "installation_start",
  "message": "설치 시작",
  "timestamp": "2025-01-21T10:57:05"
}
```
```json
{
  "status": "installation_complete",
  "message": "설치 완료",
  "timestamp": "2025-01-21T11:15:30"
}
```
```json
{
  "status": "restore_start",
  "message": "설정 파일 복원 시작",
  "timestamp": "2025-01-21T11:15:35"
}
```
```json
{
  "status": "restore_done",
  "message": "설정 파일 복원 완료",
  "timestamp": "2025-01-21T11:15:38"
}
```
```json
{
  "status": "version_saved",
  "message": "버전 파일 저장: 1.0.0.285",
  "timestamp": "2025-01-21T11:15:40"
}
```
</details>

---

### 3. LABVIEW_UPDATE - 챔버 소프트웨어 예약 업데이트

업데이트를 예약하고 다음 런처 재시작 시 자동으로 업데이트를 진행합니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "LABVIEW_UPDATE",
  "URL": "https://example.com/hbot_update_v1.0.0.285.zip",
  "version": "1.0.0.285"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

업데이트가 예약되며, 실제 설치는 다음 런처 재시작 시 진행됩니다.
설치 시작 시 `LABVIEW_UPDATE_IMMEDIATE`와 동일한 진행 상태 메시지가 전송됩니다.
</details>

---

### 4. LAUNCHER_UPDATE - 런처 자체 업데이트

런처 프로그램을 업데이트합니다. 컴퓨터 재시작 시 새 버전이 적용됩니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "LAUNCHER_UPDATE",
  "URL": "https://example.com/AppLauncher.exe"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

런처 업데이트는 백그라운드에서 조용히 진행되며, 파일 교체 후 컴퓨터 재시작 시 새 버전이 실행됩니다.
오류 발생 시에만 `installStatus` 토픽으로 에러 메시지가 전송됩니다.
</details>

---

### 5. LOCATION_CHANGE - 위치 정보 변경

클라이언트의 설치 위치 정보를 변경합니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "LOCATION_CHANGE",
  "location": "Seoul"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

**성공 응답** (`device/{clientId}/installStatus`)
```json
{
  "status": "location_changed",
  "message": "Location changed from 'Busan' to 'Seoul'",
  "timestamp": "2025-01-21T10:55:00"
}
```

**실패 응답**
```json
{
  "status": "error",
  "message": "Location is empty",
  "timestamp": "2025-01-21T10:55:00"
}
```
</details>

---

### 6. SETTINGS_UPDATE - setting.ini 파일 업데이트

챔버 소프트웨어의 설정 파일을 원격으로 업데이트합니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "SETTINGS_UPDATE",
  "settingContent": "[Version]\nVersion = 2.1\n\n[Model]\nModel = 1\n\n[Pressure]\nUsePress = 2600\nVentPress = 0"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

**성공 응답** (`device/{clientId}/status`)
```json
{
  "status": "settings_updated",
  "settingContent": "[Version]\nVersion = 2.1\n\n[Model]\nModel = 1\n\n[Pressure]\nUsePress = 2600\nVentPress = 0",
  "timestamp": "2025-01-21T10:55:00"
}
```

**실패 응답** (`device/{clientId}/installStatus`)
```json
{
  "status": "error",
  "message": "settingContent is empty",
  "timestamp": "2025-01-21T10:55:00"
}
```

</details>

---

### 7. SETTINGS_GET - setting.ini 파일 조회

현재 설정 파일의 내용을 조회합니다.

**요청 형식** (`device/{clientId}/commands`)
```json
{
  "command": "SETTINGS_GET"
}
```

<details>
<summary><b>응답 형식</b> (클릭하여 펼치기)</summary>

**성공 응답** (`device/{clientId}/status`)
```json
{
  "status": "settings_content",
  "settingContent": "[Version]\nVersion = 2.1\n\n[Model]\nModel = 1\n\n[Pressure]\nUsePress = 2600\nVentPress = 0",
  "timestamp": "2025-01-21T10:55:00"
}
```

**실패 응답** (`device/{clientId}/installStatus`)
```json
{
  "status": "error",
  "message": "File not found: C:\\Users\\...\\HBOT\\Setting\\setting.ini",
  "timestamp": "2025-01-21T10:55:00"
}
```
</details>

---

### 8. 자동 상태 전송

#### 8-1. Connected (연결 시)
MQTT 브로커에 연결 성공 시 자동으로 전송됩니다.

**전송 형식** (`device/{clientId}/status`)
```json
{
  "status": "connected",
  "payload": {
    "labviewStatus": {
      "processName": "HBOT Operator",
      "status": "running",
      "responding": true,
      "memoryMB": 245,
      "threadCount": 18,
      "startTime": "2025-01-21T09:30:15",
      "runningTime": "01:23:45",
      "totalProcessorTime": "00:12:34"
    },
    "location": "Seoul",
    "hardwareUUID": "ABC123-DEF456-GHI789",
    "launcher": {
      "version": "1.2.1"
    },
    "targetApp": {
      "version": "1.0.0.285"
    }
  },
  "timestamp": "2025-01-21T10:54:00"
}
```

#### 8-2. Running (주기적)
MQTT 연결 유지 중 1분마다 자동으로 전송됩니다.

**전송 형식** (`device/{clientId}/status`)
```json
{
  "status": "running",
  "payload": {
    "labviewStatus": {
      "processName": "HBOT Operator",
      "status": "running",
      "responding": true,
      "memoryMB": 245,
      "threadCount": 18,
      "startTime": "2025-01-21T09:30:15",
      "runningTime": "01:24:45",
      "totalProcessorTime": "00:12:39"
    },
    "location": "Seoul",
    "hardwareUUID": "ABC123-DEF456-GHI789",
    "launcher": {
      "version": "1.2.1"
    },
    "targetApp": {
      "version": "1.0.0.285"
    }
  },
  "timestamp": "2025-01-21T10:55:00"
}
```

#### 8-3. LabVIEW 업데이트 요청 (파일 미존재 시)
대상 프로그램이 존재하지 않을 때 자동으로 전송됩니다.

**전송 형식** (`device/{clientId}/status`)
```json
{
  "status": "labview_update_request",
  "payload": {
    "reason": "file_not_found",
    "location": "Seoul",
    "hardwareUUID": "ABC123-DEF456-GHI789",
    "launcher": {
      "version": "1.2.1"
    },
    "targetApp": {
      "version": "0.0.0"
    }
  },
  "timestamp": "2025-01-21T10:54:00"
}
```

## 설정 파일 (launcher_config.json)

```json
{
  "targetExecutable": "C:\\Program Files (x86)\\HBOT Operator\\HBOT Operator.exe",
  "localVersionFile": "labview_version.txt",
  "mqttSettings": {
    "broker": "localhost",
    "port": 1883,
    "location": ""
  }
}
```

## 개발 환경

- **.NET 9.0** (Windows Forms)
- **MQTTnet** 4.3.7.1207
- **Newtonsoft.Json** 13.0.3

## 주요 개선 사항

### v1.1.0 (2025-12-02)
- ✅ MQTT 파일 로깅 시스템 추가 (90일 자동 보관)
- ✅ ServiceContainer 패턴 도입으로 전역 서비스 관리 개선
- ✅ ObjectDisposedException 방어 코드 추가 (MqttControlForm)
- ✅ Null 안전성 개선 (`_sendStatusResponse` 등)
- ✅ 빌드 경고 개선 (34개 → 24개)
- ✅ 이벤트 핸들러 메모리 누수 방지
- ✅ MQTT 자동 재연결 로직 안정화 (최대 100회 제한)

## 라이선스

```text
 _______________________________________________________________________________________
< 본 프로젝트는 HBOT 챔버 시스템 관리를 위한 내부 도구입니다. >
 ----------------------------------------------------------------------------------------
        \
         \
          /\_/\  
         ( o.o )  
          > ^ <

```