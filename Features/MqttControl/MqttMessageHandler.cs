using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
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

        public MqttMessageHandler(
            MqttService mqttService,
            LauncherConfig config
            )
        {
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Balloon Tip 콜백 설정 (나중에 설정 가능)
        /// </summary>
        public void SetBalloonTipCallback(Action<string, string, int> callback)
        {
        }

        /// <summary>
        /// MQTT 메시지 수신 처리
        /// </summary>
        public void HandleMessage(MqttMessage message)
        {
            try
            {
                // JSON 메시지 파싱
                var command = JsonConvert.DeserializeObject<LaunchCommand>(message.Payload);

                if (command == null)
                {
                    return;
                }

                // 명령 처리
                switch (command.Command.ToUpper())
                {

                    case "LABVIEW_UPDATE_IMMEDIATE":
                    case "LABVIEWUPDATEIMMEDIATE":
                        _ = UpdateLabView(command, true);
                        break;

                    case "LABVIEW_UPDATE":
                    case "LABVIEWUPDATE":
                        _ = UpdateLabView(command, false);
                        break;

                    case "LAUNCHER_UPDATE":
                    case "LAUNCHERUPDATE":
                        UpdateLauncher(command);
                        break;

                    case "STATUS":
                        // 서버 요청에 대한 응답
                        SendStatus("response");
                        break;

                    case "LOCATIONCHANGE":
                    case "LOCATION_CHANGE":
                        ChangeLocation(command);
                        break;

                    case "SETTINGS_UPDATE":
                    case "SETTINGSUPDATE":
                        UpdateSettingsFile(command);
                        break;

                    case "SETTINGS_GET":
                    case "SETTINGSGET":
                        GetSettingsFile();
                        break;

                    default:
                        break;
                }
            }
            catch (Exception)
            {
                // 메시지 처리 오류 무시
            }
        }

        /// <summary>
        /// MQTT 명령으로 애플리케이션 실행
        /// </summary>
        private async Task UpdateLabView(LaunchCommand command, bool isDownloadImmediate)
        {
            try
            {
                // url이 있으면 업데이트 진행
                if (!string.IsNullOrEmpty(command.URL))
                {
                    var LabViewUpdater = new LabViewUpdater(
                        command,
                        _config,
                        SendStatusResponse
                     );

                    await LabViewUpdater.ScheduleUpdate(isDownloadImmediate);
                    return;
                }
            }
            catch (Exception ex)
            {
                SendStatusResponse("error", ex.Message);
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
                string launcherVersion = VersionInfo.LAUNCHER_VERSION;

                // 대상 앱 버전 가져오기
                string targetAppVersion = GetTargetAppVersion();

                // 하드웨어 UUID 가져오기
                string hardwareUuid = HardwareInfo.GetHardwareUuid();

                // LabVIEW 프로세스 상태 가져오기 (프로세스 이름으로 검색)
                object labviewStatus;
                Process? hbotProcess = FindHBOTOperatorProcess();

                if (hbotProcess != null && !hbotProcess.HasExited)
                {
                    hbotProcess.Refresh(); // 최신 정보로 갱신

                    var runningTime = DateTime.Now - hbotProcess.StartTime;
                    long memoryMB = hbotProcess.WorkingSet64 / 1024 / 1024;

                    labviewStatus = new
                    {
                        status = "running",
                        processName = hbotProcess.ProcessName,
                        pid = hbotProcess.Id,
                        runningTime = $"{runningTime.Hours:D2}:{runningTime.Minutes:D2}:{runningTime.Seconds:D2}",
                        memoryMB = memoryMB,
                        responding = hbotProcess.Responding,
                        threadCount = hbotProcess.Threads.Count,
                    };
                }
                else
                {
                    labviewStatus = new
                    {
                        status = "stopped",
                        processName = (string?)null,
                        pid = (int?)null,
                        runningTime = (string?)null,
                        memoryMB = (long?)null,
                        responding = (bool?)null,
                        threadCount = (int?)null
                    };
                }

                var status = new
                {
                    status = statusMessage,
                    payload = new
                    {
                        labviewStatus = labviewStatus,
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
                    },
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, status);

                Console.WriteLine($"[MQTT] Status sent ({statusMessage}) - Launcher: {launcherVersion}, App: {targetAppVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Status send error: {ex.Message}");
            }
        }

        /// <summary>
        /// 상태 응답 전송
        /// </summary>
        private async void SendStatusResponse(string status, string message)
        {
            try
            {
                if (_mqttService == null || !_mqttService.IsConnected)
                {
                    return;
                }

                var response = new
                {
                    status = status,
                    message = message,
                    timestamp = DateTime.Now
                };

                await _mqttService.PublishJsonAsync(_mqttService.InstallStatusTopic, response);
            }
            catch
            {
                // 응답 전송 실패는 무시
            }
        }

        /// <summary>
        /// MQTT 명령을 통한 런처 업데이트
        /// </summary>
        private async void UpdateLauncher(LaunchCommand command)
        {
            try
            {
                // MQTT 명령에서 다운로드 URL과 버전 정보 확인
                if (string.IsNullOrWhiteSpace(command.URL))
                {
                    return;
                }

                await UpdateLauncherAsync(command.URL);
            }
            catch (Exception ex)
            {
                SendStatusResponse("error", ex.Message);
            }
        }

        /// <summary>
        /// setting.ini 파일 경로 가져오기
        /// </summary>
        private string GetSettingsFilePath()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents, "HBOT", "Setting", "setting.ini");
        }

        /// <summary>
        /// setting.ini 파일 덮어쓰기
        /// </summary>
        private async void UpdateSettingsFile(LaunchCommand command)
        {
            try
            {
                Console.WriteLine("[MQTT] SETTINGS_UPDATE 명령 수신");

                // settingContent 확인
                if (string.IsNullOrEmpty(command.SettingContent))
                {
                    Console.WriteLine("[MQTT] SettingContent가 비어있음");
                    SendStatusResponse("error", "settingContent is empty");
                    return;
                }

                string filePath = GetSettingsFilePath();

                Console.WriteLine($"[MQTT] 파일 경로: {filePath}");
                Console.WriteLine($"[MQTT] SettingContent 길이: {command.SettingContent.Length}");

                // 디렉토리 확인
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Console.WriteLine($"[MQTT] 디렉토리 생성: {directory}");
                    Directory.CreateDirectory(directory);
                }

                // 파일 쓰기
                File.WriteAllText(filePath, command.SettingContent);
                Console.WriteLine($"[MQTT] setting.ini 업데이트 완료: {filePath}");

                // 저장된 내용 다시 읽어서 확인용으로 전송
                string savedContent = File.ReadAllText(filePath);
                Console.WriteLine($"[MQTT] 저장된 파일 다시 읽기 완료. 길이: {savedContent.Length}");

                var response = new
                {
                    status = "settings_updated",
                    settingContent = savedContent,
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, response);
                Console.WriteLine("[MQTT] 저장된 setting.ini 내용 전송 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] setting.ini 업데이트 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
            }
        }

        /// <summary>
        /// setting.ini 파일 내용 읽어서 전송
        /// </summary>
        private async void GetSettingsFile()
        {
            try
            {
                Console.WriteLine("[MQTT] SETTINGS_GET 명령 수신");

                string filePath = GetSettingsFilePath();
                Console.WriteLine($"[MQTT] 파일 경로: {filePath}");

                if (!File.Exists(filePath))
                {
                    Console.WriteLine("[MQTT] setting.ini 파일이 존재하지 않음");
                    SendStatusResponse("error", $"File not found: {filePath}");
                    return;
                }

                string content = File.ReadAllText(filePath);
                Console.WriteLine($"[MQTT] setting.ini 읽기 완료. 길이: {content.Length}");

                var response = new
                {
                    status = "settings_content",
                    settingContent = content,
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };

                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, response);
                Console.WriteLine("[MQTT] setting.ini 내용 전송 완료");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] setting.ini 읽기 오류: {ex.Message}");
                SendStatusResponse("error", ex.Message);
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
                    SendStatusResponse("error", "Location is empty");
                    return;
                }

                // 현재 설정 로드
                var config = ConfigManager.LoadConfig();

                // Location 변경
                string oldLocation = config.MqttSettings.Location ?? "미설정";
                config.MqttSettings.Location = command.Location;

                ConfigManager.SaveConfig(config);

                SendStatusResponse("location_changed", $"Location changed from '{oldLocation}' to '{command.Location}'");
            }
            catch (Exception ex)
            {
                SendStatusResponse("error", ex.Message);
            }
        }


        /// <summary>
        /// 런처 업데이트 실행
        /// </summary>
        private async Task UpdateLauncherAsync(string downloadUrl)
        {
            try
            {
                var updater = new LauncherUpdater(
                    downloadUrl
                );

                // 업데이트 파일 다운로드 및 교체
                string? updatedPath = await updater.DownloadAndReplaceExeAsync();

                if (updatedPath != null)
                {
                    // // 토스트 알림 표시 (5초 동안)
                    // _showBalloonTipCallback?.Invoke(
                    //     "런처 업데이트 완료",
                    //     "업데이트가 완료되었습니다.\n컴퓨터를 재시작하면 새 버전이 적용됩니다.",
                    //     5000
                    // );
                }
            }
            catch (Exception)
            {
                // 업데이트 오류 무시
            }
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
        /// HBOT Operator 프로세스 찾기
        /// </summary>
        private Process? FindHBOTOperatorProcess()
        {
            try
            {
                // config에서 TargetExecutable의 프로세스 이름 추출
                string? targetProcessName = null;
                if (!string.IsNullOrEmpty(_config.TargetExecutable))
                {
                    targetProcessName = Path.GetFileNameWithoutExtension(_config.TargetExecutable);
                }

                if (string.IsNullOrEmpty(targetProcessName))
                {
                    return null;
                }

                // 해당 프로세스 이름으로 검색
                var processes = Process.GetProcessesByName(targetProcessName);
                if (processes.Length > 0)
                {
                    return processes[0];
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// LabVIEW 앱이 없거나 오류 발생 시 서버에 업데이트 요청
        /// </summary>
        public async void RequestLabViewUpdate(string reason)
        {
            try
            {
                if (_mqttService == null || !_mqttService.IsConnected)
                {
                    Console.WriteLine("[MQTT] Cannot request update - MQTT not connected");
                    return;
                }

                // 현재 버전 정보 수집
                string launcherVersion = VersionInfo.LAUNCHER_VERSION;
                string hardwareUuid = HardwareInfo.GetHardwareUuid();

                var status = new
                {
                    status = "labview_update_request",
                    payload = new
                    {
                        reason = reason,
                        location = _config.MqttSettings?.Location,
                        hardwareUUID = hardwareUuid,
                        launcher = new
                        {
                            version = launcherVersion
                        },
                        targetApp = new
                        {
                            version = "0.0.0"
                        }
                    },
                    timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                };
                await _mqttService.PublishJsonAsync(_mqttService.StatusTopic, status);

                Console.WriteLine($"[MQTT] Update request sent - Reason: {reason}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MQTT] Failed to request update: {ex.Message}");
            }
        }

    }
}
