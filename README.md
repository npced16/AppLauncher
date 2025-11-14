# AppLauncher

## 폴더 구조

### 프로그램 파일 (업데이트 시 교체)
```
C:\Program Files\AppLauncher\
  └─ AppLauncher.exe
```

### 데이터 파일 (업데이트해도 보존)
```
C:\ProgramData\AppLauncher\
  ├─ Data\                       (설정 파일)
  │   ├─ launcher_config.json
  │   ├─ launcher_version.txt
  │   └─ labview_version.txt
  ├─ Downloads\                  (zip/exe 다운로드)
  │   └─ [다운로드 파일들]
  ├─ Logs\                       (설치 로그)
  │   └─ install_log_*.txt
  └─ Backup\                     (setting.ini 백업)
      └─ setting_*.ini
```

## 빌드 명령어

### 배포용
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=false
```

### 테스트용
```bash
dotnet watch run
```

## PowerShell 명령어 (참고)

### 강제종료 포함 설치
```powershell
powershell -Command "Start-Process '.\setup.exe' -ArgumentList '/q','/AcceptLicenses','yes','/log','C:\install_log.txt' -Verb RunAs; Start-Sleep -Seconds 1800; Get-Process 'setup' -ErrorAction SilentlyContinue | Stop-Process -Force"
```

```powershell
powershell -Command "Start-Process '.\setup.exe' -ArgumentList '/q','/AcceptLicenses','yes','/log','C:\install_log_TS.txt' -Verb RunAs; Start-Sleep -Seconds 1800; Get-Process 'setup' -ErrorAction SilentlyContinue | Stop-Process -Force"
```


