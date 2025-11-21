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

        private async void ShowMainForm(object? sender, EventArgs e)
        {
            try
            {
                var config = ConfigManager.LoadConfig();

                // ServiceContainer의 ApplicationLauncher 사용
                var launcher = ServiceContainer.AppLauncher;

                if (launcher == null)
                {
                    // ApplicationLauncher가 아직 생성되지 않았으면 생성
                    launcher = new ApplicationLauncher();
                    ServiceContainer.AppLauncher = launcher;
                }

                // 현재 실행 중인 프로세스 확인
                var runningProcess = launcher.GetRunningProcess();

                if (runningProcess != null && runningProcess.IsRunning)
                {
                    MessageBox.Show(
                        $"이미 실행 중입니다.\n\n{launcher.GetProcessStatusSummary()}",
                        "앱 실행",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return;
                }

                // 파일 존재 확인
                if (string.IsNullOrEmpty(config.TargetExecutable) || !File.Exists(config.TargetExecutable))
                {
                    MessageBox.Show(
                        "대상 실행 파일이 설정되지 않았거나 존재하지 않습니다.\n설정에서 실행 파일을 지정해주세요.",
                        "앱 실행",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                    return;
                }

                // 앱 실행
                Action<string> statusCallback = status => DebugLog($"[LAUNCH] {status}");
                await launcher.CheckAndLaunchInBackgroundAsync(config, statusCallback);

                MessageBox.Show(
                    "앱을 실행했습니다.",
                    "앱 실행",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"앱 실행 오류:\n{ex.Message}",
                    "오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
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
