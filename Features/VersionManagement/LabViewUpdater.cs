using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Security.Principal;
using System.Threading.Tasks;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Features.VersionManagement
{
    public class LabViewUpdater
    {
        private readonly LaunchCommand _command;
        private readonly LauncherConfig _config;
        private readonly Action<string, string>? _sendStatusResponse;

        public LabViewUpdater(
            LaunchCommand command,
            LauncherConfig config,
            Action<string, string>? sendStatusResponse = null,
            Action<string>? installStatusCallback = null
        )
        {
            _command = command;
            _config = config;
            _sendStatusResponse = sendStatusResponse;
        }


        /// <summary>
        /// 업데이트를 예약
        /// - isDownloadImmediate가 false인 경우: 다음 런처 재시작 시 자동 실행
        /// - isDownloadImmediate가 true인 경우: 런처를 즉시 재시작하여 업데이트 진행
        /// </summary>
        public async Task ScheduleUpdate(bool isDownloadImmediate)
        {
            try
            {
                Console.WriteLine("[SCHEDULE] Scheduling update for next launcher restart");

                // 업데이트 정보를 JSON에 저장
                var pendingUpdate = new PendingUpdate
                {
                    Command = _command,
                    ScheduledTime = DateTime.Now,
                    Description = $"챔버 소프트웨어 {_command.Version} 업데이트"
                };

                try
                {
                    bool saved = PendingUpdateManager.SavePendingUpdate(pendingUpdate);
                    if (!saved)
                    {
                        Console.WriteLine("[SCHEDULE] Failed to save pending update");
                        _sendStatusResponse?.Invoke("error", "업데이트 예약 저장 실패");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SCHEDULE] Exception saving pending update: {ex.Message}");
                    _sendStatusResponse?.Invoke("error", $"업데이트 예약 저장 실패: {ex.Message}");
                    return;
                }

                // 즉시 실행 모드: 런처 재시작하여 UpdateProgressForm으로 업데이트 진행
                if (isDownloadImmediate)
                {
                    await Task.Delay(1000);
                    RestartLauncher();
                }
                else
                {
                    Console.WriteLine("[SCHEDULE] Will be applied on next restart.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCHEDULE] Failed to schedule update: {ex.Message}");
                _sendStatusResponse?.Invoke("error", ex.Message);
            }
        }

        /// <summary>
        /// 런처 재시작
        /// </summary>
        private void RestartLauncher()
        {
            try
            {
                Console.WriteLine("[RESTART] Restarting launcher...");

                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (!string.IsNullOrEmpty(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                    };

                    Process.Start(startInfo);
                    Console.WriteLine("[RESTART] New launcher process started");

                    // 현재 프로세스 종료
                    //NOTE - 프로세스가 시작될떄 종료되긴하나 명시적으로 종료처리
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("[RESTART] Failed to get launcher path");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RESTART] Failed to restart launcher: {ex.Message}");
            }
        }

        /// <summary>
        /// setting.ini 파일 백업
        /// </summary>
        private string? BackupSettingFile()
        {
            try
            {
                // 원본 파일 경로
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string settingFilePath = Path.Combine(documentsPath, "HBOT", "Setting", "setting.ini");

                if (!File.Exists(settingFilePath))
                {
                    Console.WriteLine($"[BACKUP] setting.ini not found: {settingFilePath}");
                    return null;
                }

                // 백업 디렉토리 생성 (C:\ProgramData\AppLauncher\Backup)
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string backupDir = Path.Combine(programDataPath, "AppLauncher", "Backup");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // 백업 파일 경로 (타임스탬프 추가)
                string backupFilePath = Path.Combine(backupDir, $"setting_{DateTime.Now:yyyyMMddHHmmss}.ini");

                // 파일 복사
                File.Copy(settingFilePath, backupFilePath, overwrite: true);

                Console.WriteLine($"[BACKUP] Setting file backed up: {backupFilePath}");

                return backupFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKUP] Failed to backup setting file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// setting.ini 파일 복원
        /// </summary>
        private void RestoreSettingFile(string? backupFilePath)
        {
            _sendStatusResponse?.Invoke("restore_start", "설정 파일 복원 시작");
            try
            {
                if (string.IsNullOrEmpty(backupFilePath) || !File.Exists(backupFilePath))
                {
                    Console.WriteLine($"[RESTORE] No backup file to restore");
                    return;
                }

                // 복원 대상 경로
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string settingDir = Path.Combine(documentsPath, "HBOT", "Setting");
                string settingFilePath = Path.Combine(settingDir, "setting.ini");

                // 디렉토리가 없으면 생성
                if (!Directory.Exists(settingDir))
                {
                    Directory.CreateDirectory(settingDir);
                }

                // 파일 복원
                File.Copy(backupFilePath, settingFilePath, overwrite: true);

                Console.WriteLine($"[RESTORE] Setting file restored: {settingFilePath}");

                _sendStatusResponse?.Invoke("restore_done", "설정 파일 복원 완료");

                // 백업 파일 삭제 (선택사항)
                // File.Delete(backupFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RESTORE] Failed to restore setting file: {ex.Message}");
            }
        }



        /// <summary>
        /// 파일 다운로드 및 실행
        /// </summary>
        public async Task<string> DownloadAndExecuteAsync()
        {
            try
            {
                // 다운로드 디렉토리 생성 (C:\ProgramData\AppLauncher\Downloads)
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string tempDir = Path.Combine(programDataPath, "AppLauncher", "Downloads");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                // 파일명 추출
                if (string.IsNullOrEmpty(_command.URL))
                {
                    Console.WriteLine("[LabViewUpdater] URL이 비어있습니다.");
                    return "";
                }
                string fileName = Path.GetFileName(new Uri(_command.URL).LocalPath);
                bool isZipFile = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                bool isExeFile = fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                if (!isZipFile && !isExeFile)
                {
                    // 기본값: exe로 간주
                    fileName = $"download_{DateTime.Now:yyyyMMddHHmmss}.exe";
                }

                string downloadPath = Path.Combine(tempDir, fileName);
                _sendStatusResponse?.Invoke("download_start", "파일 다운로드 시작");
                // HttpClient로 다운로드
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromMinutes(10);
                    var response = await httpClient.GetAsync(_command.URL);
                    response.EnsureSuccessStatusCode();

                    using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                    _sendStatusResponse?.Invoke("download_complete", "파일 다운로드 완료");
                }

                if (!File.Exists(downloadPath))
                {
                    return "";
                }
                _sendStatusResponse?.Invoke("extract_start", "압축 해제 시작");

                // zip 파일이면 압축 해제
                if (isZipFile)
                {
                    await ExtractZipFileAsync(downloadPath);
                    _sendStatusResponse?.Invoke("extract_done", "압축 해제 완료");

                }
                else
                {
                    // exe 파일이면 실행
                    ExecuteProgram(downloadPath);
                }

                return downloadPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LabViewUpdater] 다운로드 및 실행 오류: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// ZIP 파일 압축 해제
        /// </summary>
        private async Task ExtractZipFileAsync(string zipFilePath)
        {
            try
            {

                // 압축 해제 대상 디렉토리 결정
                string extractDir;

                // zip 파일과 같은 폴더에 바로 압축 해제
                extractDir = Path.GetDirectoryName(zipFilePath) ?? Path.GetTempPath();

                // 압축 해제 디렉토리가 없으면 생성
                if (!Directory.Exists(extractDir))
                {
                    Directory.CreateDirectory(extractDir);
                }

                Console.WriteLine($"[ZIP] Extracting to: {extractDir}");

                // 기존 Volume 폴더가 있으면 삭제
                string volumeDir = Path.Combine(extractDir, "Volume");
                if (Directory.Exists(volumeDir))
                {
                    Console.WriteLine($"[ZIP] Deleting existing Volume folder: {volumeDir}");
                    Directory.Delete(volumeDir, recursive: true);
                }

                // 압축 해제 (비동기)
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, extractDir, overwriteFiles: true);
                });

                Console.WriteLine($"[ZIP] Extraction completed: {extractDir}");


                string HBOTOperatorPath = Path.Combine(extractDir, "HBOT Operator.exe");
                if (File.Exists(HBOTOperatorPath))
                {
                    Console.WriteLine($"[ZIP] Found HBOT Operator.exe: {HBOTOperatorPath}");

                    // 메타데이터 검증
                    if (!ValidateExecutableMetadata(HBOTOperatorPath, "HBOT Operator", "Ibex Medical Systems"))
                    {
                        Console.WriteLine($"[ZIP] HBOT Operator.exe validation failed - executing file directly");
                        _sendStatusResponse?.Invoke("validation_failed", "파일 검증 실패 - 파일 실행");

                        // 검증 실패 시 HBOT Operator.exe 실행
                        ExecuteProgram(HBOTOperatorPath);
                        return;
                    }
                    Console.WriteLine($"[ZIP] HBOT Operator.exe validation successful - proceeding with setup.exe");
                }
                else
                {
                    Console.WriteLine($"[ZIP] HBOT Operator.exe not found in {extractDir}");
                    return;
                }

                // Volume 폴더 안의 setup.exe 찾기
                string setupExePath = Path.Combine(volumeDir, "setup.exe");

                if (File.Exists(setupExePath))
                {
                    Console.WriteLine($"[ZIP] Found setup.exe in Volume folder: {setupExePath}");

                    // setup.exe 메타데이터 로그 출력
                    // LogExecutableMetadata(setupExePath);
                    // PowerShell로 setup.exe 실행
                    ExecuteSetupWithPowerShell(setupExePath);
                }
                else
                {
                    Console.WriteLine($"[ZIP] setup.exe not found in {volumeDir}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZIP] Extraction error: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로그램 실행
        /// </summary>
        private void ExecuteProgram(string executable)
        {
            try
            {
                string fileName = Path.GetFileName(executable).ToLower();

                // setup.exe는 PowerShell로 자동 설치 실행
                if (fileName == "setup.exe")
                {
                    ExecuteSetupWithPowerShell(executable);
                    return;
                }

                // 일반 프로그램 실행
                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                };

                // 작업 디렉토리는 실행 파일의 디렉토리로 자동 설정
                string workingDir = Path.GetDirectoryName(executable) ?? "";
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                Process.Start(startInfo);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 1년 이상 된 로그 파일 삭제
        /// </summary>
        public static void CleanupOldLogFiles()
        {
            try
            {
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string logDir = Path.Combine(programDataPath, "AppLauncher", "Logs");

                if (!Directory.Exists(logDir))
                {
                    return;
                }

                // 1년 전 날짜 계산
                DateTime oneYearAgo = DateTime.Now.AddYears(-1);

                // 로그 파일 검색
                var logFiles = Directory.GetFiles(logDir, "install_log_*.txt");
                int deletedCount = 0;

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFile);

                        // 파일 생성일이 1년 이상 지났으면 삭제
                        if (fileInfo.CreationTime < oneYearAgo)
                        {
                            File.Delete(logFile);
                            deletedCount++;
                            Console.WriteLine($"[CLEANUP] Deleted old log file: {Path.GetFileName(logFile)} (Created: {fileInfo.CreationTime:yyyy-MM-dd})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CLEANUP] Failed to delete log file {Path.GetFileName(logFile)}: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    Console.WriteLine($"[CLEANUP] Deleted {deletedCount} old log file(s)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLEANUP] Failed to cleanup old log files: {ex.Message}");
            }
        }

        /// <summary>
        /// 특정 부모 프로세스의 자식 프로세스 찾기 (WMI 사용)
        /// </summary>
        private int[] GetChildProcessIds(int parentProcessId)
        {
            try
            {
                var childPids = new System.Collections.Generic.List<int>();
                using (var searcher = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parentProcessId}"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        int childPid = Convert.ToInt32(obj["ProcessId"]);
                        childPids.Add(childPid);
                    }
                }
                return childPids.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CHILD_PROCESS] Failed to get child processes for PID {parentProcessId}: {ex.Message}");
                return new int[0];
            }
        }

        /// <summary>
        /// 특정 프로세스의 모든 자손 프로세스 찾기 (재귀적으로)
        /// </summary>
        private int[] GetAllDescendantProcessIds(int parentProcessId)
        {
            var descendants = new System.Collections.Generic.List<int>();
            var directChildren = GetChildProcessIds(parentProcessId);

            foreach (int childPid in directChildren)
            {
                descendants.Add(childPid);
                // 재귀적으로 손자, 증손자... 프로세스도 찾기
                descendants.AddRange(GetAllDescendantProcessIds(childPid));
            }

            return descendants.ToArray();
        }

        /// <summary>
        /// fonts_install.exe 프로세스 강제 종료
        /// </summary>
        /// <param name="parentProcessId">부모 프로세스 ID (지정하면 해당 프로세스의 자식만 종료)</param>
        private void KillFontsInstallProcess(int? parentProcessId = null)
        {
            try
            {
                // "fonts_install" 이름의 모든 프로세스 찾기
                var processes = Process.GetProcessesByName("fonts_install");

                if (processes.Length == 0)
                {
                    // 프로세스가 없으면 아무것도 하지 않음 (정상)
                    return;
                }

                // 부모 프로세스가 지정된 경우, 해당 부모의 자손 프로세스만 필터링
                int[]? targetPids = null;
                if (parentProcessId.HasValue)
                {
                    targetPids = GetAllDescendantProcessIds(parentProcessId.Value);
                    if (targetPids.Length > 0)
                    {
                        Console.WriteLine($"[KILL_PROCESS] Found {targetPids.Length} descendant process(es) of PID {parentProcessId.Value}: {string.Join(", ", targetPids)}");
                    }
                }

                foreach (var process in processes)
                {
                    try
                    {
                        // 프로세스가 이미 종료되었는지 확인
                        if (process.HasExited)
                        {
                            Console.WriteLine($"[KILL_PROCESS] Process already exited (PID: {process.Id})");
                            continue;
                        }

                        // 부모 프로세스가 지정된 경우, 해당 부모의 자손인지 확인
                        if (parentProcessId.HasValue && targetPids != null && !targetPids.Contains(process.Id))
                        {
                            Console.WriteLine($"[KILL_PROCESS] Skipping fonts_install (PID: {process.Id}) - not a descendant of PID {parentProcessId.Value}");
                            continue;
                        }

                        Console.WriteLine($"[KILL_PROCESS] Killing fonts_install process (PID: {process.Id})");
                        process.Kill();

                        // 프로세스가 종료될 때까지 대기 (최대 3초)
                        bool exited = process.WaitForExit(3000);

                        if (exited)
                        {
                            Console.WriteLine($"[KILL_PROCESS] Process killed successfully (PID: {process.Id})");
                        }
                        else
                        {
                            Console.WriteLine($"[KILL_PROCESS] Process did not exit within timeout (PID: {process.Id})");
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        // 프로세스 접근 권한 없음 또는 프로세스를 찾을 수 없음
                        Console.WriteLine($"[KILL_PROCESS] Access denied or process not found (PID: {process.Id}): {ex.Message}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        // 프로세스가 이미 종료됨
                        Console.WriteLine($"[KILL_PROCESS] Process already terminated (PID: {process.Id}): {ex.Message}");
                    }
                    catch (NotSupportedException ex)
                    {
                        // 원격 프로세스이거나 지원되지 않음
                        Console.WriteLine($"[KILL_PROCESS] Operation not supported (PID: {process.Id}): {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        // 기타 예외
                        Console.WriteLine($"[KILL_PROCESS] Unexpected error killing process (PID: {process.Id}): {ex.GetType().Name} - {ex.Message}");
                    }
                    finally
                    {
                        try
                        {
                            process.Dispose();
                        }
                        catch
                        {
                            // Dispose 실패 무시
                        }
                    }
                }

                Console.WriteLine($"[KILL_PROCESS] Processed {processes.Length} fonts_install process(es)");
            }
            catch (Exception ex)
            {
                // GetProcessesByName이 실패하는 경우 (매우 드묾)
                Console.WriteLine($"[KILL_PROCESS] Failed to enumerate processes: {ex.GetType().Name} - {ex.Message}");
            }
        }

        /// <summary>
        /// setup.exe를 PowerShell로 자동 설치 실행
        /// </summary>
        private void ExecuteSetupWithPowerShell(string setupExePath)
        {
            try
            {
                // Setup 실행 전 fonts_install 프로세스 강제 종료
                Console.WriteLine("[PRE-INSTALL] Checking for fonts_install processes...");
                KillFontsInstallProcess();

                Console.WriteLine("=== LabView Installation Start ===");
                Console.WriteLine($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Setup Path: {setupExePath}");

                // 로그 파일 경로 생성
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string logDir = Path.Combine(programDataPath, "AppLauncher", "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logFilePath = Path.Combine(logDir, $"install_log_{DateTime.Now:yyyyMMddHHmmss}.txt");
                Console.WriteLine($"Log file path: {logFilePath}");

                // PowerShell 명령어 구성
                // ArgumentList 옵션:
                //   '/q'              : Quiet mode (UI 없이 자동 설치)
                //   '/AcceptLicenses' : 라이선스 자동 동의
                //   'yes'             : AcceptLicenses 값
                //   '/log'            : 설치 로그 기록
                //   '{logFilePath}'   : 로그 파일 저장 경로
                // Start-Process 옵션:
                //   -Verb RunAs       : 관리자 권한으로 실행
                //   -PassThru         : 프로세스 객체 반환 (exit code 확인용)
                //   -Wait             : 설치 완료까지 대기
                string psCommand = $@"
# Setup.exe 실행
$proc = Start-Process '{setupExePath}' -ArgumentList '/q','/AcceptLicenses','yes','/log','{logFilePath}' -Verb RunAs -PassThru

# Setup.exe의 PID 출력
Write-Output ""SetupPID:$($proc.Id)""

# 설치 완료까지 대기
$proc.WaitForExit()

# Exit code 확인
$exitCode = $proc.ExitCode
Write-Output ""ExitCode:$exitCode""

# Cleanup 대기 시간 20초
Write-Output ""WaitingCleanup""
Start-Sleep -Seconds 20
Write-Output ""CleanupComplete""
";

                var bytes = System.Text.Encoding.Unicode.GetBytes(psCommand);
                var encodedCommand = Convert.ToBase64String(bytes);



                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{psCommand}\"",
                    // Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encodedCommand}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(setupExePath)
                };


                // 설치 전 setting.ini 파일 백업
                var backupSettingFile = BackupSettingFile();

                _sendStatusResponse?.Invoke("installation_start", "설치 시작");
                var psProcess = Process.Start(startInfo);
                if (psProcess != null)
                {
                    Console.WriteLine($"PowerShell process started (PID: {psProcess.Id})");

                    // ⭐ setup.exe의 PID를 저장할 변수
                    int? setupPid = null;
                    var setupPidLock = new object();

                    // ⭐ fonts_install 프로세스 모니터링 및 자동 종료 Task
                    var monitorCancellation = new System.Threading.CancellationTokenSource();
                    var monitorTask = Task.Run(async () =>
                    {
                        while (!monitorCancellation.Token.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(2000, monitorCancellation.Token); // 2초마다 체크

                                // setup.exe의 PID를 알고 있으면 그 자식만 종료, 아니면 모든 fonts_install 종료
                                int? currentSetupPid;
                                lock (setupPidLock)
                                {
                                    currentSetupPid = setupPid;
                                }

                                if (currentSetupPid.HasValue)
                                {
                                    Console.WriteLine($"[MONITOR] Checking fonts_install processes (setup.exe PID: {currentSetupPid.Value})");
                                    KillFontsInstallProcess(currentSetupPid.Value);
                                }
                                else
                                {
                                    KillFontsInstallProcess();
                                }
                            }
                            catch (TaskCanceledException)
                            {
                                break;
                            }
                        }
                        Console.WriteLine("[MONITOR] fonts_install monitoring stopped");
                    }, monitorCancellation.Token);

                    // ⭐ 실시간 출력 읽기 (setup.exe PID 추출 포함)
                    string output = "";
                    string error = "";

                    // 비동기로 출력 읽기
                    Task outputTask = Task.Run(() =>
                    {
                        var sb = new System.Text.StringBuilder();
                        while (!psProcess.StandardOutput.EndOfStream)
                        {
                            string? line = psProcess.StandardOutput.ReadLine();
                            if (line != null)
                            {
                                sb.AppendLine(line);

                                // setup.exe의 PID 추출
                                if (line.StartsWith("SetupPID:"))
                                {
                                    if (int.TryParse(line.Substring("SetupPID:".Length), out int pid))
                                    {
                                        lock (setupPidLock)
                                        {
                                            setupPid = pid;
                                        }
                                        Console.WriteLine($"[SETUP] Setup.exe PID detected: {pid}");
                                    }
                                }
                            }
                        }
                        output = sb.ToString();
                    });
                    Task errorTask = Task.Run(() => error = psProcess.StandardError.ReadToEnd());

                    // ⭐ 타임아웃 설정 (25분)
                    int timeoutMinutes = 25;
                    bool completed = psProcess.WaitForExit(timeoutMinutes * 60 * 1000);

                    Task.WaitAll(outputTask, errorTask);

                    // ⭐ 모니터링 Task 종료
                    monitorCancellation.Cancel();
                    try
                    {
                        monitorTask.Wait(5000); // 최대 5초 대기
                    }
                    catch { }

                    if (!completed)
                    {
                        Console.WriteLine($"[TIMEOUT] Installation timeout after {timeoutMinutes} minutes");

                        try
                        {
                            psProcess.Kill();
                        }
                        catch { }

                        return;
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"PowerShell Error:\n{error}");
                    }

                    // ⭐ 출력 파싱
                    Console.WriteLine($"PowerShell Output:\n{output}");

                    // Exit code 추출
                    int exitCode = 0;
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("ExitCode:"))
                        {
                            int.TryParse(line.Substring("ExitCode:".Length), out exitCode);
                            Console.WriteLine($"Parsed Exit Code: {exitCode}");
                        }
                        else if (line == "WaitingCleanup")
                        {
                            Console.WriteLine("Waiting for cleanup (20 seconds)...");
                        }
                        else if (line == "CleanupComplete")
                        {
                            Console.WriteLine("Cleanup wait completed");
                        }
                    }

                    Console.WriteLine("=== LabView Installation DONE ===");

                    // 설치 완료 후 fonts_install 프로세스 최종 정리
                    Console.WriteLine("[POST-INSTALL] Final cleanup of fonts_install processes...");
                    int? finalSetupPid;
                    lock (setupPidLock)
                    {
                        finalSetupPid = setupPid;
                    }
                    if (finalSetupPid.HasValue)
                    {
                        Console.WriteLine($"[POST-INSTALL] Cleaning up with setup.exe PID: {finalSetupPid.Value}");
                        KillFontsInstallProcess(finalSetupPid.Value);
                    }
                    else
                    {
                        KillFontsInstallProcess();
                    }

                    if (exitCode != 0)
                    {
                        Console.WriteLine($"[FAILED] Installation failed (Exit Code: {exitCode})");
                        Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        return;

                    }
                    else
                    {
                        _sendStatusResponse?.Invoke("installation_complete", "설치 완료");
                        Console.WriteLine($"[SUCCESS] Installation completed");
                        Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        RestoreSettingFile(backupSettingFile);

                    }

                }
                else
                {
                    Console.WriteLine("[ERROR] Failed to start PowerShell process");
                    Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    return;
                }

                // 버전 정보 저장
                if (!string.IsNullOrEmpty(_command.Version) && !string.IsNullOrEmpty(_config.LocalVersionFile))
                {
                    try
                    {
                        string? versionFileDir = Path.GetDirectoryName(_config.LocalVersionFile);
                        if (!string.IsNullOrEmpty(versionFileDir) && !Directory.Exists(versionFileDir))
                        {
                            Directory.CreateDirectory(versionFileDir);
                        }
                        File.WriteAllText(_config.LocalVersionFile, _command.Version);
                        Console.WriteLine($"Version file saved: {_command.Version}");
                        _sendStatusResponse?.Invoke("version_saved", $"버전 파일 저장: {_command.Version}");
                    }
                    catch (Exception versionEx)
                    {
                        Console.WriteLine($"Failed to save version file: {versionEx.Message}");
                        _sendStatusResponse?.Invoke("version_save_error", versionEx.Message);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] {ex.Message}");
                Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
                Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                _sendStatusResponse?.Invoke("error", ex.Message);
            }
        }

        /// <summary>
        /// 실행 파일의 메타데이터 검증
        /// </summary>
        private bool ValidateExecutableMetadata(string exePath, string expectedProductName, string expectedCompanyName)
        {
            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exePath);

                Console.WriteLine("=== Executable 메타데이터 검증 ===");
                Console.WriteLine($"[METADATA] 파일 경로: {exePath}");
                Console.WriteLine($"[METADATA] ProductName: {versionInfo.ProductName}");
                Console.WriteLine($"[METADATA] CompanyName: {versionInfo.CompanyName}");
                Console.WriteLine($"[METADATA] FileDescription: {versionInfo.FileDescription}");
                Console.WriteLine($"[METADATA] FileVersion: {versionInfo.FileVersion}");
                Console.WriteLine($"[METADATA] ProductVersion: {versionInfo.ProductVersion}");
                Console.WriteLine($"[METADATA] InternalName: {versionInfo.InternalName}");
                Console.WriteLine($"[METADATA] OriginalFilename: {versionInfo.OriginalFilename}");
                Console.WriteLine($"[METADATA] LegalCopyright: {versionInfo.LegalCopyright}");
                Console.WriteLine("============================");

                // ProductName과 CompanyName 검증
                bool productNameMatch = string.Equals(versionInfo.ProductName, expectedProductName, StringComparison.OrdinalIgnoreCase);
                bool companyNameMatch = string.Equals(versionInfo.CompanyName, expectedCompanyName, StringComparison.OrdinalIgnoreCase);

                Console.WriteLine($"[VALIDATION] Expected ProductName: {expectedProductName}, Actual: {versionInfo.ProductName} -> {(productNameMatch ? "PASS" : "FAIL")}");
                Console.WriteLine($"[VALIDATION] Expected CompanyName: {expectedCompanyName}, Actual: {versionInfo.CompanyName} -> {(companyNameMatch ? "PASS" : "FAIL")}");

                return productNameMatch && companyNameMatch;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[METADATA] 메타데이터 읽기 실패: {ex.Message}");
                return false;
            }
        }
    }
}
