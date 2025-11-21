using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.MqttControl;
using AppLauncher.Presentation.WinForms;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using AppLauncher.Shared.Services;

namespace AppLauncher.Features.TrayApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon? _notifyIcon;
        private MainForm? _mainForm;
        private MqttControlForm? _mqttControlForm;
        private LauncherSettingsForm? _launcherSettingsForm;
        private MqttMessageHandler _mqttMessageHandler => ServiceContainer.MqttMessageHandler!;
        private LauncherConfig? _config;
        private System.Timers.Timer? _statusTimer;
        private System.Timers.Timer? _reconnectTimer;

        private static void DebugLog(string message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif
        }

        /// <summary>
        /// Initializes a new TrayApplicationContext, setting up the tray icon UI and starting background services.
        /// </summary>
        public TrayApplicationContext()
        {
            DebugLog("[TrayApplicationContext] InitializeTrayIcon 호출");
            InitializeTrayIcon();

            DebugLog("[TrayApplicationContext] StartServices 호출");
            StartServices();
            DebugLog("[TrayApplicationContext] 생성자 완료");
        }


        /// <summary>
        /// Creates and configures the system tray icon and its context menu for the application.
        /// </summary>
        /// <remarks>
        /// Attempts to load an application icon (executable icon, then app_icon.ico) and falls back to the system application icon if none is found. Initializes a NotifyIcon (text set to "App Launcher", visible) and attaches a context menu with the following items wired to their handlers:
        /// "앱 실행" (show main form), "설정" (launcher settings), "MQTT 제어 센터" (MQTT control), and "종료" (exit).
        /// </remarks>
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

        /// <summary>
        /// Loads the launcher configuration and initializes MQTT status reporting when the global MQTT service is connected.
        /// </summary>
        /// <remarks>
        /// Loads configuration from ConfigManager. If ServiceContainer.MqttService reports a connected state, sends a "connected" status message via the MQTT message handler and starts the periodic status timer. Any exceptions are reported to the user via a tray balloon tip.
        /// </remarks>
        private void StartServices()
        {
            try
            {
                // 설정 로드
                _config = ConfigManager.LoadConfig();

                if (ServiceContainer.MqttService.IsConnected)
                {
                    _mqttMessageHandler?.SendStatus("connected");
                    StartStatusTimer();
                }
            }
            catch (Exception ex)
            {
                _notifyIcon?.ShowBalloonTip(3000, "오류", ex.Message, ToolTipIcon.Error);
            }
        }



        /// <summary>
        /// Handle changes in the MQTT connection state by starting or stopping timers and sending connection status messages.
        /// </summary>
        /// <param name="isConnected">`true` when the MQTT service is connected; `false` when disconnected.</param>
        private void OnMqttConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                // 연결 성공 시 재연결 타이머 중지
                StopReconnectTimer();

                // 연결 성공 시 초기 상태 전송
                _mqttMessageHandler?.SendStatus("connected");

                // 1분마다 상태 전송 타이머 시작
                StartStatusTimer();
            }
            else
            {
                // 연결 끊어지면 상태 타이머 중지
                StopStatusTimer();

                // 1분마다 재연결 시도 타이머 시작
                StartReconnectTimer();
            }
        }

        private void StartStatusTimer()
        {
            // 기존 타이머가 있으면 중지
            StopStatusTimer();

            // 1분(60초) 간격으로 타이머 생성
            _statusTimer = new System.Timers.Timer(60000);
            _statusTimer.Elapsed += OnStatusTimerElapsed;
            _statusTimer.AutoReset = true;
            _statusTimer.Start();
            Console.WriteLine("[MQTT] Status timer started (interval: 60 seconds)");
        }

        private void StopStatusTimer()
        {
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Elapsed -= OnStatusTimerElapsed;
                _statusTimer.Dispose();
                _statusTimer = null;

                Console.WriteLine("[MQTT] Status timer stopped");
            }
        }

        private void OnStatusTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                // 1분마다 자동으로 상태 전송
                _mqttMessageHandler?.SendStatus("current_status");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Status timer error: {ex.Message}");
            }
        }

        private void StartReconnectTimer()
        {
            // 기존 타이머가 있으면 중지
            StopReconnectTimer();

            // 1분(60초) 간격으로 재연결 시도 타이머 생성
            _reconnectTimer = new System.Timers.Timer(60000);
            _reconnectTimer.Elapsed += OnReconnectTimerElapsed;
            _reconnectTimer.AutoReset = true;
            _reconnectTimer.Start();

            Console.WriteLine("[MQTT] Reconnect timer started (interval: 60 seconds)");
        }

        private void StopReconnectTimer()
        {
            if (_reconnectTimer != null)
            {
                _reconnectTimer.Stop();
                _reconnectTimer.Elapsed -= OnReconnectTimerElapsed;
                _reconnectTimer.Dispose();
                _reconnectTimer = null;

                Console.WriteLine("[MQTT] Reconnect timer stopped");
            }
        }

        /// <summary>
        /// Handles the reconnect timer tick by attempting to establish an MQTT connection if the global MQTT service is disconnected.
        /// </summary>
        /// <remarks>
        /// Writes a reconnect attempt message to the console and, if the global MQTT service exists and is not connected, calls its ConnectAsync method. Exceptions are caught and written to the console.
        /// </remarks>
        private async void OnReconnectTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                Console.WriteLine("[MQTT] Attempting to reconnect...");

                if (ServiceContainer.MqttService != null && !ServiceContainer.MqttService.IsConnected)
                {
                    await ServiceContainer.MqttService.ConnectAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Reconnect failed: {ex.Message}");
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

        /// <summary>
        /// Shows the MQTT control window, creating and displaying a new MqttControlForm if one is not already open.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">Event arguments.</param>
        /// <remarks>
        /// When the control window is closed the reference is cleared and the launcher configuration is reloaded.
        /// </remarks>
        private void ShowMqttControlWindow(object? sender, EventArgs e)
        {
            if (_mqttControlForm == null || _mqttControlForm.IsDisposed)
            {
                // 전역 MqttService 사용
                _mqttControlForm = new MqttControlForm();
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

        /// <summary>
        /// Closes open windows, disconnects the global MQTT service, cleans up resources, and terminates the process.
        /// </summary>
        /// <remarks>
        /// Closes any active forms, disconnects ServiceContainer.MqttService if connected, disposes internal resources,
        /// waits briefly to allow clean shutdown, and calls Environment.Exit(0). If an exception occurs during shutdown,
        /// the exception is written to debug output and the process exits with code 1.
        /// </remarks>
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
                if (ServiceContainer.MqttService.IsConnected)
                {
                    await ServiceContainer.MqttService.DisconnectAsync();
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

        /// <summary>
        /// Releases timers and tray icon resources used by the application context.
        /// </summary>
        public new void Dispose()
        {
            // 상태 전송 타이머 정리
            StopStatusTimer();

            // 재연결 타이머 정리
            StopReconnectTimer();

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