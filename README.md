//강제종료추가
powershell -Command "Start-Process '.\setup.exe' -ArgumentList '/q','/AcceptLicenses','yes','/log','C:\install_log.txt' -Verb RunAs; Start-Sleep -Seconds 1800; Get-Process 'setup' -ErrorAction SilentlyContinue | Stop-Process -Force"



powershell -Command "Start-Process '.\setup.exe' -ArgumentList '/q','/AcceptLicenses','yes','/log','C:\install_log_TS.txt' -Verb RunAs; Start-Sleep -Seconds 1800; Get-Process 'setup' -ErrorAction SilentlyContinue | Stop-Process -Force"


C:\Users\UK\AppData\Local\Temp\AppLauncherDownloads 설치파일위치


배포용 명령어
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=false

테스트용 명령어 
dotnet watch run