using System;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using Application = System.Windows.Application;

namespace AppLauncher
{
    public class TrayApplicationContext : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        public TrayApplicationContext()
        {
            InitializeTrayIcon();
            StartLauncher();
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

            var unregisterMenuItem = new ToolStripMenuItem("시작프로그램 등록 해제");
            unregisterMenuItem.Click += UnregisterFromStartup;
            contextMenu.Items.Add(unregisterMenuItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitMenuItem = new ToolStripMenuItem("종료");
            exitMenuItem.Click += Exit;
            contextMenu.Items.Add(exitMenuItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += ShowWindow;
        }

        private async void StartLauncher()
        {
            try
            {
                _notifyIcon!.Text = "App Launcher - 버전 확인 중...";

                var config = ConfigManager.LoadConfig();
                var launcher = new ApplicationLauncher(null);

                await launcher.CheckAndLaunchInBackgroundAsync(config, UpdateTrayStatus);

                _notifyIcon.Text = "App Launcher - 완료";

                // 5초 후 트레이 아이콘 제거 및 종료
                await System.Threading.Tasks.Task.Delay(5000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                _notifyIcon!.Text = $"App Launcher - 오류: {ex.Message}";
                _notifyIcon.ShowBalloonTip(3000, "오류", ex.Message, ToolTipIcon.Error);

                await System.Threading.Tasks.Task.Delay(5000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown();
                });
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

        private void UnregisterFromStartup(object? sender, EventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "시작프로그램 등록을 해제하시겠습니까?",
                "등록 해제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                bool success = TaskSchedulerManager.UnregisterTask();

                if (success)
                {
                    System.Windows.MessageBox.Show(
                        "시작프로그램 등록이 해제되었습니다.",
                        "완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "등록 해제에 실패했습니다.\n관리자 권한으로 실행해주세요.",
                        "실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void Exit(object? sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
        }
    }
}
