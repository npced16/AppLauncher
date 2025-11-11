using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using AppLauncher.Presentation.WinForms;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using AppLauncher.Features.MqttControl;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.VersionManagement;
using Newtonsoft.Json;

namespace AppLauncher.Features.TrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon? _notifyIcon;
        private MqttControlForm? _mqttControlForm;
        private LauncherSettingsForm? _launcherSettingsForm;
        private MqttService? _mqttService;
        private MqttMessageHandler? _mqttMessageHandler;
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

            var launcherSettingsMenuItem = new ToolStripMenuItem("런처 설정");
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
                _notifyIcon!.Text = "App Launcher - 초기화 중...";

                // 설정 로드
                _config = ConfigManager.LoadConfig();

                // 백그라운드에서 대상 프로그램 실행
                _notifyIcon.Text = "App Launcher - 프로그램 실행 중...";
                var launcher = new ApplicationLauncher();
                await launcher.CheckAndLaunchInBackgroundAsync(_config, UpdateTrayStatus);

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

                // 하드웨어 UUID를 ClientId로 사용
                string clientId = HardwareInfo.GetHardwareUuid();
                _mqttService = new MqttService(_config.MqttSettings, clientId);

                // MQTT 메시지 핸들러 생성
                _mqttMessageHandler = new MqttMessageHandler(
                    _mqttService,
                    _config,
                    UpdateTrayStatus,
                    ShowBalloonTip
                );

                // 이벤트 핸들러 등록
                _mqttService.MessageReceived += (msg) => _mqttMessageHandler?.HandleMessage(msg);
                _mqttService.ConnectionStateChanged += OnMqttConnectionStateChanged;
                _mqttService.LogMessage += OnMqttLogMessage;

                // MQTT 브로커 연결
                await _mqttService.ConnectAsync();

                UpdateTrayStatus("MQTT 연결됨");
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

        private void ShowBalloonTip(string title, string message, ToolTipIcon icon)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
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
                    UpdateTrayStatus("설정이 업데이트되었습니다");
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
                Application.Exit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"종료 중 오류: {ex.Message}");
                Application.Exit();
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
