using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using AppLauncher.Features.TrayApp;
using AppLauncher.Shared;
using System.IO;
using AppLauncher.Features.VersionManagement;
using AppLauncher.Presentation.WinForms;
using AppLauncher.Shared.Configuration;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared.Services;

namespace AppLauncher
{
    static class Program
    {
        private static Mutex? _mutex;


        /// <summary>
        /// 업데이트 후 남은 구버전 파일(.old) 삭제
        /// </summary>
        private static void CleanupOldVersion()
        {
            try
            {
                string currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentExePath))
                    return;

                string oldFilePath = currentExePath + ".old";
                if (File.Exists(oldFilePath))
                {
                    DebugLogger.Log("CLEANUP", $"구버전 파일 발견: {oldFilePath}");

                    // 파일 삭제 시도 (최대 3번)
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.Delete(oldFilePath);
                            DebugLogger.Log("CLEANUP", "구버전 파일 삭제 완료");
                            break;
                        }
                        catch
                        {
                            if (i < 2)
                            {
                                System.Threading.Thread.Sleep(500);
                            }
                            else
                            {
                                DebugLogger.Log("CLEANUP", "구버전 파일 삭제 실패 (재부팅 시 삭제됨)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("CLEANUP", $"오류: {ex.Message}");
            }
        }
        /// <summary>
        /// Program Files가 아닌 곳에서 실행되면 자동으로 설치
        /// </summary>
        private static void CheckAndInstallToSystemPath()
        {
            DebugLogger.Log("Install", "\nCheckAndInstallToSystemPath 시작");
            try
            {
                string currentExePath =
                    Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                DebugLogger.Log("Install", $"현재 실행 경로: {currentExePath}");

                if (string.IsNullOrEmpty(currentExePath))
                {
                    DebugLogger.Log("Install", "경로가 비어있음 - 종료");
                    return;
                }

                string programFilesPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles
                );
                DebugLogger.Log("Install", $"Program Files 경로: {programFilesPath}");

                string targetDir = Path.Combine(programFilesPath, "AppLauncher");
                string targetExePath = Path.Combine(targetDir, "AppLauncher.exe");
                DebugLogger.Log("Install", $"목표 경로: {targetExePath}");

                // 이미 Program Files에서 실행 중이면 스킵
                if (currentExePath.Equals(targetExePath, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLogger.Log("Install", "이미 Program Files에서 실행 중 - 스킵");
                    return;
                }

                DebugLogger.Log("Install", "Program Files로 복사 시작...");

                // 기존 AppLauncher 프로세스 종료
                DebugLogger.Log("Install", "기존 프로세스 종료 시도...");
                KillExistingProcessesAtPath(targetExePath);
                DebugLogger.Log("Install", "기존 프로세스 종료 완료");

                // Program Files로 복사
                if (!Directory.Exists(targetDir))
                {
                    DebugLogger.Log("Install", $"디렉토리 생성: {targetDir}");
                    Directory.CreateDirectory(targetDir);
                }

                DebugLogger.Log("Install", "파일 복사 중...");
                File.Copy(currentExePath, targetExePath, true);
                DebugLogger.Log("Install", "파일 복사 완료");

                // Program Files 버전 실행하고 현재 프로세스 (APPluncher) 종료
                // 따라서 .NET 런타임 설치 여부와 상관없이 실행 가능
                // 이미 관리자 권한으로 실행 중이므로 권한 문제 없음
                DebugLogger.Log("Install", "Program Files 버전 실행...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = targetExePath,
                };
                Process.Start(startInfo);

                DebugLogger.Log("Install", "현재 프로세스 종료");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DebugLogger.Log("Install", $"오류 발생: {ex.GetType().Name}");
                DebugLogger.Log("Install", $"오류 메시지: {ex.Message}");
                DebugLogger.Log("Install", $"스택 트레이스:\n{ex.StackTrace}");

                // 설치 실패 - 사용자에게 알림
                MessageBox.Show(
                    $"Program Files 설치 실패:\n{ex.Message}\n\n현재 위치에서 계속 실행합니다.",
                    "설치 실패",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }



        [STAThread]
        static void Main()
        {

            DebugLogger.Log("Program", "=== AppLauncher 시작 ===");

            // 구버전 파일 삭제
            CleanupOldVersion();

            // 1년 이상 된 로그 파일 삭제
            DebugLogger.Log("CLEANUP", "로그 파일 정리 시작...");
            LabViewUpdater.CleanupOldLogFiles();
            DebugLogger.Log("CLEANUP", "로그 파일 정리 완료");

#if !DEBUG
            // 자동 설치: Program Files가 아닌 곳에서 실행되면 자동으로 Program Files로 복사 (Release 모드에서만)
            DebugLogger.Log("Program", "CheckAndInstallToSystemPath 호출");
            CheckAndInstallToSystemPath();
#else
            DebugLogger.Log("Program", "Debug 모드 - 자동 설치 스킵");
#endif



            const string mutexName = "Global\\AppLauncher_SingleInstance";
            bool createdNew;
            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                DebugLogger.Log("Main", "이미 실행 중 - 기존 프로세스 종료");
                // 이미 실행 중 - 기존 프로세스 종료하고 새로 실행
                KillExistingProcesses();

                // Mutex 다시 생성
                _mutex?.Dispose();
                _mutex = new Mutex(true, mutexName, out createdNew);

                if (!createdNew)
                {
                    DebugLogger.Log("Main", "기존 프로세스 종료 실패");
                    MessageBox.Show(
                        "기존 프로세스를 종료하지 못했습니다.",
                        "오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }
                DebugLogger.Log("Main", "기존 프로세스 종료 성공");
            }

            var config = ConfigManager.LoadConfig();
            DebugLogger.Log("Main", "ServiceContainer 초기화 중...");
            ServiceContainer.Initialize(config);

            // 작업 스케줄러에 등록되어 있는지 확인
            DebugLogger.Log("Main", "작업 스케줄러 확인...");
            if (!TaskSchedulerManager.IsTaskRegistered())
            {
                DebugLogger.Log("Main", "작업 스케줄러 등록 시도");
                // 등록되어 있지 않으면 등록 시도 (self-contained 빌드 지원)
                string exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                bool registered = TaskSchedulerManager.RegisterTask(exePath);

                if (registered)
                {
                    DebugLogger.Log("Main", "작업 스케줄러 등록 성공");
                    MessageBox.Show("시작 프로그램에 등록되었습니다.\n다음 로그인부터 자동으로 실행됩니다.",
                        "등록 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    DebugLogger.Log("Main", "작업 스케줄러 등록 실패");
                    MessageBox.Show("시작 프로그램 등록에 실패했습니다.\n관리자 권한으로 실행해주세요.",
                        "등록 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                DebugLogger.Log("Main", "작업 스케줄러에 이미 등록됨");
            }

            // WinForms 앱 설정
            DebugLogger.Log("Main", "\nWinForms 앱 설정...");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 예약된 업데이트 확인
            DebugLogger.Log("Main", "Pending update 확인...");
            if (PendingUpdateManager.HasPendingUpdate())
            {
                UpdateLauncher();
            }
            else
            {
                startSWApp();
            }


        }

        private static void UpdateLauncher()
        {
            DebugLogger.Log("Main", "Pending update 발견! 업데이트 진행...");
            // HBOT Operator 언인스톨
            bool success = UninstallSWService.UninstallHbotOperator();

            try
            {
                var command = PendingUpdateManager.LoadPendingUpdate();
                if (command != null)
                {
                    var config = ConfigManager.LoadConfig();
                    using (var updateForm = new UpdateProgressForm(command, config))
                    {
                        DebugLogger.Log("Main", "UpdateProgressForm 실행...");
                        Application.Run(updateForm);
                        DebugLogger.Log("Main", "UpdateProgressForm 종료");
                    }
                    DebugLogger.Log("Main", "업데이트 완료. 컴퓨터 재시작 예정...");
                    return;
                }
                // 파일은 있지만 로드 실패한 경우: 로그 남기고 pending 제거 후 정상 모드로 진행
                DebugLogger.Log("Main", "Pending update 파일을 로드하지 못했습니다. 정상 모드로 진행합니다.");
                PendingUpdateManager.ClearPendingUpdate();
                startSWApp();
            }
            catch (Exception ex)
            {
                DebugLogger.Log("Main", $"업데이트 처리 오류: {ex.Message}");
                MessageBox.Show(
                    $"업데이트 처리 중 오류가 발생했습니다:\n{ex.Message}\n\n정상 모드로 계속 진행합니다.",
                    "업데이트 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                startSWApp();
            }
        }

        private static void startSWApp()
        {

            var config = ConfigManager.LoadConfig();
            try
            {
                // 파일 존재 여부 체크
                if (!string.IsNullOrEmpty(config.TargetExecutable))
                {
                    if (File.Exists(config.TargetExecutable))
                    {
                        // 파일이 있으면 정상 실행 (오류 발생 시 업데이트 요청)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var launcher = new ApplicationLauncher();
                                ServiceContainer.AppLauncher = launcher; // 전역 컨테이너에 저장
                                Action<string> statusCallback = status => DebugLogger.Log("LAUNCH", $"{status}");
                                await launcher.CheckAndLaunchInBackgroundAsync(config, statusCallback);
                                DebugLogger.Log("Main", "백그라운드 프로그램 시작 완료");
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Log("Main", $"백그라운드 프로그램 시작 오류: {ex.Message}");
                                DebugLogger.Log("Main", "서버에 업데이트 요청...");
                                try
                                {
                                    await RequestUpdateCall();
                                }
                                catch (Exception reqEx)
                                {
                                    DebugLogger.Log("Main", $"업데이트 요청 실패: {reqEx.Message}");
                                }
                            }
                        });
                    }
                    else
                    {
                        DebugLogger.Log("Main", "대상 파일이 존재하지 않음. 서버에 업데이트 요청...");
                        _ = RequestUpdateCall(); // Fire-and-forget (백그라운드에서 실행)
                    }
                }
                else
                {
                    DebugLogger.Log("Main", "대상 프로그램이 설정되지 않음");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("Main", $"백그라운드 프로그램 시작 오류: {ex.Message}");
            }

            DebugLogger.Log("Main", "TrayApplicationContext 생성...");
            using (var trayContext = new TrayApplicationContext())
            {
                DebugLogger.Log("Main", "Application.Run 시작");
                Application.Run(trayContext);
                DebugLogger.Log("Main", "Application.Run 종료");
            }

            // 정리
            DebugLogger.Log("Main", "정리 중...");

            // ServiceContainer 정리 (MQTT 연결 해제 등)
            ServiceContainer.Dispose();

            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            DebugLogger.Log("Main", "종료 완료");
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

        /// <summary>
        /// 특정 경로에서 실행 중인 AppLauncher 프로세스 종료
        /// </summary>
        private static void KillExistingProcessesAtPath(string targetPath)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();

                // 같은 이름의 프로세스 찾기
                var processes = Process.GetProcessesByName("AppLauncher")
                    .Where(p => p.Id != currentProcess.Id)
                    .ToList();

                foreach (var process in processes)
                {
                    try
                    {
                        string? processPath = process.MainModule?.FileName;

                        // 대상 경로와 일치하는 프로세스만 종료
                        if (!string.IsNullOrEmpty(processPath) &&
                            processPath.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
                        {
                            DebugLogger.Log("Install", $"프로세스 종료: PID={process.Id}, Path={processPath}");
                            process.Kill();
                            process.WaitForExit(5000); // 5초 대기
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log("Install", $"프로세스 종료 실패: {ex.Message}");
                    }
                }

                Thread.Sleep(1000); // 프로세스가 완전히 종료될 때까지 대기
            }
            catch (Exception ex)
            {
                DebugLogger.Log("Install", $"KillExistingProcessesAtPath 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 서버에 LabVIEW 업데이트 요청 (파일 없을 때)
        /// MQTT 연결될 때까지 재시도
        /// </summary>
        private static async Task RequestUpdateCall()
        {
            var mqttService = ServiceContainer.MqttService;
            var handler = ServiceContainer.MqttMessageHandler;

            if (mqttService == null || handler == null)
            {
                DebugLogger.Log("Main", "MQTT 서비스가 초기화되지 않아 업데이트 요청 불가");
                return;
            }

            // MQTT 연결될 때까지 재시도 (최대 3분)
            int retryCount = 0;
            int maxRetries = 36; // 5초 * 36 = 180초 = 3분

            while (!mqttService.IsConnected && retryCount < maxRetries)
            {
                try
                {
                    DebugLogger.Log("Main", $"MQTT 연결 시도 중... ({retryCount + 1}/{maxRetries})");
                    await mqttService.ConnectAsync();

                    if (mqttService.IsConnected)
                    {
                        DebugLogger.Log("Main", "MQTT 연결 성공!");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log("Main", $"MQTT 연결 실패: {ex.Message}");
                }

                retryCount++;
                if (!mqttService.IsConnected && retryCount < maxRetries)
                {
                    DebugLogger.Log("Main", "5초 후 재시도...");
                    await Task.Delay(5000); // 5초 대기
                }
            }

            // 연결 성공 여부 확인
            if (mqttService.IsConnected)
            {
                DebugLogger.Log("Main", "MQTT 연결 완료. 업데이트 요청 전송...");
                handler.RequestLabViewUpdate("file_not_found");
            }
            else
            {
                DebugLogger.Log("Main", "MQTT 연결 실패. 업데이트 요청을 보낼 수 없습니다.");
            }
        }
    }
}
