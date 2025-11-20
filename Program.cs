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

        private static void DebugLog(string message)
        {
#if DEBUG
            Console.WriteLine(message);
#endif
        }

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
                    DebugLog($"[CLEANUP] 구버전 파일 발견: {oldFilePath}");

                    // 파일 삭제 시도 (최대 3번)
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            File.Delete(oldFilePath);
                            DebugLog($"[CLEANUP] 구버전 파일 삭제 완료");
                            Console.WriteLine($"[CLEANUP] Old version file deleted: {oldFilePath}");
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
                                DebugLog($"[CLEANUP] 구버전 파일 삭제 실패 (재부팅 시 삭제됨)");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"[CLEANUP] 오류: {ex.Message}");
            }
        }
        /// <summary>
        /// Program Files가 아닌 곳에서 실행되면 자동으로 설치
        /// </summary>
        private static void CheckAndInstallToSystemPath()
        {
            DebugLog("\n[설치] CheckAndInstallToSystemPath 시작");
            try
            {
                string currentExePath =
                    Environment.ProcessPath
                    ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
                DebugLog($"[설치] 현재 실행 경로: {currentExePath}");

                if (string.IsNullOrEmpty(currentExePath))
                {
                    DebugLog("[설치] 경로가 비어있음 - 종료");
                    return;
                }

                string programFilesPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles
                );
                DebugLog($"[설치] Program Files 경로: {programFilesPath}");

                string targetDir = Path.Combine(programFilesPath, "AppLauncher");
                string targetExePath = Path.Combine(targetDir, "AppLauncher.exe");
                DebugLog($"[설치] 목표 경로: {targetExePath}");

                // 이미 Program Files에서 실행 중이면 스킵
                if (currentExePath.Equals(targetExePath, StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog("[설치] 이미 Program Files에서 실행 중 - 스킵");
                    return;
                }

                DebugLog("[설치] Program Files로 복사 시작...");

                // Program Files로 복사
                if (!Directory.Exists(targetDir))
                {
                    DebugLog($"[설치] 디렉토리 생성: {targetDir}");
                    Directory.CreateDirectory(targetDir);
                }

                DebugLog("[설치] 파일 복사 중...");
                File.Copy(currentExePath, targetExePath, true);
                DebugLog("[설치] 파일 복사 완료");

                // Program Files 버전 실행하고 현재 프로세스 종료
                // 작업 스케줄러 등록은 Program.cs에서 처리
                DebugLog("[설치] Program Files 버전 실행...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = targetExePath,
                    UseShellExecute = true,
                };
                Process.Start(startInfo);

                DebugLog("[설치] 현재 프로세스 종료");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                DebugLog($"[설치] 오류 발생: {ex.GetType().Name}");
                DebugLog($"[설치] 오류 메시지: {ex.Message}");
                DebugLog($"[설치] 스택 트레이스:\n{ex.StackTrace}");

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
#if DEBUG
            Console.WriteLine("=== AppLauncher 시작 ===");
#endif
            // 중복 실행 방지
            DebugLog("\n[Main] 중복 실행 체크...");

            // 구버전 파일 삭제
            CleanupOldVersion();

            // 1년 이상 된 로그 파일 삭제
            DebugLog("[CLEANUP] 로그 파일 정리 시작...");
            LabViewUpdater.CleanupOldLogFiles();
            DebugLog("[CLEANUP] 로그 파일 정리 완료");

#if !DEBUG
            // 자동 설치: Program Files가 아닌 곳에서 실행되면 자동으로 Program Files로 복사 (Release 모드에서만)
            DebugLog("[Program] CheckAndInstallToSystemPath 호출");
            CheckAndInstallToSystemPath();
#else
            DebugLog("[Program] Debug 모드 - 자동 설치 스킵");
#endif

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

            // 예약된 업데이트 확인
            DebugLog("[Main] Pending update 확인...");
            if (PendingUpdateManager.HasPendingUpdate())
            {
                UpdateLauncher();
            }
            else
            {
                startAppAndMQTT();
            }


        }

        private static void UpdateLauncher()
        {
            DebugLog("[Main] Pending update 발견! 업데이트 진행...");

            try
            {
                var pendingUpdate = PendingUpdateManager.LoadPendingUpdate();
                if (pendingUpdate != null)
                {
                    var config = ConfigManager.LoadConfig();
                    using (var updateForm = new UpdateProgressForm(pendingUpdate, config))
                    {
                        DebugLog("[Main] UpdateProgressForm 실행...");
                        Application.Run(updateForm);
                        DebugLog("[Main] UpdateProgressForm 종료");
                    }
                    DebugLog("[Main] 업데이트 완료. 컴퓨터 재시작 예정...");
                    return;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"[Main] 업데이트 처리 오류: {ex.Message}");
                MessageBox.Show(
                    $"업데이트 처리 중 오류가 발생했습니다:\n{ex.Message}\n\n정상 모드로 계속 진행합니다.",
                    "업데이트 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                startAppAndMQTT();
            }
        }

        private static void startAppAndMQTT()
        {

            // 업데이트가 없으면 백그라운드 프로그램 시작
            DebugLog("[Main] 업데이트 없음. 백그라운드 프로그램 시작...");
            var config = ConfigManager.LoadConfig();

            // 서비스 컨테이너 초기화
            DebugLog("[Main] ServiceContainer 초기화 중...");
            ServiceContainer.Initialize(config);

            try
            {
                // 파일 존재 여부 체크
                if (!string.IsNullOrEmpty(config.TargetExecutable))
                {
                    if (File.Exists(config.TargetExecutable))
                    {
                        // 파일이 있으면 정상 실행
                        var launcher = new ApplicationLauncher();
                        Action<string> statusCallback = status => DebugLog($"[LAUNCH] {status}");
                        _ = launcher.CheckAndLaunchInBackgroundAsync(config, statusCallback);

                        DebugLog("[Main] 백그라운드 프로그램 시작 완료");
                    }
                    else
                    {
                        // 파일이 없으면 MQTT를 통해 서버에 업데이트 요청
                        DebugLog("[Main] 대상 파일이 존재하지 않음. 서버에 업데이트 요청...");
                        _ = RequestLabViewUpdateFromServerAsync(config);
                    }
                }
                else
                {
                    DebugLog("[Main] 대상 프로그램이 설정되지 않음");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"[Main] 백그라운드 프로그램 시작 오류: {ex.Message}");
            }

            DebugLog("[Main] TrayApplicationContext 생성...");
            using (var trayContext = new TrayApplicationContext())
            {
                DebugLog("[Main] Application.Run 시작");
                Application.Run(trayContext);
                DebugLog("[Main] Application.Run 종료");
            }

            // MQTT 서비스 정리
            if (ServiceContainer.MqttService != null)
            {
                DebugLog("[Main] MQTT 서비스 정리 중...");
                ServiceContainer.MqttService.DisconnectAsync().Wait();
                ServiceContainer.MqttService.Dispose();
                ServiceContainer.MqttService = null;
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

        /// <summary>
        /// 서버에 LabVIEW 업데이트 요청 (파일 없을 때)
        /// </summary>
        private static async Task RequestLabViewUpdateFromServerAsync(LauncherConfig config)
        {
            try
            {
                if (ServiceContainer.MqttService == null)
                {
                    DebugLog("[UPDATE_REQUEST] 전역 MQTT 서비스가 초기화되지 않았습니다");
                    return;
                }

                DebugLog("[UPDATE_REQUEST] 전역 MQTT 서비스 사용");

                // 메시지 수신 이벤트 핸들러
                bool updateReceived = false;
                LaunchCommand? receivedCommand = null;
                TaskCompletionSource<bool> updateReceivedTcs = new TaskCompletionSource<bool>();

                void MessageHandler(MqttMessage msg)
                {
                    try
                    {
                        ServiceContainer.MqttMessageHandler.HandleMessage(msg);
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"[UPDATE_REQUEST] 메시지 처리 오류: {ex.Message}");
                    }
                }

                // 임시 이벤트 핸들러 등록
                ServiceContainer.MqttService.MessageReceived += MessageHandler;

                try
                {
                    // MQTT 연결 (아직 연결되지 않은 경우)
                    if (!ServiceContainer.MqttService.IsConnected)
                    {
                        DebugLog("[UPDATE_REQUEST] MQTT 연결 중...");
                        await ServiceContainer.MqttService.ConnectAsync();

                        if (!ServiceContainer.MqttService.IsConnected)
                        {
                            DebugLog("[UPDATE_REQUEST] MQTT 연결 실패");
                            return;
                        }

                        DebugLog("[UPDATE_REQUEST] MQTT 연결 성공");
                    }
                    else
                    {
                        DebugLog("[UPDATE_REQUEST] 이미 MQTT 연결됨");
                    }

                    // 서버에 업데이트 요청 전송
                    string hardwareUuid = HardwareInfo.GetHardwareUuid();
                    var request = new
                    {
                        status = "labview_update_request",
                        payload = new
                        {
                            reason = "file_not_found",
                            currentVersion = "0.0.0",
                            hardwareUUID = hardwareUuid,
                            location = config.MqttSettings.Location,
                            timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                        }
                    };
                    // SendStatus("labview_update_request");

                    await ServiceContainer.MqttService.PublishJsonAsync(ServiceContainer.MqttService.StatusTopic, request);
                    DebugLog("[UPDATE_REQUEST] 업데이트 요청 전송 완료");

                    // 서버 응답 대기 (최대 30초)
                    DebugLog("[UPDATE_REQUEST] 서버 응답 대기 중... (최대 30초)");
                    var timeoutTask = Task.Delay(30000);
                    var completedTask = await Task.WhenAny(updateReceivedTcs.Task, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        DebugLog("[UPDATE_REQUEST] 서버 응답 타임아웃 (30초)");
                        return;
                    }

                    if (updateReceived && receivedCommand != null && !string.IsNullOrEmpty(receivedCommand.URL))
                    {
                        DebugLog($"[UPDATE_REQUEST] 다운로드 시작: {receivedCommand.URL}");

                        // 다운로드 및 설치
                        var updater = new LabViewUpdater(receivedCommand, config);
                        string downloadedPath = await updater.DownloadAndExecuteAsync();

                        if (!string.IsNullOrEmpty(downloadedPath))
                        {
                            DebugLog("[UPDATE_REQUEST] 설치 완료. 앱 실행...");

                            // 설치 완료 후 앱 실행
                            await Task.Delay(2000);
                            if (File.Exists(config.TargetExecutable))
                            {
                                var launcher = new ApplicationLauncher();
                                Action<string> statusCallback = status => DebugLog($"[LAUNCH] {status}");
                                _ = launcher.CheckAndLaunchInBackgroundAsync(config, statusCallback);
                            }
                        }
                        else
                        {
                            DebugLog("[UPDATE_REQUEST] 설치 실패");
                        }
                    }
                }
                finally
                {
                    // 임시 이벤트 핸들러 제거
                    ServiceContainer.MqttService.MessageReceived -= MessageHandler;
                }
            }
            catch (Exception ex)
            {
                DebugLog($"[UPDATE_REQUEST] 오류 발생: {ex.Message}");
            }
        }
    }
}
