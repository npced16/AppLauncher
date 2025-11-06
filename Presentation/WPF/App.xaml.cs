using System;
using System.Windows;
using Microsoft.Win32;
using AppLauncher.Features.TrayApp;
using AppLauncher.Shared;

namespace AppLauncher.Presentation.WPF
{
    public partial class App : Application
    {
        private TrayApplicationContext? _trayContext;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 작업 스케줄러에 등록되어 있는지 확인
            if (!TaskSchedulerManager.IsTaskRegistered())
            {
                // 등록되어 있지 않으면 등록 시도
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                bool registered = TaskSchedulerManager.RegisterTask(exePath);

                if (registered)
                {
                    MessageBox.Show("시작 프로그램에 등록되었습니다.\n다음 로그인부터 자동으로 실행됩니다.",
                        "등록 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("시작 프로그램 등록에 실패했습니다.\n관리자 권한으로 실행해주세요.",
                        "등록 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // 트레이 앱 시작 (manifest에서 관리자 권한 요구)
            _trayContext = new TrayApplicationContext();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayContext?.Dispose();
            base.OnExit(e);
        }

        public static void UnregisterStartup()
        {
            try
            {
                string appName = "AppLauncher";

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        if (key.GetValue(appName) != null)
                        {
                            key.DeleteValue(appName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시작프로그램 등록 해제 실패: {ex.Message}", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
