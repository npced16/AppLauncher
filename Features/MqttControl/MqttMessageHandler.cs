using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.VersionManagement;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using Newtonsoft.Json;

namespace AppLauncher.Features.MqttControl
{
    /// <summary>
    /// MQTT 메시지 처리 및 상태 보고를 담당하는 클래스
    /// </summary>
    public class MqttMessageHandler
    {
        private readonly MqttService _mqttService;
        private readonly LauncherConfig _config;
        private readonly Action<string> _statusCallback;
        private readonly Action<string>? _installStatusCallback;

        public MqttMessageHandler(
            MqttService mqttService,
            LauncherConfig config,
            Action<string> statusCallback,
            Action<string>? installStatusCallback = null)
        {
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _statusCallback = statusCallback ?? throw new ArgumentNullException(nameof(statusCallback));
            _installStatusCallback = installStatusCallback;
        }

        /// <summary>
        /// MQTT 메시지 수신 처리
        /// </summary>
        public void HandleMessage(MqttMessage message)
        {
            try
            {
                _statusCallback($"메시지 수신: {message.Topic}");

                // JSON 메시지 파싱
                var command = JsonConvert.DeserializeObject<LaunchCommand>(message.Payload);

                if (command == null)
                {
                    return;
                }

                // 명령 처리
                switch (command.Command.ToUpper())
                {
                    case "LABVIEW_UPDATE":
                    case "LABVIEWUPDATE":
                        LaunchApplication(command);
                        break;

                    case "LAUNCHER_UPDATE":
                    case "LAUNCHERUPDATE":
                        UpdateLauncherViaMqtt(command);
                        break;

                    case "status":
                        SendStatus();
                        break;

                    default:
                        _statusCallback($"알 수 없는 명령: {command.Command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _statusCallback($"메시지 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// MQTT 명령으로 애플리케이션 실행
        /// </summary>
        private async void LaunchApplication(LaunchCommand command)
        {
            try
            {
                string executable;

                // url이 있으면 다운로드 후 실행
                if (!string.IsNullOrEmpty(command.URL))
                {
                    _statusCallback("다운로드 중...");
                    executable = await DownloadAndExecuteAsync(command.URL, command);
                    return;
                }
                // 다운로드 에러 mqtt 응답은 DownloadAndExecuteAsync에서 처리 
            }
            catch (Exception ex)
            {
                _statusCallback($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
            }
        }

        /// <summary>
        /// 파일 다운로드 및 실행
        /// </summary>
        private async Task<string> DownloadAndExecuteAsync(string downloadUrl, LaunchCommand command)
        {
            try
            {
                _statusCallback("파일 다운로드 중...");
                _installStatusCallback?.Invoke("다운로드 중");

                // 임시 디렉토리 생성
                string tempDir = Path.Combine(Path.GetTempPath(), "AppLauncherDownloads");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                // 파일명 추출
                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    fileName = $"download_{DateTime.Now:yyyyMMddHHmmss}.exe";

                string downloadPath = Path.Combine(tempDir, fileName);

                // HttpClient로 다운로드
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10);
                    var response = await httpClient.GetAsync(downloadUrl);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                if (!File.Exists(downloadPath))
                {
                    _statusCallback("다운로드 실패");
                    SendStatusResponse("error", "Download failed");
                    _installStatusCallback?.Invoke("대기 중");
                    return "";
                }

                _statusCallback($"다운로드 완료: {Path.GetFileName(downloadPath)}");

                // 다운로드한 파일 실행
                ExecuteProgram(downloadPath, command);

                return downloadPath;
            }
            catch (Exception ex)
            {
                _statusCallback($"다운로드 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
                return "";
            }
        }

        /// <summary>
        /// 프로그램 실행
        /// </summary>
        private void ExecuteProgram(string executable, LaunchCommand command)
        {
            try
            {
                string fileName = Path.GetFileName(executable).ToLower();

                // setup.exe는 PowerShell로 자동 설치 실행
                if (fileName == "setup.exe")
                {
                    ExecuteSetupWithPowerShell(executable, command);
                    return;
                }

                // 일반 프로그램 실행
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                };

                // 작업 디렉토리 자동 설정: config > executable이 있는 디렉토리
                string workingDir = _config.WorkingDirectory ?? "";

                // workingDir이 없으면 실행 파일이 있는 디렉토리를 자동으로 사용
                if (string.IsNullOrEmpty(workingDir))
                {
                    workingDir = Path.GetDirectoryName(executable) ?? "";
                }

                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                Process.Start(startInfo);

                _statusCallback($"프로그램 실행: {Path.GetFileName(executable)}");

                // 상태 응답 전송
                SendStatusResponse("launched", executable);

                // 일반 프로그램 실행 후 대기 중으로 복귀
                _installStatusCallback?.Invoke("대기 중");
            }
            catch (Exception ex)
            {
                _statusCallback($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
            }
        }

        /// <summary>
        /// setup.exe를 PowerShell로 자동 설치 실행
        /// </summary>
        private void ExecuteSetupWithPowerShell(string setupExePath, LaunchCommand command)
        {
            try
            {
                _statusCallback("LabView 설치 시작...");
                _installStatusCallback?.Invoke("설치 중");

                // MQTT: 설치 시작 로그 전송
                SendStatusResponse("install_start", $"Setup.exe 실행 시작: {setupExePath}");

                // 로그 파일 경로에 타임스탬프 추가
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logPath = $"C:\\install_log_{timestamp}.txt";

                // PowerShell 명령을 통해 실행 (관리자 권한, 30분 후 강제 종료)
                // 전체 경로를 직접 사용하여 정확한 setup.exe 실행
                string psCommand = $"Start-Process '{setupExePath}' -ArgumentList '/q','/AcceptLicenses','yes','/log','{logPath}' -Verb RunAs; Start-Sleep -Seconds 1800; Get-Process 'setup' -ErrorAction SilentlyContinue | Stop-Process -Force";

                _statusCallback($"PowerShell 명령 실행: {psCommand}");
                _statusCallback($"로그 파일: {logPath}");
                SendStatusResponse("install_command", psCommand);
                SendStatusResponse("install_log_path", logPath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{psCommand}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true  // 백그라운드에서 실행
                };

                Process.Start(startInfo);

                _statusCallback("PowerShell 프로세스 시작됨");
                SendStatusResponse("install_process_started", "PowerShell 프로세스가 시작되었습니다");

                // 업데이트 완료 시 버전 정보 저장
                if (!string.IsNullOrEmpty(command.Version) && !string.IsNullOrEmpty(_config.LocalVersionFile))
                {
                    try
                    {
                        string versionFileDir = Path.GetDirectoryName(_config.LocalVersionFile);
                        if (!string.IsNullOrEmpty(versionFileDir) && !Directory.Exists(versionFileDir))
                        {
                            Directory.CreateDirectory(versionFileDir);
                        }
                        File.WriteAllText(_config.LocalVersionFile, command.Version);
                        _statusCallback($"버전 {command.Version} 저장 완료");
                        SendStatusResponse("version_saved", $"버전 파일 저장: {command.Version}");
                    }
                    catch (Exception versionEx)
                    {
                        Debug.WriteLine($"버전 파일 저장 실패: {versionEx.Message}");
                        SendStatusResponse("version_save_error", versionEx.Message);
                    }
                }

                _statusCallback("LabView 자동 설치 실행됨 (약 30분 소요)");

                // 상태 응답 전송
                SendStatusResponse("installing", $"{setupExePath} - 설치 진행 중 (약 30분 소요)");
            }
            catch (Exception ex)
            {
                _statusCallback($"설치 실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
            }
        }

        /// <summary>
        /// 상태 정보 전송
        /// </summary>
        private async void SendStatus()
        {
            try
            {
                if (_mqttService == null || !_mqttService.IsConnected)
                {
                    return;
                }

                // 런처 버전 가져오기
                string launcherVersion = GetLauncherVersion();

                // 대상 앱 버전 가져오기
                string targetAppVersion = GetTargetAppVersion();

                // 하드웨어 UUID 가져오기
                string hardwareUuid = HardwareInfo.GetHardwareUuid();

                var status = new
                {
                    status = "running",
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    mqttConnected = _mqttService.IsConnected,
                    location = _config.MqttSettings?.Location,
                    hardwareUUID = hardwareUuid,
                    launcherVersion = launcherVersion,
                    targetAppVersion = targetAppVersion
                };

                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, status);

                _statusCallback($"상태 전송 완료 (런처: {launcherVersion}, 앱: {targetAppVersion})");
            }
            catch (Exception ex)
            {
                _statusCallback($"상태 전송 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 상태 응답 전송
        /// </summary>
        private async void SendStatusResponse(string status, string message)
        {
            try
            {
                if (_mqttService == null || !_mqttService.IsConnected || _config.MqttSettings == null)
                {
                    return;
                }

                var response = new
                {
                    status = status,
                    message = message,
                    timestamp = DateTime.Now
                };

                await _mqttService.PublishJsonAsync(_mqttService.ResponseTopic, response);
            }
            catch
            {
                // 응답 전송 실패는 무시
            }
        }

        /// <summary>
        /// 버전 정보 전송
        /// </summary>
        public async Task SendVersionInfoAsync()
        {
            if (_mqttService == null || !_mqttService.IsConnected)
            {
                return;
            }

            try
            {
                // 런처 버전 가져오기
                string launcherVersion = GetLauncherVersion();

                // 대상 앱 버전 가져오기
                string targetAppVersion = GetTargetAppVersion();

                // 하드웨어 UUID 가져오기
                string hardwareUuid = HardwareInfo.GetHardwareUuid();

                // 버전 정보 객체 생성
                var versionInfo = new
                {
                    location = _config.MqttSettings?.Location,
                    hardwareUuid = hardwareUuid,
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    launcher = new
                    {
                        version = launcherVersion
                    },
                    targetApp = new
                    {
                        version = targetAppVersion
                    }
                };

                // 상태 토픽으로 버전 정보 발행
                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, versionInfo);

                _statusCallback($"버전 정보 보고 완료 (런처: {launcherVersion}, 앱: {targetAppVersion})");
            }
            catch (Exception ex)
            {
                _statusCallback($"버전 정보 보고 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// MQTT 명령을 통한 런처 업데이트
        /// </summary>
        private async void UpdateLauncherViaMqtt(LaunchCommand command)
        {
            try
            {
                _statusCallback("MQTT 명령: 런처 업데이트 확인");
                _installStatusCallback?.Invoke("버전 확인 중");

                // MQTT 명령에서 다운로드 URL과 버전 정보 확인
                if (string.IsNullOrWhiteSpace(command.URL))
                {
                    _statusCallback("업데이트 실패: 다운로드 URL이 제공되지 않았습니다.");
                    SendStatusResponse("error", "Download URL not provided in MQTT command");
                    _installStatusCallback?.Invoke("대기 중");
                    return;
                }

                if (string.IsNullOrWhiteSpace(command.Version))
                {
                    _statusCallback("업데이트 실패: 버전 정보가 제공되지 않았습니다.");
                    SendStatusResponse("error", "Version not provided in MQTT command");
                    _installStatusCallback?.Invoke("대기 중");
                    return;
                }

                // 현재 런처 버전 가져오기 (코드에 내장된 버전)
                string currentVersion = VersionInfo.LAUNCHER_VERSION;
                string incomingVersion = command.Version;

                _statusCallback($"버전 비교: 현재 {currentVersion} vs 수신 {incomingVersion}");
                SendStatusResponse("version_check", $"현재: {currentVersion}, 수신: {incomingVersion}");

                // 버전 비교
                bool needsUpdate = CompareVersions(currentVersion, incomingVersion);

                if (!needsUpdate)
                {
                    _statusCallback($"업데이트 불필요: 현재 버전({currentVersion})이 최신이거나 동일합니다.");
                    SendStatusResponse("update_skip", $"현재 버전 {currentVersion}이 최신입니다");
                    _installStatusCallback?.Invoke("대기 중");
                    return;
                }

                _statusCallback($"새 버전 감지: {currentVersion} -> {incomingVersion}");
                SendStatusResponse("update_required", $"버전 업데이트 필요: {currentVersion} -> {incomingVersion}");
                _installStatusCallback?.Invoke("런처 업데이트 중");

                // self-contained 빌드 지원
                string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string versionFile = _config.LauncherVersionFile ??
                    Path.Combine(Path.GetDirectoryName(ConfigManager.GetConfigFilePath()) ?? "", "launcher_version.txt");

                _statusCallback($"새 버전 {command.Version} 다운로드 중...");
                await UpdateLauncherAsync(command.URL, command.Version, currentExePath, versionFile);
            }
            catch (Exception ex)
            {
                _statusCallback($"런처 업데이트 실패: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
            }
        }

        /// <summary>
        /// 버전 비교: 수신한 버전이 현재 버전보다 높으면 true 반환
        /// </summary>
        private bool CompareVersions(string currentVersion, string incomingVersion)
        {
            try
            {
                // Version 클래스를 사용한 비교
                if (Version.TryParse(currentVersion, out Version? current) &&
                    Version.TryParse(incomingVersion, out Version? incoming))
                {
                    return incoming > current;
                }

                // 파싱 실패시 문자열 비교
                return string.Compare(incomingVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
            }
            catch
            {
                // 비교 실패시 업데이트 필요한 것으로 간주
                return true;
            }
        }

        /// <summary>
        /// 런처 업데이트 실행
        /// </summary>
        private async Task UpdateLauncherAsync(string downloadUrl, string newVersion, string currentExePath, string versionFile)
        {
            try
            {
                _statusCallback("런처 업데이트 다운로드 중...");

                var updater = new BackgroundUpdater(
                    downloadUrl,
                    versionFile,
                    (status) => _statusCallback(status)
                );

                // 업데이트 파일 다운로드 및 교체
                string? updatedPath = await updater.DownloadAndReplaceExeAsync(newVersion, currentExePath);

                if (updatedPath != null)
                {
                    _statusCallback("업데이트 완료. 재시작합니다...");

                    await Task.Delay(2000);

                    // 새 버전 실행
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = updatedPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);

                    // 현재 앱 종료
                    System.Windows.Forms.Application.Exit();
                }
                else
                {
                    _statusCallback("업데이트 실패");
                }
            }
            catch (Exception ex)
            {
                _statusCallback($"업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 런처의 현재 버전을 가져옵니다 (코드 내장 버전만 사용)
        /// </summary>
        private string GetLauncherVersion()
        {
            return VersionInfo.LAUNCHER_VERSION;
        }

        /// <summary>
        /// 대상 앱의 현재 버전을 가져옵니다
        /// </summary>
        private string GetTargetAppVersion()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_config.LocalVersionFile))
                {
                    if (File.Exists(_config.LocalVersionFile))
                    {
                        string version = File.ReadAllText(_config.LocalVersionFile).Trim();
                        return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
                    }
                }

                return "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

    }
}
