using System;
using System.Diagnostics;
using System.Linq;
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

        private static void DebugLog(string message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif
        }

        [STAThread]
        static void Main()
        {
#if DEBUG
            Console.WriteLine("=== AppLauncher 시작 ===");
#endif
            // 중복 실행 방지
            DebugLog("\n[Main] 중복 실행 체크...");
            const string mutexName = "Global\\AppLauncher_SingleInstance";
            bool createdNew;
            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                DebugLog("[Main] 이미 실행 중 - 기존 프로세스 종료");
                // 이미 실행 중 - 기존 프로세스 종료하고 새로 실행
                KillExistingProcesses();

                // Mutex 다시 생성
                _mutex?.Dispose();
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (!createdNew)
                {
                    DebugLog("[Main] 기존 프로세스 종료 실패");
                    MessageBox.Show(
                        "기존 프로세스를 종료하지 못했습니다.",
                        "오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }
                DebugLog("[Main] 기존 프로세스 종료 성공");
            }

            // 작업 스케줄러에 등록되어 있는지 확인
            DebugLog("[Main] 작업 스케줄러 확인...");
            if (!TaskSchedulerManager.IsTaskRegistered())
            {
                DebugLog("[Main] 작업 스케줄러 등록 시도");
                // 등록되어 있지 않으면 등록 시도 (self-contained 빌드 지원)
                string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                bool registered = TaskSchedulerManager.RegisterTask(exePath);

                if (registered)
                {
                    DebugLog("[Main] 작업 스케줄러 등록 성공");
                    MessageBox.Show("시작 프로그램에 등록되었습니다.\n다음 로그인부터 자동으로 실행됩니다.",
                        "등록 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    DebugLog("[Main] 작업 스케줄러 등록 실패");
                    MessageBox.Show("시작 프로그램 등록에 실패했습니다.\n관리자 권한으로 실행해주세요.",
                        "등록 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                DebugLog("[Main] 작업 스케줄러에 이미 등록됨");
            }

            // WinForms 앱 설정
            DebugLog("\n[Main] WinForms 앱 설정...");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 트레이 앱 시작
            DebugLog("[Main] TrayApplicationContext 생성...");
            using (var trayContext = new TrayApplicationContext())
            {
                DebugLog("[Main] Application.Run 시작");
                Application.Run(trayContext);
                DebugLog("[Main] Application.Run 종료");
            }

            // 정리
            DebugLog("[Main] 정리 중...");
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            DebugLog("[Main] 종료 완료");
        }

        /// <summary>
        /// 실행 중인 다른 AppLauncher 프로세스 종료
        /// </summary>
        private static void KillExistingProcesses()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                string currentExePath = Environment.ProcessPath ?? currentProcess.MainModule?.FileName ?? "";

                if (string.IsNullOrEmpty(currentExePath))
                    return;

                // 같은 이름의 프로세스 찾기
                var processes = Process.GetProcessesByName("AppLauncher")
                    .Where(p => p.Id != currentProcess.Id)
                    .ToList();

                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000); // 5초 대기
                    }
                    catch
                    {
                        // 종료 실패 무시
                    }
                }

                Thread.Sleep(500); // 프로세스가 완전히 종료될 때까지 대기
            }
            catch
            {
                // 오류 무시
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
                MessageBox.Show($"시작프로그램 등록 해제 실패: {ex.Message}", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
