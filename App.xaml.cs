using System.Windows;
using Microsoft.Win32;

namespace AppLauncher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 시작프로그램 등록 (첫 실행시)
            RegisterStartup();
        }

        private void RegisterStartup()
        {
            try
            {
                string appName = "AppLauncher";
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");

                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        var currentValue = key.GetValue(appName) as string;

                        // 이미 등록되어 있고 경로가 같으면 스킵
                        if (currentValue == exePath)
                            return;

                        // 등록 또는 업데이트
                        key.SetValue(appName, exePath);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시작프로그램 등록 실패: {ex.Message}", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
