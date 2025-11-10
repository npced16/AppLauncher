using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using Application = System.Windows.Application;
using AppLauncher.Presentation.WPF;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using AppLauncher.Features.MqttControl;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.VersionManagement;
using Newtonsoft.Json;

namespace AppLauncher.Features.TrayApp
{
    public class TrayApplicationContext : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;
        private MqttControlWindow? _mqttControlWindow;
        private MqttService? _mqttService;
        private LauncherConfig? _config;

        public TrayApplicationContext()
        {
            InitializeTrayIcon();
            StartServices();
        }

        private void InitializeTrayIcon()
        {
            // 아이콘 파일 로드
            Icon? appIcon = null;
            string iconPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "app_icon.ico"
            );

            if (System.IO.File.Exists(iconPath))
            {
                try
                {
                    appIcon = new Icon(iconPath);
                }
                catch
                {
                    appIcon = SystemIcons.Application;
                }
            }
            else
            {
                appIcon = SystemIcons.Application;
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Visible = true,
                Text = "App Launcher"
            };

            // 컨텍스트 메뉴 생성
            var contextMenu = new ContextMenuStrip();

            var showMenuItem = new ToolStripMenuItem("상태 보기");
            showMenuItem.Click += ShowWindow;
            contextMenu.Items.Add(showMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var mqttControlMenuItem = new ToolStripMenuItem("MQTT 제어 센터");
            mqttControlMenuItem.Click += ShowMqttControlWindow;
            contextMenu.Items.Add(mqttControlMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());


            var exitMenuItem = new ToolStripMenuItem("종료");
            exitMenuItem.Click += Exit;
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += ShowWindow;
        }

        private async void StartServices()
        {
            try
            {
                _notifyIcon!.Text = "App Launcher - 초기화 중...";

                // 설정 로드
                _config = ConfigManager.LoadConfig();

                // 기존 버전 체크 및 실행 (선택사항)
                if (!string.IsNullOrWhiteSpace(_config.VersionCheckUrl))
                {
                    _notifyIcon.Text = "App Launcher - 버전 확인 중...";
                    var launcher = new ApplicationLauncher(null);
                    await launcher.CheckAndLaunchInBackgroundAsync(_config, UpdateTrayStatus);
                }

                // MQTT 서비스 시작
                if (_config.MqttSettings != null)
                {
                    _notifyIcon.Text = "App Launcher - MQTT 연결 중...";
                    await StartMqttServiceAsync();
                }

                _notifyIcon.Text = "App Launcher - 대기 중";
            }
            catch (Exception ex)
            {
                _notifyIcon!.Text = $"App Launcher - 오류: {ex.Message}";
                _notifyIcon.ShowBalloonTip(3000, "오류", ex.Message, ToolTipIcon.Error);
            }
        }

        private async System.Threading.Tasks.Task StartMqttServiceAsync()
        {
            try
            {
                if (_config?.MqttSettings == null)
                {
                    return;
                }

                _mqttService = new MqttService(_config.MqttSettings);

                // 이벤트 핸들러 등록
                _mqttService.MessageReceived += OnMqttMessageReceived;
                _mqttService.ConnectionStateChanged += OnMqttConnectionStateChanged;
                _mqttService.LogMessage += OnMqttLogMessage;

                // MQTT 브로커 연결
                await _mqttService.ConnectAsync();

                UpdateTrayStatus("MQTT 연결됨");

                // 연결 성공 시 자동으로 현재 버전 정보 보고
                await SendVersionInfoAsync();
            }
            catch (Exception ex)
            {
                string errorDetail = ex.Message;
                if (ex.InnerException != null)
                {
                    errorDetail += $"\n상세: {ex.InnerException.Message}";
                }
                UpdateTrayStatus($"MQTT 오류");
            }
        }

        private void OnMqttMessageReceived(MqttMessage message)
        {
            try
            {
                UpdateTrayStatus($"메시지 수신: {message.Topic}");

                // JSON 메시지 파싱
                var command = JsonConvert.DeserializeObject<LaunchCommand>(message.Payload);

                if (command == null)
                {
                    return;
                }

                // 명령 처리
                switch (command.Command.ToLower())
                {
                    case "launch":
                    case "start":
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
                        UpdateTrayStatus($"알 수 없는 명령: {command.Command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                UpdateTrayStatus($"메시지 처리 오류: {ex.Message}");
            }
        }

        private void OnMqttConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                UpdateTrayStatus("MQTT 연결됨");
            }
            else
            {
                UpdateTrayStatus("MQTT 연결 끊김");
            }
        }

        private void OnMqttLogMessage(string message)
        {
            // 로그 메시지를 트레이 상태로 업데이트
            UpdateTrayStatus(message);
            System.Diagnostics.Debug.WriteLine($"[MQTT] {message}");
        }

        public string GetMqttStatus()
        {
            if (_mqttService == null)
            {
                return "MQTT 미설정";
            }

            if (_mqttService.IsConnected)
            {
                return $"연결됨: {_config?.MqttSettings?.Broker}:{_config?.MqttSettings?.Port}";
            }
            else
            {
                string status = "연결 끊김";
                if (!string.IsNullOrEmpty(_mqttService.LastError))
                {
                    status += $"\n오류: {_mqttService.LastError}";
                }
                return status;
            }
        }

        private async void LaunchApplication(LaunchCommand command)
        {
            try
            {
                string executable;

                // downloadUrl이 있으면 다운로드 후 실행
                if (!string.IsNullOrEmpty(command.DownloadUrl))
                {
                    UpdateTrayStatus("다운로드 중...");
                    executable = await DownloadAndExecuteAsync(command.DownloadUrl, command);
                    return;
                }

                // 로컬 실행 파일 사용
                executable = command.Executable ?? _config?.TargetExecutable ?? "";

                if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
                {
                    UpdateTrayStatus($"실행 파일을 찾을 수 없습니다: {executable}");
                    return;
                }

                ExecuteProgram(executable, command);
            }
            catch (Exception ex)
            {
                UpdateTrayStatus($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
            }
        }

        private async Task<string> DownloadAndExecuteAsync(string downloadUrl, LaunchCommand command)
        {
            try
            {
                UpdateTrayStatus("파일 다운로드 중...");
                _notifyIcon?.ShowBalloonTip(2000, "다운로드", "실행 파일 다운로드 중...", ToolTipIcon.Info);

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
                    UpdateTrayStatus("다운로드 실패");
                    SendStatusResponse("error", "Download failed");
                    return "";
                }

                UpdateTrayStatus($"다운로드 완료: {Path.GetFileName(downloadPath)}");

                // 다운로드한 파일 실행
                ExecuteProgram(downloadPath, command);

                return downloadPath;
            }
            catch (Exception ex)
            {
                UpdateTrayStatus($"다운로드 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                return "";
            }
        }

        private void ExecuteProgram(string executable, LaunchCommand command)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                };

                // 작업 디렉토리 설정
                string workingDir = command.WorkingDirectory ?? _config?.WorkingDirectory ?? "";
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

                UpdateTrayStatus($"프로그램 실행: {Path.GetFileName(executable)}");
                _notifyIcon?.ShowBalloonTip(2000, "프로그램 실행",
                    $"{Path.GetFileName(executable)} 실행됨", ToolTipIcon.Info);

                // 상태 응답 전송
                SendStatusResponse("launched", executable);
            }
            catch (Exception ex)
            {
                UpdateTrayStatus($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
            }
        }

        private async void SendStatus()
        {
            try
            {
                if (_mqttService == null || _config?.MqttSettings == null)
                {
                    return;
                }

                // 런처 버전 가져오기
                string launcherVersion = GetLauncherVersion();

                // 대상 앱 버전 가져오기
                string targetAppVersion = GetTargetAppVersion();

                var status = new
                {
                    status = "running",
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    mqttConnected = _mqttService.IsConnected,
                    clientId = _config.MqttSettings.ClientId,
                    hostname = Environment.MachineName,
                    launcherVersion = launcherVersion,
                    targetAppVersion = targetAppVersion
                };

                string responseTopic = $"{_config.MqttSettings.Topic}/status";
                await _mqttService.PublishJsonAsync(responseTopic, status);

                UpdateTrayStatus($"상태 전송 완료 (런처: {launcherVersion}, 앱: {targetAppVersion})");
            }
            catch (Exception ex)
            {
                UpdateTrayStatus($"상태 전송 오류: {ex.Message}");
            }
        }

        private async void SendStatusResponse(string status, string message)
        {
            try
            {
                if (_mqttService == null || _config?.MqttSettings == null)
                {
                    return;
                }

                var response = new
                {
                    status = status,
                    message = message,
                    timestamp = DateTime.Now
                };

                string responseTopic = $"{_config.MqttSettings.Topic}/response";
                await _mqttService.PublishJsonAsync(responseTopic, response);
            }
            catch
            {
                // 응답 전송 실패는 무시
            }
        }

        private void UpdateTrayStatus(string message)
        {
            if (_notifyIcon != null)
            {
                // NotifyIcon.Text는 최대 63자까지만 지원
                var truncatedMessage = message.Length > 63 ? message.Substring(0, 60) + "..." : message;
                _notifyIcon.Text = $"App Launcher - {truncatedMessage}";
            }
        }

        private void ShowWindow(object? sender, EventArgs e)
        {
            if (_mainWindow == null || !_mainWindow.IsVisible)
            {
                _mainWindow = new MainWindow();
                _mainWindow.Closed += (s, args) => _mainWindow = null;
                _mainWindow.Show();
            }
            else
            {
                _mainWindow.Activate();
            }
        }

        private void ShowMqttControlWindow(object? sender, EventArgs e)
        {
            if (_mqttControlWindow == null || !_mqttControlWindow.IsVisible)
            {
                // 기존 MqttService 인스턴스를 전달
                _mqttControlWindow = new MqttControlWindow(_mqttService);
                _mqttControlWindow.Closed += (s, args) =>
                {
                    _mqttControlWindow = null;
                    // 설정이 변경되었을 수 있으므로 다시 로드
                    _config = ConfigManager.LoadConfig();
                };
                _mqttControlWindow.Show();
            }
            else
            {
                _mqttControlWindow.Activate();
            }
        }

        private async void UpdateLauncherViaMqtt(LaunchCommand command)
        {
            try
            {
                UpdateTrayStatus("MQTT 명령: 런처 업데이트 시작");
                _notifyIcon?.ShowBalloonTip(3000, "런처 업데이트", "MQTT 명령을 받아 런처 업데이트를 시작합니다...", ToolTipIcon.Info);

                // MQTT 명령에서 다운로드 URL과 버전 정보 확인
                if (string.IsNullOrWhiteSpace(command.DownloadUrl))
                {
                    _notifyIcon?.ShowBalloonTip(3000, "업데이트 실패", "다운로드 URL이 제공되지 않았습니다.", ToolTipIcon.Warning);
                    SendStatusResponse("error", "Download URL not provided in MQTT command");
                    return;
                }

                if (string.IsNullOrWhiteSpace(command.Version))
                {
                    _notifyIcon?.ShowBalloonTip(3000, "업데이트 실패", "버전 정보가 제공되지 않았습니다.", ToolTipIcon.Warning);
                    SendStatusResponse("error", "Version not provided in MQTT command");
                    return;
                }

                string currentExePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                string versionFile = _config?.LauncherVersionFile ??
                    Path.Combine(Path.GetDirectoryName(ConfigManager.GetConfigFilePath()) ?? "", "launcher_version.txt");

                _notifyIcon?.ShowBalloonTip(3000, "업데이트 발견", $"새 버전 {command.Version} 다운로드 중...", ToolTipIcon.Info);
                await UpdateLauncherAsync(command.DownloadUrl, command.Version, currentExePath, versionFile);
            }
            catch (Exception ex)
            {
                _notifyIcon?.ShowBalloonTip(5000, "업데이트 오류", $"런처 업데이트 실패: {ex.Message}", ToolTipIcon.Error);
                SendStatusResponse("error", ex.Message);
            }
        }


        private async Task UpdateLauncherAsync(string downloadUrl, string newVersion, string currentExePath, string versionFile)
        {
            try
            {
                UpdateTrayStatus("런처 업데이트 다운로드 중...");

                var updater = new BackgroundUpdater(
                    downloadUrl,
                    versionFile,
                    (status) => UpdateTrayStatus(status)
                );

                // 업데이트 파일 다운로드 및 교체
                string? updatedPath = await updater.DownloadAndReplaceExeAsync(newVersion, currentExePath);

                if (updatedPath != null)
                {
                    _notifyIcon?.ShowBalloonTip(3000, "업데이트 완료", "런처가 업데이트되었습니다. 재시작합니다...", ToolTipIcon.Info);

                    await Task.Delay(2000);

                    // 새 버전 실행
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = updatedPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);

                    // 현재 앱 종료
                    Application.Current.Shutdown();
                }
                else
                {
                    _notifyIcon?.ShowBalloonTip(5000, "업데이트 실패", "런처 업데이트에 실패했습니다.", ToolTipIcon.Error);
                    UpdateTrayStatus("업데이트 실패");
                }
            }
            catch (Exception ex)
            {
                _notifyIcon?.ShowBalloonTip(5000, "업데이트 오류", $"업데이트 중 오류 발생: {ex.Message}", ToolTipIcon.Error);
                UpdateTrayStatus($"업데이트 오류: {ex.Message}");
            }
        }



        private async void Exit(object? sender, EventArgs e)
        {
            try
            {
                UpdateTrayStatus("종료 중...");

                // MQTT 연결 해제
                if (_mqttService != null && _mqttService.IsConnected)
                {
                    UpdateTrayStatus("MQTT 연결 해제 중...");
                    await _mqttService.DisconnectAsync();
                    await Task.Delay(500); // MQTT 연결 해제 대기
                }

                // 리소스 정리
                Dispose();

                // 잠시 대기 후 종료
                await Task.Delay(300);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"종료 중 오류: {ex.Message}");
                Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// MQTT를 통해 현재 버전 정보를 중앙 서버에 보고
        /// </summary>
        private async Task SendVersionInfoAsync()
        {
            if (_mqttService == null || !_mqttService.IsConnected || _config == null)
            {
                return;
            }

            try
            {
                // 런처 버전 가져오기
                string launcherVersion = GetLauncherVersion();

                // 대상 앱 버전 가져오기
                string targetAppVersion = GetTargetAppVersion();

                // 호스트 정보
                string hostName = Environment.MachineName;
                string userName = Environment.UserName;

                // 버전 정보 객체 생성
                var versionInfo = new
                {
                    type = "version_report",
                    clientId = _config.MqttSettings?.ClientId ?? "Unknown",
                    hostname = hostName,
                    username = userName,
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    launcher = new
                    {
                        version = launcherVersion,
                        executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe")
                    },
                    targetApp = new
                    {
                        version = targetAppVersion,
                        executablePath = _config.TargetExecutable ?? "Not configured"
                    }
                };

                // 상태 토픽으로 버전 정보 발행
                string statusTopic = _config.MqttSettings?.Topic?.Replace("/commands", "/status") ?? "applauncher/status";
                await _mqttService.PublishJsonAsync(statusTopic, versionInfo);

                UpdateTrayStatus($"버전 정보 보고 완료 (런처: {launcherVersion}, 앱: {targetAppVersion})");
            }
            catch (Exception ex)
            {
                UpdateTrayStatus($"버전 정보 보고 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 런처의 현재 버전을 가져옵니다
        /// </summary>
        private string GetLauncherVersion()
        {
            try
            {
                if (_config != null)
                {
                    string versionFile = _config.LauncherVersionFile ??
                        Path.Combine(Path.GetDirectoryName(ConfigManager.GetConfigFilePath()) ?? "", "launcher_version.txt");

                    if (File.Exists(versionFile))
                    {
                        return File.ReadAllText(versionFile).Trim();
                    }
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
                if (_config != null && !string.IsNullOrWhiteSpace(_config.LocalVersionFile))
                {
                    if (File.Exists(_config.LocalVersionFile))
                    {
                        return File.ReadAllText(_config.LocalVersionFile).Trim();
                    }
                }

                return "Not installed";
            }
            catch
            {
                return "Unknown";
            }
        }

        public void Dispose()
        {
            // MQTT 서비스 정리
            if (_mqttService != null)
            {
                _mqttService.DisconnectAsync().Wait();
                _mqttService.Dispose();
                _mqttService = null;
            }

            // 트레이 아이콘 정리
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
