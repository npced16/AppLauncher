using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.MqttControl;
using AppLauncher.Presentation.WinForms;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Features.TrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon? _notifyIcon;
        private MainForm? _mainForm;
        private MqttControlForm? _mqttControlForm;
        private LauncherSettingsForm? _launcherSettingsForm;
        private MqttService? _mqttService;
        private MqttMessageHandler? _mqttMessageHandler;
        private LauncherConfig? _config;
        private ApplicationLauncher? _applicationLauncher;

        private static void DebugLog(string message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif
        }

        public TrayApplicationContext()
        {
            DebugLog("[TrayApplicationContext] InitializeTrayIcon 호출");
            InitializeTrayIcon();
            DebugLog("[TrayApplicationContext] StartServices 호출");
            StartServices();
            DebugLog("[TrayApplicationContext] 생성자 완료");
        }


        private void InitializeTrayIcon()
        {
            // 아이콘 파일 로드 - 실행 파일에서 아이콘 추출 (가장 안전)
            Icon? appIcon = null;

            try
            {
                // 먼저 실행 파일의 아이콘 사용 시도
                string exePath =
                    Environment.ProcessPath
                    ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(exePath))
                {
                    appIcon = Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch
            {
                // 실행 파일 아이콘 추출 실패시 파일에서 로드 시도
                try
                {
                    string iconPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "app_icon.ico"
                    );
                    if (File.Exists(iconPath))
                    {
                        appIcon = new Icon(iconPath);
                    }
                }
                catch
                {
                    // 최종 대안으로 시스템 아이콘 사용
                    appIcon = SystemIcons.Application;
                }
            }

            // 아이콘이 여전히 null이면 시스템 아이콘 사용
            if (appIcon == null)
            {
                appIcon = SystemIcons.Application;
            }

            _notifyIcon = new NotifyIcon
            {
                Icon = appIcon,
                Visible = true,
                Text = "App Launcher",
            };

            // 컨텍스트 메뉴 생성
            var contextMenu = new ContextMenuStrip();

            var mainFormMenuItem = new ToolStripMenuItem("앱 실행");
            mainFormMenuItem.Click += ShowMainForm;
            contextMenu.Items.Add(mainFormMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var launcherSettingsMenuItem = new ToolStripMenuItem("설정");
            launcherSettingsMenuItem.Click += ShowLauncherSettingsWindow;
            contextMenu.Items.Add(launcherSettingsMenuItem);

            var mqttControlMenuItem = new ToolStripMenuItem("MQTT 제어 센터");
            mqttControlMenuItem.Click += ShowMqttControlWindow;
            contextMenu.Items.Add(mqttControlMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("종료");
            exitMenuItem.Click += Exit;
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private async void StartServices()
        {
            try
            {

                // 설정 로드
                _config = ConfigManager.LoadConfig();

                // MQTT 서비스 시작
                if (_config.MqttSettings != null)
                {
                    await StartMqttServiceAsync();
                }

            }
            catch (Exception ex)
            {
                _notifyIcon.ShowBalloonTip(3000, "오류", ex.Message, ToolTipIcon.Error);
            }
        }

        private async Task StartMqttServiceAsync()
        {
            try
            {
                if (_config?.MqttSettings == null)
                {
                    return;
                }

                // 하드웨어 UUID를 ClientId로 사용
                string clientId = HardwareInfo.GetHardwareUuid();
                _mqttService = new MqttService(_config.MqttSettings, clientId);

                // MQTT 메시지 핸들러 생성
                _mqttMessageHandler = new MqttMessageHandler(
                    _mqttService,
                    _config,
                    _applicationLauncher,
                    ShowBalloonTip
                );

                // 이벤트 핸들러 등록
                _mqttService.MessageReceived += (msg) => _mqttMessageHandler?.HandleMessage(msg);
                _mqttService.ConnectionStateChanged += OnMqttConnectionStateChanged;
                _mqttService.LogMessage += OnMqttLogMessage;

                // MQTT 브로커 연결
                await _mqttService.ConnectAsync();
            }
            catch (Exception ex)
            {
                string errorDetail = ex.Message;
                if (ex.InnerException != null)
                {
                    errorDetail += $"\n상세: {ex.InnerException.Message}";
                }
            }
        }

        private void OnMqttConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                // 연결 성공 시 초기 상태 전송
                _mqttMessageHandler?.SendStatus("connected");
            }
        }

        private void OnMqttLogMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[MQTT] {message}");
        }

        private void ShowBalloonTip(string title, string message, int timeout)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(timeout, title, message, ToolTipIcon.Info);
            }
        }

        private void ShowMainForm(object? sender, EventArgs e)
        {
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm();
                _mainForm.FormClosed += (s, args) =>
                {
                    _mainForm = null;
                };
                _mainForm.Show();
            }
            else
            {
                _mainForm.Activate();
            }
        }

        private void ShowLauncherSettingsWindow(object? sender, EventArgs e)
        {
            if (_launcherSettingsForm == null || _launcherSettingsForm.IsDisposed)
            {
                _launcherSettingsForm = new LauncherSettingsForm();
                _launcherSettingsForm.FormClosed += (s, args) =>
                {
                    _launcherSettingsForm = null;
                    // 설정이 변경되었을 수 있으므로 다시 로드
                    _config = ConfigManager.LoadConfig();
                };
                _launcherSettingsForm.Show();
            }
            else
            {
                _launcherSettingsForm.Activate();
            }
        }

        private void ShowMqttControlWindow(object? sender, EventArgs e)
        {
            if (_mqttControlForm == null || _mqttControlForm.IsDisposed)
            {
                // 기존 MqttService 인스턴스를 전달
                _mqttControlForm = new MqttControlForm(_mqttService);
                _mqttControlForm.FormClosed += (s, args) =>
                {
                    _mqttControlForm = null;
                    // 설정이 변경되었을 수 있으므로 다시 로드
                    _config = ConfigManager.LoadConfig();
                };
                _mqttControlForm.Show();
            }
            else
            {
                _mqttControlForm.Activate();
            }
        }

        private async void Exit(object? sender, EventArgs e)
        {
            try
            {
                // 모든 열려있는 폼 닫기
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    _mainForm.Close();
                }
                if (_mqttControlForm != null && !_mqttControlForm.IsDisposed)
                {
                    _mqttControlForm.Close();
                }
                if (_launcherSettingsForm != null && !_launcherSettingsForm.IsDisposed)
                {
                    _launcherSettingsForm.Close();
                }

                // MQTT 연결 해제
                if (_mqttService != null && _mqttService.IsConnected)
                {
                    await _mqttService.DisconnectAsync();
                    await Task.Delay(500); // MQTT 연결 해제 대기
                }

                // 리소스 정리
                Dispose();

                // 잠시 대기 후 프로세스 완전 종료
                await Task.Delay(300);

                // Application.Exit()는 메시지 루프만 종료하므로
                // Environment.Exit(0)를 사용하여 프로세스 전체를 종료
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"종료 중 오류: {ex.Message}");
                // 오류 발생 시에도 프로세스 강제 종료
                Environment.Exit(1);
            }
        }

        public new void Dispose()
        {
            // 관리 중인 프로세스 종료
            if (_applicationLauncher != null)
            {
                _applicationLauncher.Cleanup();
            }

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
