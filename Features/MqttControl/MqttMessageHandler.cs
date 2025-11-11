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
        private readonly Action<string, string, ToolTipIcon> _balloonTipCallback;

        public MqttMessageHandler(
            MqttService mqttService,
            LauncherConfig config,
            Action<string> statusCallback,
            Action<string, string, ToolTipIcon> balloonTipCallback)
        {
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _statusCallback = statusCallback ?? throw new ArgumentNullException(nameof(statusCallback));
            _balloonTipCallback = balloonTipCallback ?? throw new ArgumentNullException(nameof(balloonTipCallback));
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
                switch (command.Command.ToLower())
                {
                    case "update_labview":
                    case "updatelabview":
                        LaunchApplication(command);
                        break;

                    case "update_launcher":
                    case "updatelauncher":
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

                // downloadUrl이 있으면 다운로드 후 실행
                if (!string.IsNullOrEmpty(command.DownloadUrl))
                {
                    _statusCallback("다운로드 중...");
                    executable = await DownloadAndExecuteAsync(command.DownloadUrl, command);
                    return;
                }

                // 로컬 실행 파일 사용
                executable = command.Executable ?? _config.TargetExecutable ?? "";

                if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
                {
                    _statusCallback($"실행 파일을 찾을 수 없습니다: {executable}");
                    return;
                }

                ExecuteProgram(executable, command);
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
                _balloonTipCallback("다운로드", "실행 파일 다운로드 중...", ToolTipIcon.Info);

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
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                };

                // 작업 디렉토리 자동 설정: command > config > executable이 있는 디렉토리
                string workingDir = command.WorkingDirectory ?? _config.WorkingDirectory ?? "";

                // workingDir이 없으면 실행 파일이 있는 디렉토리를 자동으로 사용
                if (string.IsNullOrEmpty(workingDir))
                {
                    workingDir = Path.GetDirectoryName(executable) ?? "";
                }

                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                // 인수 설정
                if (!string.IsNullOrEmpty(command.Arguments))
                {
                    startInfo.Arguments = command.Arguments;
                }

                Process.Start(startInfo);

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
                    }
                    catch (Exception versionEx)
                    {
                        Debug.WriteLine($"버전 파일 저장 실패: {versionEx.Message}");
                    }
                }

                _statusCallback($"프로그램 실행: {Path.GetFileName(executable)}");
                _balloonTipCallback("프로그램 실행", $"{Path.GetFileName(executable)} 실행됨", ToolTipIcon.Info);

                // 상태 응답 전송
                SendStatusResponse("launched", executable);
            }
            catch (Exception ex)
            {
                _statusCallback($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
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
                _statusCallback("MQTT 명령: 런처 업데이트 시작");
                _balloonTipCallback("런처 업데이트", "MQTT 명령을 받아 런처 업데이트를 시작합니다...", ToolTipIcon.Info);

                // MQTT 명령에서 다운로드 URL과 버전 정보 확인
                if (string.IsNullOrWhiteSpace(command.DownloadUrl))
                {
                    _balloonTipCallback("업데이트 실패", "다운로드 URL이 제공되지 않았습니다.", ToolTipIcon.Warning);
                    SendStatusResponse("error", "Download URL not provided in MQTT command");
                    return;
                }

                if (string.IsNullOrWhiteSpace(command.Version))
                {
                    _balloonTipCallback("업데이트 실패", "버전 정보가 제공되지 않았습니다.", ToolTipIcon.Warning);
                    SendStatusResponse("error", "Version not provided in MQTT command");
                    return;
                }

                // self-contained 빌드 지원
                string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string versionFile = _config.LauncherVersionFile ??
                    Path.Combine(Path.GetDirectoryName(ConfigManager.GetConfigFilePath()) ?? "", "launcher_version.txt");

                _balloonTipCallback("업데이트 발견", $"새 버전 {command.Version} 다운로드 중...", ToolTipIcon.Info);
                await UpdateLauncherAsync(command.DownloadUrl, command.Version, currentExePath, versionFile);
            }
            catch (Exception ex)
            {
                _balloonTipCallback("업데이트 오류", $"런처 업데이트 실패: {ex.Message}", ToolTipIcon.Error);
                SendStatusResponse("error", ex.Message);
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
                    _balloonTipCallback("업데이트 완료", "런처가 업데이트되었습니다. 재시작합니다...", ToolTipIcon.Info);

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
                    _balloonTipCallback("업데이트 실패", "런처 업데이트에 실패했습니다.", ToolTipIcon.Error);
                    _statusCallback("업데이트 실패");
                }
            }
            catch (Exception ex)
            {
                _balloonTipCallback("업데이트 오류", $"업데이트 중 오류 발생: {ex.Message}", ToolTipIcon.Error);
                _statusCallback($"업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 런처의 현재 버전을 가져옵니다
        /// </summary>
        private string GetLauncherVersion()
        {
            try
            {
                string versionFile = _config.LauncherVersionFile ??
                    Path.Combine(Path.GetDirectoryName(ConfigManager.GetConfigFilePath()) ?? "", "launcher_version.txt");

                if (File.Exists(versionFile))
                {
                    return File.ReadAllText(versionFile).Trim();
                }

                // 버전 파일이 없으면 어셈블리 버전 사용
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return version?.ToString() ?? "1.0.0";
            }
            catch
            {
                return "Unknown";
            }
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
