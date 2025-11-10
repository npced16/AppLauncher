# AppLauncher

MQTT를 통한 원격 제어가 가능한 애플리케이션 런처 및 업데이트 관리 도구

## 주요 기능


//powersell 윈도우 7 이상부터 기본내장
powershell -Command "Start-Process '.\setup.exe' -ArgumentList '/q','/AcceptLicenses','yes','/log','C:\install_log.txt' -Verb RunAs"


//강제종료추가
powershell -Command "Start-Process '.\setup.exe' -ArgumentList '/q','/AcceptLicenses','yes','/log','C:\install_log.txt' -Verb RunAs; Start-Sleep -Seconds 1800; Get-Process 'setup' -ErrorAction SilentlyContinue | Stop-Process -Force"

### 1. 원격 제어 (MQTT)
- MQTT 브로커를 통한 원격 명령 수신
- **런처 자체 업데이트 제어**: MQTT 명령으로 런처 프로그램 자동 업데이트
- **대상 앱 업데이트 제어**: MQTT 명령으로 관리 대상 애플리케이션 자동 업데이트
- **중앙 관제**: 연결 시 자동으로 현재 버전 정보 보고 (런처 버전 + 대상 앱 버전)
- 실시간 상태 모니터링 및 응답

### 2. MQTT 기반 자동 업데이트
- 중앙 서버에서 MQTT를 통해 모든 업데이트 관리
- 두 가지 독립적인 업데이트 경로:
  - 런처(실행기) 자체 업데이트
  - 대상 애플리케이션 업데이트
5- 다운로드 URL과 버전 정보를 MQTT 명령에 포함
- 실행 중인 프로세스 종료 및 파일 교체
- 백업 및 복구 기능

### 3. 트레이 앱
- 시스템 트레이에서 상시 실행
- **중복 실행 방지** (Mutex 사용)
- Windows 시작 프로그램 자동 등록 (작업 스케줄러)
- 관리자 권한 자동 획득

### 4. 설정 관리
- **AppData에 설정 저장** (`%AppData%\AppLauncher\`)
- 런처 업데이트 후에도 설정 유지
- UI를 통한 MQTT 설정 관리
- MQTT 제어 센터 (연결 상태, 로그 확인)

## 빌드 방법

### 일반 빌드

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

### 단일 파일 배포용 빌드 (GitHub Releases용)

GitHub Releases에 업로드하거나 MQTT를 통해 배포할 단일 EXE 파일을 생성하려면:

```bash
dotnet publish --configuration Release --runtime win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

빌드된 파일 위치:
- `bin\Release\net9.0-windows\win-x64\publish\AppLauncher.exe` (약 1.5MB)

이 파일은:
- 단일 실행 파일로 패키징됨
- .NET 9 런타임 필요 (설치되어 있어야 함)
- GitHub Releases에 업로드하여 직접 다운로드 링크로 사용 가능

### GitHub Releases에 업로드

1. **GitHub 리포지토리의 Releases 페이지로 이동**
   - `https://github.com/YOUR_USERNAME/AppLauncher/releases`

2. **새 릴리스 생성**
   - "Draft a new release" 클릭

3. **릴리스 정보 입력**
   - **Tag version**: `v1.0.0` (버전 번호)
   - **Release title**: `AppLauncher v1.0.0`
   - **Description**: 릴리스 노트 작성

4. **EXE 파일 업로드**
   - "Attach binaries..." 클릭
   - `bin\Release\net9.0-windows\win-x64\publish\AppLauncher.exe` 파일 드래그 앤 드롭

5. **릴리스 퍼블리시**
   - "Publish release" 클릭

### 다운로드 링크 사용

릴리스가 퍼블리시되면 다음과 같은 **직접 다운로드 링크**를 얻게 됩니다:

```
https://github.com/YOUR_USERNAME/AppLauncher/releases/download/v1.0.0/AppLauncher.exe
```

이 링크는:
- 브라우저에서 바로 다운로드됨
- WPF 애플리케이션에서 HttpClient로 다운로드 가능
- MQTT 업데이트 명령의 `downloadUrl`로 사용 가능

**MQTT 업데이트 명령 예시:**
```json
{
  "command": "update_launcher",
  "downloadUrl": "https://github.com/YOUR_USERNAME/AppLauncher/releases/download/v1.0.0/AppLauncher.exe",
  "version": "1.0.0"
}
```

## 설정 방법

`launcher_config.json` 파일을 수정하여 설정합니다:

```json
{
  "targetExecutable": "C:\\Program Files\\YourApp\\YourApp.exe",
  "workingDirectory": "C:\\Program Files\\YourApp",
  "versionCheckUrl": "https://example.com/target_app_version.json",
  "localVersionFile": "version.txt",
  "launcherVersionFile": "launcher_version.txt",
  "mqttSettings": {
    "broker": "mqtt.example.com",
    "port": 1883,
    "clientId": "AppLauncher_PC01",
    "topic": "applauncher/commands",
  }
}
```

### 설정 항목 설명

#### 대상 애플리케이션 설정
- **targetExecutable**: 실행할 대상 프로그램의 전체 경로
- **workingDirectory**: 대상 프로그램의 작업 디렉토리 (선택사항)
- **versionCheckUrl**: 대상 앱 버전 정보를 확인할 URL (JSON 형식, 레거시 모드에서만 사용)
- **localVersionFile**: 대상 앱 로컬 버전 파일 경로 (버전 추적용)

#### 런처 버전 추적
- **launcherVersionFile**: 런처 로컬 버전 파일 경로 (기본값: launcher_version.txt)

#### MQTT 설정 (선택사항)
- **broker**: MQTT 브로커 주소 (기본값:localHost)
- **port**: MQTT 브로커 포트 (기본값: 1883)
- **clientId**: MQTT 클라이언트 ID (기본값: AppLauncher)
- **topic**: 명령을 수신할 MQTT 토픽 (기본값: applauncher/commands)
- **username**: MQTT 인증 사용자명 (선택사항)
- **password**: MQTT 인증 비밀번호 (선택사항)

## MQTT 원격 제어

### MQTT 명령 형식

MQTT를 통해 프로그램을 원격으로 실행하려면 다음 형식의 JSON 메시지를 발행합니다:

#### 프로그램 실행 명령

**방법 1: 다운로드 URL 사용 (권장)**
```json
{
  "command": "launch",
  "downloadUrl": "https://example.com/YourApp.exe",
  "workingDirectory": "C:\\temp",
  "arguments": "--arg1 value1"
}
```

**방법 2: 로컬 실행 파일 경로 사용**
```json
{
  "command": "launch",
  "executable": "C:\\Program Files\\YourApp\\YourApp.exe",
  "workingDirectory": "C:\\Program Files\\YourApp",
  "arguments": "--arg1 value1"
}
```

**방법 3: 기본 설정 파일 사용**
```json
{
  "command": "start"
}
```
(설정 파일의 `targetExecutable`을 실행)

#### 상태 확인 명령
```json
{
  "command": "status"
}
```

#### 런처 자체 업데이트 명령
```json
{
  "command": "update_launcher",
  "downloadUrl": "https://example.com/AppLauncher-2.1.0.exe",
  "version": "2.1.0"
}
```

이 명령은:
1. MQTT 메시지에서 제공된 다운로드 URL로 새 버전을 다운로드합니다
2. 현재 실행 중인 런처를 종료하고 새 버전으로 교체합니다
3. 새 버전을 자동으로 실행하고 재시작합니다

**필수 필드:**
- `downloadUrl`: 새 런처 EXE 파일의 다운로드 URL
- `version`: 새 버전 번호 (예: "2.1.0")

**참고**: 런처 업데이트 후에도 `%AppData%\AppLauncher\`에 저장된 설정은 유지됩니다.

### MQTT 명령 필드 설명

#### 프로그램 실행 명령 (launch, start)
- **command**: 명령 종류 (`launch`, `start`, `status`, `update_launcher`)
- **downloadUrl**: 다운로드할 EXE 파일 URL (우선순위 1)
- **executable**: 실행할 로컬 프로그램 경로 (우선순위 2)
- **workingDirectory**: 작업 디렉토리 (선택사항)
- **arguments**: 실행 인자 (선택사항)

**실행 우선순위:**
1. `downloadUrl`이 있으면: 파일 다운로드 후 실행
2. `executable`이 있으면: 해당 경로의 파일 실행
3. 둘 다 없으면: 설정 파일의 `targetExecutable` 실행

#### 업데이트 명령 (update_launcher)
- **command**: `update_launcher` 또는 `updatelauncher`
- 추가 필드 불필요 (설정 파일의 `launcherUpdateUrl` 사용)

### MQTT 명령 예제 (Python)

#### 다운로드 URL로 실행
```python
import paho.mqtt.client as mqtt
import json

# MQTT 클라이언트 생성
client = mqtt.Client()
client.username_pw_set("your_username", "your_password")
client.connect("mqtt.example.com", 1883)

# 다운로드 URL로 프로그램 실행
command = {
    "command": "launch",
    "downloadUrl": "https://example.com/MyApp.exe"
}
client.publish("applauncher/commands", json.dumps(command))
client.disconnect()
```

#### 로컬 파일로 실행
```python
import paho.mqtt.client as mqtt
import json

# MQTT 클라이언트 생성
client = mqtt.Client()
client.username_pw_set("your_username", "your_password")
client.connect("mqtt.example.com", 1883)

# 로컬 파일 실행
command = {
    "command": "launch",
    "executable": "C:\\Windows\\System32\\notepad.exe"
}
client.publish("applauncher/commands", json.dumps(command))
client.disconnect()
```

#### 런처 자체 업데이트
```python
import paho.mqtt.client as mqtt
import json

# MQTT 클라이언트 생성
client = mqtt.Client()
client.username_pw_set("your_username", "your_password")
client.connect("mqtt.example.com", 1883)

# 런처 업데이트 명령 (다운로드 URL과 버전 포함)
command = {
    "command": "update_launcher",
    "downloadUrl": "https://example.com/AppLauncher-2.1.0.exe",
    "version": "2.1.0"
}
client.publish("applauncher/commands", json.dumps(command))
client.disconnect()
```

## 중앙 관제 (MQTT 기반)

### 자동 버전 정보 보고

런처는 MQTT 브로커에 연결되면 자동으로 현재 버전 정보를 중앙 서버에 보고합니다.

#### 보고되는 정보

```json
{
  "type": "version_report",
  "clientId": "AppLauncher_PC01",
  "hostname": "DESKTOP-ABC123",
  "username": "user",
  "timestamp": "2025-01-15T10:30:45",
  "launcher": {
    "version": "2.0.5",
    "executablePath": "C:\\Program Files\\AppLauncher\\AppLauncher.exe"
  },
  "targetApp": {
    "version": "1.3.2",
    "executablePath": "C:\\Program Files\\YourApp\\YourApp.exe"
  }
}
```

#### 버전 정보가 발행되는 토픽

- **토픽**: `applauncher/status` (명령 토픽에서 `/commands`를 `/status`로 대체)
- **발행 시점**: MQTT 브로커 연결 성공 시 자동 발행

### 중앙 서버 구축 예제 (Python)

```python
import paho.mqtt.client as mqtt
import json
from datetime import datetime

# 클라이언트별 버전 정보 저장
client_versions = {}

def on_message(client, userdata, message):
    try:
        payload = json.loads(message.payload.decode())

        # 버전 정보 보고 수신
        if payload.get("type") == "version_report":
            client_id = payload.get("clientId")
            client_versions[client_id] = {
                "hostname": payload.get("hostname"),
                "launcher_version": payload["launcher"]["version"],
                "target_app_version": payload["targetApp"]["version"],
                "last_seen": payload.get("timestamp"),
                "updated_at": datetime.now().isoformat()
            }

            print(f"[{client_id}] 버전 정보 수신:")
            print(f"  - 런처: {payload['launcher']['version']}")
            print(f"  - 대상 앱: {payload['targetApp']['version']}")

            # 업데이트가 필요한 경우 명령 전송
            if payload['launcher']['version'] < "2.1.0":
                send_launcher_update(client, client_id)

def send_launcher_update(client, client_id):
    """특정 클라이언트에게 런처 업데이트 명령 전송"""
    command = {
        "command": "update_launcher",
        "downloadUrl": "https://example.com/AppLauncher-2.1.0.exe",
        "version": "2.1.0"
    }
    # 클라이언트별 토픽 사용 가능
    client.publish("applauncher/commands", json.dumps(command))
    print(f"[{client_id}]에게 업데이트 명령 전송")

# MQTT 클라이언트 설정
client = mqtt.Client()
client.username_pw_set("admin", "admin_password")
client.on_message = on_message

client.connect("mqtt.example.com", 1883)
client.subscribe("applauncher/status")  # 상태 토픽 구독
client.loop_forever()
```

### 중앙 관제의 장점

1. **실시간 모니터링**: 모든 클라이언트의 현재 버전을 실시간으로 확인
2. **선택적 업데이트**: 특정 버전의 클라이언트에게만 업데이트 명령 전송
3. **중앙 집중식 관리**: 별도의 버전 파일 서버 없이 MQTT만으로 모든 관리
4. **유연한 배포**: 단계적 업데이트(Canary Deployment) 가능

## 서버 설정 (레거시 모드)

### 버전 JSON 파일 (version.json) - 선택사항

MQTT 없이 자동 업데이트를 사용하려면 서버에 다음과 같은 형식의 JSON 파일을 호스팅합니다:

#### 대상 애플리케이션 버전 파일 (target_app_version.json)
```json
{
  "version": "1.0.5",
  "downloadUrl": "https://example.com/YourApp-1.0.5.exe"
}
```

**필드 설명:**
- **version**: 최신 버전 번호 (예: "1.0.5")
- **downloadUrl**: 업데이트 EXE 파일의 다운로드 URL

**참고**: MQTT 모드에서는 이 파일이 불필요합니다. 모든 업데이트는 MQTT 명령으로 처리됩니다.

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

4. **MQTT 서비스 시작 (설정된 경우)**
   - `launcher_config.json`에 `mqttSettings`가 있으면 MQTT 브로커에 연결
   - 설정된 토픽을 구독하여 원격 명령 대기

5. **동작 모드**
   - **MQTT 모드**: MQTT 메시지를 통해 프로그램 실행 (트레이에 상주)
   - **레거시 모드**: 버전 체크 후 자동 실행 및 종료 (기존 방식)

### 이후 실행 (자동 시작)

1. **Windows 로그온 시 자동 실행**
   - 작업 스케줄러를 통해 자동으로 시작
   - UAC 프롬프트 없이 관리자 권한으로 실행됨

2. **백그라운드 실행**
   - 트레이 아이콘만 표시
   - MQTT 메시지 수신 대기 또는 자동 업데이트 진행

### 트레이 아이콘 기능

- **더블클릭**: 상태 창 표시
- **우클릭 메뉴**:
  - **상태 보기**: 현재 진행 상황을 보여주는 창 열기
  - **MQTT 제어 센터**: MQTT 연결 상태, 로그, 클라이언트 정보 확인
  - **MQTT 설정**: MQTT 브로커 주소, 포트, 클라이언트 ID 등 설정 변경
  - **런처 업데이트 확인**: 런처 자체 업데이트 수동 실행
  - **종료**: 런처 즉시 종료


### 방법 1: 트레이 메뉴 사용
1. 트레이 아이콘 우클릭
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

## 프로젝트 구조 (Feature-based)

```
AppLauncher/
├── Features/                           # 기능별 모듈
│   ├── AppLaunching/                   # 앱 실행 관리
│   │   └── ApplicationLauncher.cs      # 프로그램 실행 로직
│   ├── MqttControl/                    # MQTT 통신
│   │   └── MqttService.cs              # MQTT pub/sub 서비스
│   ├── TrayApp/                        # 트레이 앱
│   │   └── TrayApplicationContext.cs   # 트레이 아이콘 및 메뉴
│   └── VersionManagement/              # 버전 관리
│       ├── VersionChecker.cs           # 버전 비교
│       └── BackgroundUpdater.cs        # 파일 다운로드
├── Presentation/                       # UI 레이어
│   └── WPF/
│       ├── App.xaml                    # WPF 애플리케이션
│       ├── App.xaml.cs                 # 시작 로직, 작업 스케줄러 등록
│       └── MainWindow.xaml             # 상태 표시 창
├── Shared/                             # 공유 코드
│   ├── Configuration/
│   │   └── ConfigManager.cs            # 설정 관리
│   └── TaskScheduler/
│       └── TaskSchedulerManager.cs     # 작업 스케줄러
├── app.manifest                        # 관리자 권한 설정
├── app_icon.ico                        # 로켓 아이콘
└── launcher_config.json                # 런처 설정
```

## 업데이트 시스템 상세

### 두 가지 독립적인 업데이트 경로

AppLauncher는 두 가지 독립적인 업데이트 메커니즘을 제공합니다:

#### 1. 런처 자체 업데이트
- **설정 필드**: `launcherUpdateUrl`, `launcherVersionFile`
- **MQTT 명령**: `update_launcher` 또는 `updatelauncher`
- **트레이 메뉴**: "런처 업데이트 확인" 클릭
- **동작 과정**:
  1. 서버의 `launcher_version.json`에서 최신 버전 확인
  2. 업데이트가 필요하면 새 EXE 다운로드
  3. 현재 실행 중인 `AppLauncher.exe` 프로세스 종료
  4. 기존 파일을 `.backup`으로 백업
  5. 다운로드한 파일로 교체
  6. 새 버전 자동 실행
- **설정 유지**: `%AppData%\AppLauncher\` 폴더에 저장된 설정은 업데이트 후에도 보존됨

#### 2. 대상 애플리케이션 업데이트
- **설정 필드**: `versionCheckUrl`, `localVersionFile`
- **MQTT 명령**: `launch` 명령으로 새 버전의 `downloadUrl` 제공
- **동작 과정**:
  1. 서버의 `target_app_version.json`에서 최신 버전 확인
  2. 업데이트가 필요하면 새 EXE 다운로드
  3. 대상 앱 프로세스 종료 (실행 중인 경우)
  4. 파일 교체
  5. 업데이트된 앱 실행

### MQTT 제어 센터

트레이 메뉴에서 "MQTT 제어 센터"를 통해 다음 정보를 확인할 수 있습니다:
- **연결 상태**: 연결됨 / 연결 안됨
- **브로커 정보**: `broker:port`
- **클라이언트 ID**: 현재 MQTT 클라이언트 ID
- **구독 토픽**: 현재 구독 중인 토픽
- **실시간 로그**: MQTT 메시지 수신 및 연결 상태 변경 로그

## 참고사항

- 첫 실행 시 반드시 **관리자 권한**으로 실행해야 작업 스케줄러에 등록됩니다
- `launcher_config.json`이 없으면 자동 생성되므로, 생성 후 수정해야 합니다
- **MQTT 모드**: `mqttSettings`를 설정하면 트레이에 상주하며 MQTT 명령 대기
- **레거시 모드**: MQTT 미설정 시 버전 체크 후 자동 실행 및 종료
- **중복 실행 방지**: Mutex를 사용하여 동시에 여러 인스턴스가 실행되지 않도록 보장
- MQTT를 통해 다운로드 URL을 받으면 자동으로 파일 다운로드 후 실행
- 다운로드한 파일은 임시 폴더에 저장되며, 실행 후 자동 삭제되지 않음
- 업데이트 서버는 HTTPS를 권장합니다
- 작업 스케줄러를 통해 등록되므로 UAC 프롬프트 없이 자동 시작됩니다
- 런처는 백그라운드에서 실행되며 UI 창은 기본적으로 표시되지 않습니다
- 트레이 아이콘 더블클릭 시 상태 창을 볼 수 있습니다
- **설정 위치**: 모든 설정은 `%AppData%\AppLauncher\launcher_config.json`에 저장되어 런처 업데이트 후에도 유지됩니다

## 사용된 라이브러리

- **MQTTnet 5.0.1**: MQTT 클라이언트 라이브러리
- **Newtonsoft.Json 13.0.3**: JSON 직렬화/역직렬화

## 라이선스

MIT License
