using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
        private readonly Action<string> _statusCallback;
        private readonly Action<string>? _installStatusCallback;
        private readonly Action<string, string> _sendStatusResponse;

        public LabViewUpdater(
            LaunchCommand command,
            LauncherConfig config,
            Action<string> statusCallback,
            Action<string>? installStatusCallback,
            Action<string, string> sendStatusResponse
        )
        {
            _command = command;
            _config = config;
            _statusCallback = statusCallback;
            _installStatusCallback = installStatusCallback;
            _sendStatusResponse = sendStatusResponse;
        }

        private void SendStatusResponse(string status, string message)
        {
            _sendStatusResponse(status, message);
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
                    _statusCallback("setting.ini 파일이 없어 백업을 건너뜁니다.");
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
                _statusCallback($"설정 파일 백업 완료: {backupFilePath}");
                SendStatusResponse("setting_backup", backupFilePath);

                return backupFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BACKUP] Failed to backup setting file: {ex.Message}");
                _statusCallback($"설정 파일 백업 실패: {ex.Message}");
                SendStatusResponse("backup_error", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// setting.ini 파일 복원
        /// </summary>
        private void RestoreSettingFile(string? backupFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(backupFilePath) || !File.Exists(backupFilePath))
                {
                    Console.WriteLine($"[RESTORE] No backup file to restore");
                    _statusCallback("복원할 백업 파일이 없습니다.");
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
                _statusCallback($"설정 파일 복원 완료: {settingFilePath}");
                SendStatusResponse("setting_restore", settingFilePath);

                // 백업 파일 삭제 (선택사항)
                // File.Delete(backupFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RESTORE] Failed to restore setting file: {ex.Message}");
                _statusCallback($"설정 파일 복원 실패: {ex.Message}");
                SendStatusResponse("restore_error", ex.Message);
            }
        }



        /// <summary>
        /// 파일 다운로드 및 실행
        /// </summary>
        public async Task<string> DownloadAndExecuteAsync()
        {
            try
            {
                _statusCallback("파일 다운로드 중...");
                _installStatusCallback?.Invoke("다운로드 중");

                // 다운로드 디렉토리 생성 (C:\ProgramData\AppLauncher\Downloads)
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string tempDir = Path.Combine(programDataPath, "AppLauncher", "Downloads");
                if (!Directory.Exists(tempDir))
                    Directory.CreateDirectory(tempDir);

                // 파일명 추출
                string fileName = Path.GetFileName(new Uri(_command.URL).LocalPath);
                bool isZipFile = fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                bool isExeFile = fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

                if (!isZipFile && !isExeFile)
                {
                    // 기본값: exe로 간주
                    fileName = $"download_{DateTime.Now:yyyyMMddHHmmss}.exe";
                }

                string downloadPath = Path.Combine(tempDir, fileName);

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
                }

                if (!File.Exists(downloadPath))
                {
                    _statusCallback("다운로드 실패");
                    SendStatusResponse("error", "Download failed");
                    _installStatusCallback?.Invoke("대기 중");
                    return "";
                }

                _statusCallback($"다운로드 완료: {Path.GetFileName(downloadPath)}");

                // zip 파일이면 압축 해제
                if (isZipFile)
                {
                    await ExtractZipFileAsync(downloadPath);
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
                _statusCallback($"다운로드 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
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
                _statusCallback("압축 해제 중...");
                _installStatusCallback?.Invoke("압축 해제 중");
                SendStatusResponse("extracting", zipFilePath);


                // 압축 해제 대상 디렉토리 결정
                string extractDir;

                // config에 WorkingDirectory가 설정되어 있으면 사용
                if (!string.IsNullOrEmpty(_config.WorkingDirectory) && Directory.Exists(_config.WorkingDirectory))
                {
                    extractDir = _config.WorkingDirectory;
                }
                else
                {
                    // 없으면 zip 파일과 같은 폴더에 바로 압축 해제
                    extractDir = Path.GetDirectoryName(zipFilePath) ?? Path.GetTempPath();
                }
                // 압축 해제 디렉토리가 없으면 생성
                if (!Directory.Exists(extractDir))
                {
                    Directory.CreateDirectory(extractDir);
                }

                _statusCallback($"압축 해제 위치: {extractDir}");
                Console.WriteLine($"[ZIP] Extracting to: {extractDir}");

                // 기존 Volume 폴더가 있으면 삭제
                string volumeDir = Path.Combine(extractDir, "Volume");
                if (Directory.Exists(volumeDir))
                {
                    _statusCallback("기존 Volume 폴더 삭제 중...");
                    Console.WriteLine($"[ZIP] Deleting existing Volume folder: {volumeDir}");
                    Directory.Delete(volumeDir, recursive: true);
                }

                // 압축 해제 (비동기)
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, extractDir, overwriteFiles: true);
                });

                _statusCallback($"압축 해제 완료: {extractDir}");
                Console.WriteLine($"[ZIP] Extraction completed: {extractDir}");
                SendStatusResponse("extract_success", extractDir);

                // Volume 폴더 안의 setup.exe 찾기
                string setupExePath = Path.Combine(volumeDir, "setup.exe");

                if (File.Exists(setupExePath))
                {
                    _statusCallback($"setup.exe 발견: {setupExePath}");
                    Console.WriteLine($"[ZIP] Found setup.exe in Volume folder: {setupExePath}");

                    // PowerShell로 setup.exe 실행
                    ExecuteSetupWithPowerShell(setupExePath);
                }
                else
                {
                    _statusCallback("압축 해제 완료. Volume 폴더에서 setup.exe를 찾을 수 없어 자동 실행하지 않습니다.");
                    Console.WriteLine($"[ZIP] setup.exe not found in {volumeDir}");
                    SendStatusResponse("extract_complete_no_setup", volumeDir);
                    _installStatusCallback?.Invoke("대기 중");
                }
            }
            catch (Exception ex)
            {
                _statusCallback($"압축 해제 오류: {ex.Message}");
                Console.WriteLine($"[ZIP] Extraction error: {ex.Message}");
                SendStatusResponse("extract_error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
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
                    UseShellExecute = true
                };
                // 작업 디렉토리 자동 설정: config > executable이 있는 디렉토리
                string workingDir = _config.WorkingDirectory ?? "";

                // workingDir이 없으면 실행 파일이 있는 디렉토리를 자동으로 사용
                if (string.IsNullOrEmpty(workingDir))
                {
                    workingDir = Path.GetDirectoryName(executable) ?? "";
                }

                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                Process.Start(startInfo);

                _statusCallback($"프로그램 실행: {Path.GetFileName(executable)}");

                // 상태 응답 전송
                SendStatusResponse("launched", executable);

                // 일반 프로그램 실행 후 대기 중으로 복귀
                _installStatusCallback?.Invoke("대기 중");
            }
            catch (Exception ex)
            {
                _statusCallback($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
            }
        }

        /// <summary>
        /// setup.exe를 PowerShell로 자동 설치 실행
        /// </summary>
        private void ExecuteSetupWithPowerShell(string setupExePath)
        {
            try
            {

                Console.WriteLine("=== AppLauncher Installation Start ===");
                Console.WriteLine($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Setup Path: {setupExePath}");

                _statusCallback("LabView 설치 시작...");
                _installStatusCallback?.Invoke("설치 중");



                SendStatusResponse("install_start", $"Setup.exe 실행 시작: {setupExePath}");

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
$proc = Start-Process '{setupExePath}' -ArgumentList '/q','/AcceptLicenses','yes','/log','{logFilePath}' -Verb RunAs -PassThru -Wait

# Exit code 확인
$exitCode = $proc.ExitCode
Write-Output ""ExitCode:$exitCode""

# Cleanup 대기 시간 20초
Write-Output ""WaitingCleanup""
Start-Sleep -Seconds 20
Write-Output ""CleanupComplete""

# PID 출력
Write-Output $proc.Id
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


                var psProcess = Process.Start(startInfo);
                if (psProcess != null)
                {
                    Console.WriteLine($"PowerShell process started (PID: {psProcess.Id})");

                    // ⭐ 실시간 출력 읽기
                    string output = "";
                    string error = "";

                    // 비동기로 출력 읽기
                    Task outputTask = Task.Run(() => output = psProcess.StandardOutput.ReadToEnd());
                    Task errorTask = Task.Run(() => error = psProcess.StandardError.ReadToEnd());

                    // ⭐ 타임아웃 설정 (25분)
                    int timeoutMinutes = 25;
                    bool completed = psProcess.WaitForExit(timeoutMinutes * 60 * 1000);

                    Task.WaitAll(outputTask, errorTask);

                    if (!completed)
                    {
                        Console.WriteLine($"[TIMEOUT] Installation timeout after {timeoutMinutes} minutes");
                        _statusCallback($"설치 타임아웃 ({timeoutMinutes}분 초과)");
                        SendStatusResponse("install_timeout", $"Timeout after {timeoutMinutes} minutes");

                        try
                        {
                            psProcess.Kill();
                        }
                        catch { }

                        _installStatusCallback?.Invoke("대기 중");
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
                            _statusCallback("설치 완료, 정리 중 (20초 대기)...");
                        }
                        else if (line == "CleanupComplete")
                        {
                            Console.WriteLine("Cleanup wait completed");
                            _statusCallback("정리 완료");
                        }
                    }

                    Console.WriteLine("=== AppLauncher Installation DONE ===");
                    if (exitCode != 0)
                    {
                        Console.WriteLine($"[FAILED] Installation failed (Exit Code: {exitCode})");
                        Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        _statusCallback($"설치 실패 (종료 코드: {exitCode})");
                        SendStatusResponse("install_failed", $"Exit code: {exitCode}");
                        _installStatusCallback?.Invoke("대기 중");
                        return;

                    }
                    else
                    {

                        Console.WriteLine($"[SUCCESS] Installation completed");
                        Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        _statusCallback($"설치 완료 (종료 코드: {exitCode})");
                        SendStatusResponse("install_success", $"Exit code: {exitCode}");
                        RestoreSettingFile(backupSettingFile);

                    }

                }
                else
                {
                    Console.WriteLine("[ERROR] Failed to start PowerShell process");
                    Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    _statusCallback("PowerShell 프로세스 시작 실패");
                    SendStatusResponse("error", "Failed to start PowerShell process");
                    _installStatusCallback?.Invoke("대기 중");
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
                        _statusCallback($"버전 {_command.Version} 저장 완료");
                        SendStatusResponse("version_saved", $"버전 파일 저장: {_command.Version}");
                    }
                    catch (Exception versionEx)
                    {
                        Console.WriteLine($"Failed to save version file: {versionEx.Message}");
                        SendStatusResponse("version_save_error", versionEx.Message);
                    }
                }

                _statusCallback("LabView 설치 완료");
                _installStatusCallback?.Invoke("대기 중");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EXCEPTION] {ex.Message}");
                Console.WriteLine($"Stack Trace:\n{ex.StackTrace}");
                Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                _statusCallback($"설치 실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
            }
        }
    }
}
