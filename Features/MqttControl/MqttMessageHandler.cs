using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Features.VersionManagement;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using Newtonsoft.Json;

namespace AppLauncher.Features.MqttControl
{
    /// <summary>
    /// MQTT 메시지 처리 및 상태 보고를 담당하는 클래스
    /// </summary>
    public class MqttMessageHandler
    {
        private readonly MqttService _mqttService;
        private readonly LauncherConfig _config;
        private readonly Action<string> _statusCallback;
        private readonly Action<string>? _installStatusCallback;
        private readonly ApplicationLauncher? _applicationLauncher;

        public MqttMessageHandler(
            MqttService mqttService,
            LauncherConfig config,
            Action<string> statusCallback,
            Action<string>? installStatusCallback = null,
            ApplicationLauncher? applicationLauncher = null)
        {
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _statusCallback = statusCallback ?? throw new ArgumentNullException(nameof(statusCallback));
            _installStatusCallback = installStatusCallback;
            _applicationLauncher = applicationLauncher;
        }

        /// <summary>
        /// MQTT 메시지 수신 처리
        /// </summary>
        public void HandleMessage(MqttMessage message)
        {
            try
            {
                _statusCallback($"메시지 수신: {message.Topic}");

                // JSON 메시지 파싱
                var command = JsonConvert.DeserializeObject<LaunchCommand>(message.Payload);

                if (command == null)
                {
                    return;
                }

                // 명령 처리
                switch (command.Command.ToUpper())
                {
                    case "LABVIEW_UPDATE":
                    case "LABVIEWUPDATE":
                        LaunchApplication(command);
                        break;

                    case "LAUNCHER_UPDATE":
                    case "LAUNCHERUPDATE":
                        UpdateLauncherViaMqtt(command);
                        break;

                    case "STATUS":
                        // 서버 요청에 대한 응답
                        SendStatus("response");
                        break;

                    case "LOCATIONCHANGE":
                    case "LOCATION_CHANGE":
                        ChangeLocation(command);
                        break;

                    default:
                        _statusCallback($"알 수 없는 명령: {command.Command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _statusCallback($"메시지 처리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// MQTT 명령으로 애플리케이션 실행
        /// </summary>
        private async void LaunchApplication(LaunchCommand command)
        {
            try
            {
                string executable;

                // url이 있으면 다운로드 후 실행
                if (!string.IsNullOrEmpty(command.URL))
                {
                    _statusCallback("다운로드 중...");
                    executable = await DownloadAndExecuteAsync(command.URL, command);
                    return;
                }
                // 다운로드 에러 mqtt 응답은 DownloadAndExecuteAsync에서 처리 
            }
            catch (Exception ex)
            {
                _statusCallback($"실행 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
            }
        }

        /// <summary>
        /// 파일 다운로드 및 실행
        /// </summary>
        private async Task<string> DownloadAndExecuteAsync(string downloadUrl, LaunchCommand command)
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
                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
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
                    var response = await httpClient.GetAsync(downloadUrl);
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
                    await ExtractZipFileAsync(downloadPath, command);
                }
                else
                {
                    // exe 파일이면 실행
                    ExecuteProgram(downloadPath, command);
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
        private async Task ExtractZipFileAsync(string zipFilePath, LaunchCommand command)
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
                    ExecuteSetupWithPowerShell(setupExePath, command);
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
        private void ExecuteProgram(string executable, LaunchCommand command)
        {
            try
            {
                string fileName = Path.GetFileName(executable).ToLower();

                // setup.exe는 PowerShell로 자동 설치 실행
                if (fileName == "setup.exe")
                {
                    ExecuteSetupWithPowerShell(executable, command);
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
        private void ExecuteSetupWithPowerShell(string setupExePath, LaunchCommand command)
        {
            try
            {
                // 관리자 권한 확인
                bool isAdmin = IsRunningAsAdministrator();

                Console.WriteLine("=== AppLauncher Installation Start ===");
                Console.WriteLine($"Start Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"Setup Path: {setupExePath}");
                Console.WriteLine($"Administrator: {(isAdmin ? "Yes" : "No")}");

                _statusCallback("LabView 설치 시작...");
                _statusCallback($"관리자 권한: {(isAdmin ? "예" : "아니오")}");
                _installStatusCallback?.Invoke("설치 중");

                if (!isAdmin)
                {
                    Console.WriteLine("[WARNING] Not running as administrator - installation may fail");
                    _statusCallback("[경고] 관리자 권한으로 실행되지 않았습니다. 설치가 실패할 수 있습니다.");
                    SendStatusResponse("warning", "Not running as administrator");
                }

                // MQTT: 설치 시작 로그 전송
                SendStatusResponse("install_start", $"Setup.exe 실행 시작: {setupExePath}");

                // 로그 파일 경로 생성 (C:\ProgramData\AppLauncher\Logs)
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string logDir = Path.Combine(programDataPath, "AppLauncher", "Logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                string logFilePath = Path.Combine(logDir, $"install_log_{DateTime.Now:yyyyMMddHHmmss}.txt");
                Console.WriteLine($"Log file path: {logFilePath}");

                // setup.exe 실행하고 모니터링을 위한 PID 가져오기
                string psCommand = $@"
$proc = Start-Process '{setupExePath}' -ArgumentList '/q','/AcceptLicenses','yes','/log','{logFilePath}' -Verb RunAs -PassThru
Write-Output $proc.Id
";

                Console.WriteLine("Executing Setup.exe via PowerShell (-Verb RunAs)");
                Console.WriteLine($"Setup Path: {setupExePath}");
                _statusCallback($"PowerShell로 Setup.exe 실행");
                SendStatusResponse("install_command", setupExePath);

                // TODO : C 드라이브 밖에서 실행해 볼 것  
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-Command \"{psCommand}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(setupExePath) // setup.exe가 있는 폴더로 설정
                };

                var psProcess = Process.Start(startInfo);
                if (psProcess != null)
                {
                    Console.WriteLine($"PowerShell process started (PID: {psProcess.Id})");

                    // setup.exe PID 읽기
                    string output = psProcess.StandardOutput.ReadToEnd();
                    string error = psProcess.StandardError.ReadToEnd();

                    psProcess.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                    {
                        Console.WriteLine($"PowerShell Error:\n{error}");
                    }

                    // 출력에서 setup.exe PID 파싱
                    if (int.TryParse(output.Trim(), out int setupPid))
                    {
                        Console.WriteLine($"Setup.exe PID: {setupPid}");
                        SendStatusResponse("setup_pid", setupPid.ToString());

                        // 프로세스가 시작될 때까지 잠시 대기
                        System.Threading.Thread.Sleep(1000);

                        // setup.exe 프로세스 모니터링
                        try
                        {
                            var setupProcess = Process.GetProcessById(setupPid);
                            Console.WriteLine($"Monitoring setup.exe (PID: {setupPid})");
                            _statusCallback($"설치 모니터링 중 (PID: {setupPid})");

                            // 5초마다 프로세스 모니터링
                            while (!setupProcess.HasExited)
                            {
                                try
                                {
                                    var cpuTime = setupProcess.TotalProcessorTime;
                                    var workingSet = setupProcess.WorkingSet64 / 1024 / 1024; // MB
                                    Console.WriteLine($"[Monitor] PID: {setupPid}, CPU Time: {cpuTime.TotalSeconds:F1}s, Memory: {workingSet}MB");
                                    SendStatusResponse("setup_status", $"CPU: {cpuTime.TotalSeconds:F1}s, Mem: {workingSet}MB");
                                }
                                catch
                                {
                                    // 프로세스가 종료되었을 수 있음
                                    break;
                                }

                                System.Threading.Thread.Sleep(5000);
                            }

                            setupProcess.WaitForExit();
                            int exitCode = setupProcess.ExitCode;
                            Console.WriteLine($"Setup.exe Exit Code: {exitCode}");

                            if (exitCode != 0)
                            {
                                Console.WriteLine($"[FAILED] Installation failed (Exit Code: {exitCode})");
                                Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                                _statusCallback($"설치 실패 (종료 코드: {exitCode})");
                                SendStatusResponse("install_failed", $"Exit code: {exitCode}");
                                _installStatusCallback?.Invoke("대기 중");
                                return;
                            }

                            Console.WriteLine($"[SUCCESS] Installation completed");
                            Console.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                            _statusCallback($"설치 완료 (종료 코드: {exitCode})");
                            SendStatusResponse("install_success", $"Exit code: {exitCode}");
                        }
                        catch (ArgumentException)
                        {
                            Console.WriteLine($"[ERROR] Setup.exe process (PID: {setupPid}) not found or already exited");
                            _statusCallback($"설치 프로세스를 찾을 수 없음 (PID: {setupPid})");
                            SendStatusResponse("error", $"Setup process not found: {setupPid}");
                            _installStatusCallback?.Invoke("대기 중");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ERROR] Failed to get Setup.exe PID. PowerShell output: {output}");
                        _statusCallback("Setup.exe PID를 가져올 수 없음");
                        SendStatusResponse("error", "Failed to get setup.exe PID");
                        _installStatusCallback?.Invoke("대기 중");
                        return;
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

                // 업데이트 완료 시 버전 정보 저장
                if (!string.IsNullOrEmpty(command.Version) && !string.IsNullOrEmpty(_config.LocalVersionFile))
                {
                    try
                    {
                        string? versionFileDir = Path.GetDirectoryName(_config.LocalVersionFile);
                        if (!string.IsNullOrEmpty(versionFileDir) && !Directory.Exists(versionFileDir))
                        {
                            Directory.CreateDirectory(versionFileDir);
                        }
                        File.WriteAllText(_config.LocalVersionFile, command.Version);
                        Console.WriteLine($"Version file saved: {command.Version}");
                        _statusCallback($"버전 {command.Version} 저장 완료");
                        SendStatusResponse("version_saved", $"버전 파일 저장: {command.Version}");
                    }
                    catch (Exception versionEx)
                    {
                        Console.WriteLine($"Failed to save version file: {versionEx.Message}");
                        SendStatusResponse("version_save_error", versionEx.Message);
                    }
                }

                _statusCallback("LabView 자동 설치 실행됨 (약 30분 소요)");

                // Send status response
                SendStatusResponse("installing", $"{setupExePath}");
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

        /// <summary>
        /// 현재 상태를 MQTT로 전송 (연결 시 자동 호출)
        /// </summary>
        public async void SendStatus(string statusMessage = "")
        {
            try
            {
                if (_mqttService == null || !_mqttService.IsConnected)
                {
                    return;
                }

                // 런처 버전 가져오기
                string launcherVersion = GetLauncherVersion();

                // 대상 앱 버전 가져오기
                string targetAppVersion = GetTargetAppVersion();

                // 하드웨어 UUID 가져오기
                string hardwareUuid = HardwareInfo.GetHardwareUuid();

                var status = new
                {
                    status = statusMessage,
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                    mqttConnected = _mqttService.IsConnected,
                    location = _config.MqttSettings?.Location,
                    hardwareUUID = hardwareUuid,
                    launcher = new
                    {
                        version = launcherVersion
                    },
                    targetApp = new
                    {
                        version = targetAppVersion
                    }
                };

                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, status);

                Console.WriteLine($"[MQTT] Status sent ({statusMessage}) - Launcher: {launcherVersion}, App: {targetAppVersion}");
                _statusCallback($"상태 전송 완료 ({statusMessage}) (런처: {launcherVersion}, 앱: {targetAppVersion})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Status send error: {ex.Message}");
                _statusCallback($"상태 전송 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 상태 응답 전송
        /// </summary>
        private async void SendStatusResponse(string status, string message)
        {
            try
            {
                if (_mqttService == null || !_mqttService.IsConnected || _config.MqttSettings == null)
                {
                    return;
                }

                var response = new
                {
                    status = status,
                    message = message,
                    timestamp = DateTime.Now
                };

                await _mqttService.PublishJsonAsync(_mqttService.ResponseTopic, response);
            }
            catch
            {
                // 응답 전송 실패는 무시
            }
        }

        /// <summary>
        /// MQTT 명령을 통한 런처 업데이트
        /// </summary>
        private async void UpdateLauncherViaMqtt(LaunchCommand command)
        {
            try
            {
                _statusCallback("MQTT 명령: 런처 업데이트 확인");
                _installStatusCallback?.Invoke("버전 확인 중");

                // MQTT 명령에서 다운로드 URL과 버전 정보 확인
                if (string.IsNullOrWhiteSpace(command.URL))
                {
                    _statusCallback("업데이트 실패: 다운로드 URL이 제공되지 않았습니다.");
                    SendStatusResponse("error", "Download URL not provided in MQTT command");
                    _installStatusCallback?.Invoke("대기 중");
                    return;
                }

                if (string.IsNullOrWhiteSpace(command.Version))
                {
                    _statusCallback("업데이트 실패: 버전 정보가 제공되지 않았습니다.");
                    SendStatusResponse("error", "Version not provided in MQTT command");
                    _installStatusCallback?.Invoke("대기 중");
                    return;
                }

                // 현재 런처 버전 가져오기 (코드에 내장된 버전)
                string currentVersion = VersionInfo.LAUNCHER_VERSION;
                string incomingVersion = command.Version;

                _statusCallback($"버전 비교: 현재 {currentVersion} vs 수신 {incomingVersion}");
                SendStatusResponse("version_check", $"현재: {currentVersion}, 수신: {incomingVersion}");

                // 버전 비교
                bool needsUpdate = CompareVersions(currentVersion, incomingVersion);

                if (!needsUpdate)
                {
                    _statusCallback($"업데이트 불필요: 현재 버전({currentVersion})이 최신이거나 동일합니다.");
                    SendStatusResponse("update_skip", $"현재 버전 {currentVersion}이 최신입니다");
                    _installStatusCallback?.Invoke("대기 중");
                    return;
                }

                _statusCallback($"새 버전 감지: {currentVersion} -> {incomingVersion}");
                SendStatusResponse("update_required", $"버전 업데이트 필요: {currentVersion} -> {incomingVersion}");
                _installStatusCallback?.Invoke("런처 업데이트 중");

                // Program Files 경로로 고정
                string targetExePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "AppLauncher",
                    "AppLauncher.exe"
                );

                string versionFile = _config.LauncherVersionFile ??
                    Path.Combine(Path.GetDirectoryName(ConfigManager.GetConfigFilePath()) ?? "", "launcher_version.txt");

                _statusCallback($"새 버전 {command.Version} 다운로드 중...");
                await UpdateLauncherAsync(command.URL, command.Version, targetExePath, versionFile);
            }
            catch (Exception ex)
            {
                _statusCallback($"런처 업데이트 실패: {ex.Message}");
                SendStatusResponse("error", ex.Message);
                _installStatusCallback?.Invoke("대기 중");
            }
        }

        /// <summary>
        /// Location 변경 처리
        /// </summary>
        private void ChangeLocation(LaunchCommand command)
        {
            try
            {
                if (string.IsNullOrEmpty(command.Location))
                {
                    Console.WriteLine("[MQTT] Location change failed: Location is empty");
                    _statusCallback("Location 변경 실패: Location이 비어있음");
                    SendStatusResponse("error", "Location is empty");
                    return;
                }

                Console.WriteLine($"[MQTT] Changing location to: {command.Location}");
                _statusCallback($"Location 변경 중: {command.Location}");

                // 현재 설정 로드
                var config = ConfigManager.LoadConfig();

                if (config.MqttSettings == null)
                {
                    Console.WriteLine("[MQTT] Location change failed: MQTT settings not found");
                    _statusCallback("Location 변경 실패: MQTT 설정을 찾을 수 없음");
                    SendStatusResponse("error", "MQTT settings not found");
                    return;
                }

                // Location 변경
                string oldLocation = config.MqttSettings.Location ?? "미설정";
                config.MqttSettings.Location = command.Location;

                // 설정 저장
                ConfigManager.SaveConfig(config);

                Console.WriteLine($"[MQTT] Location changed: {oldLocation} -> {command.Location}");
                _statusCallback($"Location 변경 완료: {oldLocation} -> {command.Location}");

                // 변경된 상태 전송
                SendStatus("changeLocation");
                SendStatusResponse("location_changed", $"Location changed from '{oldLocation}' to '{command.Location}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Location change error: {ex.Message}");
                _statusCallback($"Location 변경 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
            }
        }

        /// <summary>
        /// 버전 비교: 수신한 버전이 현재 버전보다 높으면 true 반환
        /// </summary>
        private bool CompareVersions(string currentVersion, string incomingVersion)
        {
            try
            {
                // Version 클래스를 사용한 비교
                if (Version.TryParse(currentVersion, out Version? current) &&
                    Version.TryParse(incomingVersion, out Version? incoming))
                {
                    return incoming > current;
                }

                // 파싱 실패시 문자열 비교
                return string.Compare(incomingVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
            }
            catch
            {
                // 비교 실패시 업데이트 필요한 것으로 간주
                return true;
            }
        }

        /// <summary>
        /// 런처 업데이트 실행
        /// </summary>
        private async Task UpdateLauncherAsync(string downloadUrl, string newVersion, string currentExePath, string versionFile)
        {
            try
            {
                _statusCallback("런처 업데이트 다운로드 중...");

                var updater = new BackgroundUpdater(
                    downloadUrl,
                    versionFile
                );

                // 업데이트 파일 다운로드 및 교체
                string? updatedPath = await updater.DownloadAndReplaceExeAsync(newVersion, currentExePath);

                if (updatedPath != null)
                {
                    _statusCallback("업데이트 완료. 재시작합니다...");

                    await Task.Delay(2000);

                    // 새 버전 실행
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = updatedPath,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);

                    // 현재 앱 종료
                    System.Windows.Forms.Application.Exit();
                }
                else
                {
                    _statusCallback("업데이트 실패");
                }
            }
            catch (Exception ex)
            {
                _statusCallback($"업데이트 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 런처의 현재 버전을 가져옵니다 (코드 내장 버전만 사용)
        /// </summary>
        private string GetLauncherVersion()
        {
            return VersionInfo.LAUNCHER_VERSION;
        }

        /// <summary>
        /// 대상 앱의 현재 버전을 가져옵니다
        /// </summary>
        private string GetTargetAppVersion()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_config.LocalVersionFile))
                {
                    if (File.Exists(_config.LocalVersionFile))
                    {
                        string version = File.ReadAllText(_config.LocalVersionFile).Trim();
                        return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
                    }
                }

                return "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        /// <summary>
        /// 현재 프로세스가 관리자 권한으로 실행 중인지 확인
        /// </summary>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
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

    }
}
