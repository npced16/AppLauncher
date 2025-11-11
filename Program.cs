using System;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using AppLauncher.Features.TrayApp;
using AppLauncher.Shared;

namespace AppLauncher
{
    static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            // 중복 실행 방지
            const string mutexName = "Global\\AppLauncher_SingleInstance";
            bool createdNew;
            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // 이미 실행 중
                MessageBox.Show(
                    "AppLauncher가 이미 실행 중입니다.\n트레이 아이콘을 확인해주세요.",
                    "중복 실행",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
                return;
            }

            // 작업 스케줄러에 등록되어 있는지 확인
            if (!TaskSchedulerManager.IsTaskRegistered())
            {
                // 등록되어 있지 않으면 등록 시도 (self-contained 빌드 지원)
                string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                bool registered = TaskSchedulerManager.RegisterTask(exePath);

                if (registered)
                {
                    MessageBox.Show("시작 프로그램에 등록되었습니다.\n다음 로그인부터 자동으로 실행됩니다.",
                        "등록 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("시작 프로그램 등록에 실패했습니다.\n관리자 권한으로 실행해주세요.",
                        "등록 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            // WinForms 앱 설정
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 트레이 앱 시작
            using (var trayContext = new TrayApplicationContext())
            {
                Application.Run(trayContext);
            }

            // 정리
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
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
                MessageBox.Show($"시작프로그램 등록 해제 실패: {ex.Message}", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
