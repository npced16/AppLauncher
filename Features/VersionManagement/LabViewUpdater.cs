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
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using AppLauncher.Shared.Services;

namespace AppLauncher.Features.VersionManagement
{
    public class LabViewUpdater
    {
        private static void Log(string message) => DebugLogger.Log("LabViewUpdate", message);

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
                Log("[SCHEDULE] Scheduling update for next launcher restart");

                // 업데이트 정보를 JSON에 저장 (LaunchCommand 직접 저장)
                try
                {
                    bool saved = PendingUpdateManager.SavePendingUpdate(_command);
                    if (!saved)
                    {
                        Log("[SCHEDULE] Failed to save pending update");
                        _sendStatusResponse?.Invoke("error", "업데이트 예약 저장 실패");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[SCHEDULE] Exception saving pending update: {ex.Message}");
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
                    Log("[SCHEDULE] Will be applied on next restart.");
                }
            }
            catch (Exception ex)
            {
                Log($"[SCHEDULE] Failed to schedule update: {ex.Message}");
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
                Log("[RESTART] Restarting launcher...");

                string exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (!string.IsNullOrEmpty(exePath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                    };

                    Process.Start(startInfo);
                    Log("[RESTART] New launcher process started");

                    // 현재 프로세스 종료
                    //NOTE - 프로세스가 시작될떄 종료되긴하나 명시적으로 종료처리
                    Environment.Exit(0);
                }
                else
                {
                    Log("[RESTART] Failed to get launcher path");
                }
            }
            catch (Exception ex)
            {
                Log($"[RESTART] Failed to restart launcher: {ex.Message}");
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
                    Log($"[BACKUP] setting.ini not found: {settingFilePath}");
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

                Log($"[BACKUP] Setting file backed up: {backupFilePath}");

                return backupFilePath;
            }
            catch (Exception ex)
            {
                Log($"[BACKUP] Failed to backup setting file: {ex.Message}");
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
                    Log($"[RESTORE] No backup file to restore");
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

                Log($"[RESTORE] Setting file restored: {settingFilePath}");

                _sendStatusResponse?.Invoke("restore_done", "설정 파일 복원 완료");

                // 백업 파일 삭제 (선택사항)
                // File.Delete(backupFilePath);
            }
            catch (Exception ex)
            {
                Log($"[RESTORE] Failed to restore setting file: {ex.Message}");
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
                    Log("[LabViewUpdater] URL이 비어있습니다.");
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
                    await ExecuteProgram(downloadPath);
                }

                return downloadPath;
            }
            catch (Exception ex)
            {
                Log($"[LabViewUpdater] 다운로드 및 실행 오류: {ex.Message}");
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

                Log($"[ZIP] Extracting to: {extractDir}");

                // 기존 Volume 폴더가 있으면 삭제
                string volumeDir = Path.Combine(extractDir, "Volume");
                if (Directory.Exists(volumeDir))
                {
                    Log($"[ZIP] Deleting existing Volume folder: {volumeDir}");
                    Directory.Delete(volumeDir, recursive: true);
                }

                // 압축 해제 (비동기)
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, extractDir, overwriteFiles: true);
                });

                Log($"[ZIP] Extraction completed: {extractDir}");


                string HBOTOperatorPath = Path.Combine(extractDir, "HBOT Operator.exe");
                if (File.Exists(HBOTOperatorPath))
                {
                    Log($"[ZIP] Found HBOT Operator.exe: {HBOTOperatorPath}");

                    // 메타데이터 검증
                    if (!ValidateExecutableMetadata(HBOTOperatorPath, "HBOT Operator", "Ibex Medical Systems"))
                    {
                        Log($"[ZIP] HBOT Operator.exe validation failed - executing file directly");
                        _sendStatusResponse?.Invoke("validation_failed", "파일 검증 실패 - 파일 실행");

                        // 검증 실패 시 HBOT Operator.exe 실행
                        var targetExe = _config.TargetExecutable;
                        if (!string.IsNullOrEmpty(targetExe) && File.Exists(targetExe))
                        {
                            await ExecuteProgram(targetExe);
                        }

                        return;
                    }
                    Log($"[ZIP] HBOT Operator.exe validation successful - proceeding with setup.exe");
                }
                else
                {
                    Log($"[ZIP] HBOT Operator.exe not found in {extractDir}");
                    return;
                }

                // Volume 폴더 안의 setup.exe 찾기
                string setupExePath = Path.Combine(volumeDir, "setup.exe");

                if (File.Exists(setupExePath))
                {
                    Log($"[ZIP] Found setup.exe in Volume folder: {setupExePath}");

                    // setup.exe 메타데이터 로그 출력
                    // LogExecutableMetadata(setupExePath);
                    // PowerShell로 setup.exe 실행
                    await ExecuteSetupWithPowerShell(setupExePath);
                }
                else
                {
                    Log($"[ZIP] setup.exe not found in {volumeDir}");
                }
            }
            catch (Exception ex)
            {
                Log($"[ZIP] Extraction error: {ex.Message}");
            }
        }

        /// <summary>
        /// 프로그램 실행
        /// </summary>
        private async Task ExecuteProgram(string executable)
        {
            try
            {
                string fileName = Path.GetFileName(executable).ToLower();

                // setup.exe는 PowerShell로 자동 설치 실행
                if (fileName == "setup.exe")
                {
                    await ExecuteSetupWithPowerShell(executable);
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
                            Log($"[CLEANUP] Deleted old log file: {Path.GetFileName(logFile)} (Created: {fileInfo.CreationTime:yyyy-MM-dd})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[CLEANUP] Failed to delete log file {Path.GetFileName(logFile)}: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    Log($"[CLEANUP] Deleted {deletedCount} old log file(s)");
                }
            }
            catch (Exception ex)
            {
                Log($"[CLEANUP] Failed to cleanup old log files: {ex.Message}");
            }
        }


        /// <summary>
        /// setup.exe를 PowerShell로 자동 설치 실행
        /// </summary>
        private async Task ExecuteSetupWithPowerShell(string setupExePath)
        {
            try
            {
                // Setup 실행 전 fonts_install 프로세스 강제 종료
                Log("=== LabView Installation Start ===");
                Log($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Log($"Setup Path: {setupExePath}");

                // 로그 파일 경로 생성
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string logDir = Path.Combine(programDataPath, "AppLauncher", "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string logFilePath = Path.Combine(logDir, $"install_log_{DateTime.Now:yyyyMMddHHmmss}.txt");
                Log($"Log file path: {logFilePath}");

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
                $proc = Start-Process '{setupExePath}' -ArgumentList '/qb!','/AcceptLicenses','yes','/log','{logFilePath}' -Verb RunAs -PassThru
                $proc.PriorityClass = 'High'

                Write-Output ""SetupPID:$($proc.Id)""

                $proc.WaitForExit()

                $exitCode = $proc.ExitCode
                Write-Output ""ExitCode:$exitCode""
                ";

                // # Setup.exe 실행
                // $proc = Start - Process '{setupExePath}' - ArgumentList '/q','/AcceptLicenses','yes','/log','{logFilePath}' - Verb RunAs - PassThru

                // # Exit code 확인
                // $exitCode = $proc.ExitCode
                // Write - Output ""ExitCode:$exitCode""

                // # Cleanup 대기 시간 20초
                // Write - Output ""WaitingCleanup""
                // Start - Sleep - Seconds 20
                // Write - Output ""CleanupComplete""

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{psCommand}\"",
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
                    Log($"PowerShell process started (PID: {psProcess.Id})");

                    //  setup.exe의 PID를 저장할 변수
                    int? setupPid = null;
                    var setupPidLock = new object();

                    // fonts_install 프로세스 모니터링 및 자동 종료 (비동기)
                    var monitorCancellation = new System.Threading.CancellationTokenSource();
                    var monitorTask = Task.Run(async () =>
                    {
                        while (!monitorCancellation.Token.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(2000, monitorCancellation.Token); // 2초마다 체크

                                // setup.exe의 PID를 알고 있으면 그 자식만 종료
                                int? currentSetupPid;
                                lock (setupPidLock)
                                {
                                    currentSetupPid = setupPid;
                                }

                                FontInstallMonitor.KillFontsInstallProcess(currentSetupPid);
                            }
                            catch (TaskCanceledException)
                            {
                                break;
                            }
                        }
                        Log("[MONITOR] fonts_install monitoring stopped");
                    }, monitorCancellation.Token);

                    // 실시간 출력 읽기
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

                                // setup.exe의 PID 추출 및 저장
                                if (line.StartsWith("SetupPID:"))
                                {
                                    if (int.TryParse(line.Substring("SetupPID:".Length), out int pid))
                                    {
                                        lock (setupPidLock)
                                        {
                                            setupPid = pid;
                                        }
                                        Log($"[SETUP] Setup.exe PID detected: {pid}");
                                    }
                                }
                            }
                        }
                        output = sb.ToString();
                    });

                    Task errorTask = Task.Run(() => error = psProcess.StandardError.ReadToEnd());

                    // 타임아웃 설정 (3시간 MAX)
                    int timeoutMinutes = 180;

                    // 비동기로 대기 (UI 스레드 블로킹 방지)
                    Task processTask = Task.Run(() => psProcess.WaitForExit());
                    Task allTasks = Task.WhenAll(processTask, outputTask, errorTask);

                    bool completed = await Task.WhenAny(allTasks, Task.Delay(timeoutMinutes * 60 * 1000)) == allTasks;

                    // 모니터링 Task 종료
                    monitorCancellation.Cancel();
                    try
                    {
                        await Task.WhenAny(monitorTask, Task.Delay(5000)); // 최대 5초 대기
                    }
                    catch { }

                    if (!completed)
                    {
                        Log($"[TIMEOUT] Installation timeout after {timeoutMinutes} minutes");

                        try
                        {
                            psProcess.Kill();
                        }
                        catch { }

                        return;
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        Log($"PowerShell Error:\n{error}");
                    }

                    // 출력 파싱
                    Log($"PowerShell Output:\n{output}");

                    // Exit code 추출
                    int exitCode = 0;
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("ExitCode:"))
                        {
                            int.TryParse(line.Substring("ExitCode:".Length), out exitCode);
                            Log($"Parsed Exit Code: {exitCode}");
                            break;
                        }
                    }

                    Log("=== LabView Installation DONE ===");

                    // Exit Code 확인: 0 (성공) 또는 3010 (성공 - 재부팅 필요)
                    if (exitCode != 0 && exitCode != 3010)
                    {
                        Log($"[FAILED] Installation failed (Exit Code: {exitCode})");
                        Log($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        return;
                    }
                    else
                    {
                        _sendStatusResponse?.Invoke("installation_complete", "설치 완료");
                        Log($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        RestoreSettingFile(backupSettingFile);

                        // 버전 정보 저장 (설치 성공 시에만)
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
                                Log($"Version file saved: {_command.Version}");
                                _sendStatusResponse?.Invoke("version_saved", $"버전 파일 저장: {_command.Version}");
                            }
                            catch (Exception versionEx)
                            {
                                Log($"Failed to save version file: {versionEx.Message}");
                                _sendStatusResponse?.Invoke("version_save_error", versionEx.Message);
                            }
                        }
                    }
                }
                else
                {
                    Log("[ERROR] Failed to start PowerShell process");
                    Log($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    return;
                }

            }
            catch (Exception ex)
            {
                Log($"[EXCEPTION] {ex.Message}");
                Log($"Stack Trace:\n{ex.StackTrace}");
                Log($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

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

                Log("=== Executable 메타데이터 검증 ===");
                Log($"[METADATA] 파일 경로: {exePath}");
                Log($"[METADATA] ProductName: {versionInfo.ProductName}");
                Log($"[METADATA] CompanyName: {versionInfo.CompanyName}");
                Log($"[METADATA] FileDescription: {versionInfo.FileDescription}");
                Log($"[METADATA] FileVersion: {versionInfo.FileVersion}");
                Log($"[METADATA] ProductVersion: {versionInfo.ProductVersion}");
                Log($"[METADATA] InternalName: {versionInfo.InternalName}");
                Log($"[METADATA] OriginalFilename: {versionInfo.OriginalFilename}");
                Log($"[METADATA] LegalCopyright: {versionInfo.LegalCopyright}");
                Log("============================");

                // ProductName과 CompanyName 검증
                bool productNameMatch = string.Equals(versionInfo.ProductName, expectedProductName, StringComparison.OrdinalIgnoreCase);
                bool companyNameMatch = string.Equals(versionInfo.CompanyName, expectedCompanyName, StringComparison.OrdinalIgnoreCase);

                Log($"[VALIDATION] Expected ProductName: {expectedProductName}, Actual: {versionInfo.ProductName} -> {(productNameMatch ? "PASS" : "FAIL")}");
                Log($"[VALIDATION] Expected CompanyName: {expectedCompanyName}, Actual: {versionInfo.CompanyName} -> {(companyNameMatch ? "PASS" : "FAIL")}");

                return productNameMatch && companyNameMatch;
            }
            catch (Exception ex)
            {
                Log($"[METADATA] 메타데이터 읽기 실패: {ex.Message}");
                return false;
            }
        }
    }
}
