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
        private readonly Action<string, string, int>? _showBalloonTipCallback;

        public MqttMessageHandler(
            MqttService mqttService,
            LauncherConfig config,
            Action<string, string, int>? showBalloonTipCallback = null)
        {
            _mqttService = mqttService ?? throw new ArgumentNullException(nameof(mqttService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _showBalloonTipCallback = showBalloonTipCallback;
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
                    case "LABVIEW_UPDATE":
                    case "LABVIEWUPDATE":
                        UpdateLabView(command);
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
        private void UpdateLabView(LaunchCommand command)
        {
            try
            {
                // url이 있으면 업데이트 예약 및 런처 재시작
                if (!string.IsNullOrEmpty(command.URL))
                {
                    var LabViewUpdater = new LabViewUpdater(
                        command,
                        _config,
                        SendStatusResponse
                     );

                    // 업데이트를 예약하고 런처 재시작
                    LabViewUpdater.ScheduleUpdate();
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
                        status = "stopped"
                        processName = null,
                        pid = null,
                        runningTime = null,
                        memoryMB = null,
                        responding = null,
                        threadCount = null,
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

                if (config.MqttSettings == null)
                {
                    SendStatusResponse("error", "MQTT settings not found");
                    return;
                }

                // Location 변경
                string oldLocation = config.MqttSettings.Location ?? "미설정";
                config.MqttSettings.Location = command.Location;

                // 변경된 상태 전송
                SendStatus("changeLocation");

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
                    // 토스트 알림 표시 (5초 동안)
                    _showBalloonTipCallback?.Invoke(
                        "런처 업데이트 완료",
                        "업데이트가 완료되었습니다.\n컴퓨터를 재시작하면 새 버전이 적용됩니다.",
                        5000
                    );

                    // 새 버전 실행 ( 다음 컴퓨터 재실행시 자동으로 새버전 이 실행되도록 )
                    // var startInfo = new ProcessStartInfo
                    // {
                    //     FileName = updatedPath,
                    //     UseShellExecute = true
                    // };
                    // Process.Start(startInfo);

                    // 현재 앱 종료 안함 - 컴퓨터 재시작시 자동으로 새 버전 실행됨
                    // System.Windows.Forms.Application.Exit();
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
        /// HBOT Operator 프로세스 찾기 (config의 TargetExecutable 기반)
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


    }
}
