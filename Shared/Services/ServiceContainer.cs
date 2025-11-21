using System;
using System.Threading.Tasks;
using System.Timers;
using AppLauncher.Features.MqttControl;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Shared.Services
{
    /// <summary>
    /// 전역 서비스 컨테이너 (Pinia/Riverpod 스타일)
    /// </summary>
    public static class ServiceContainer
    {
        public static MqttService? MqttService { get; private set; }
        public static MqttMessageHandler? MqttMessageHandler { get; private set; }
        public static LauncherConfig? Config { get; private set; }
        public static ApplicationLauncher? AppLauncher { get; set; }
        private static readonly object _lock = new object();
        private static Timer? _statusTimer;
        /// <summary>
        /// 모든 서비스 초기화 및 MQTT 연결 시작
        /// </summary>
        public static void Initialize(LauncherConfig config)
        {
            lock (_lock)
            {
                // 기존 서비스가 있으면 먼저 정리
                Dispose();

                Config = config;

                // MQTT 서비스 초기화
                string clientId = HardwareInfo.GetHardwareUuid();
                MqttService = new MqttService(config.MqttSettings, clientId);

                // MQTT 메시지 핸들러 초기화
                MqttMessageHandler = new MqttMessageHandler(MqttService, config);

                // MQTT 메시지 수신 이벤트 연결
                MqttService.MessageReceived += (msg) => MqttMessageHandler?.HandleMessage(msg);

                // MQTT 연결 상태 변경 이벤트 연결 (1분마다 상태 전송)
                MqttService.ConnectionStateChanged += OnMqttConnectionStateChanged;

                // MQTT 연결 시작 (백그라운드에서 비동기 실행)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await MqttService.ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ServiceContainer] MQTT 초기 연결 실패: {ex.Message}");
                        // 자동 재연결 로직이 작동하므로 예외 무시
                    }
                });
            }
        }

        /// <summary>
        /// MQTT 연결 상태 변경 시 타이머 제어
        /// </summary>
        private static void OnMqttConnectionStateChanged(bool isConnected)
        {
            if (isConnected)
            {
                // 연결되면 타이머 시작 (1분 = 60000ms)
                if (_statusTimer == null)
                {
                    _statusTimer = new Timer(60000); // 1분
                    _statusTimer.Elapsed += OnStatusTimerElapsed;
                    _statusTimer.AutoReset = true;
                }
                _statusTimer.Start();
                Console.WriteLine("[ServiceContainer] 상태 전송 타이머 시작 (1분 간격)");

                // 즉시 한번 전송
                MqttMessageHandler?.SendStatus("connected");
            }
            else
            {
                // 연결 끊기면 타이머 정지
                _statusTimer?.Stop();
                Console.WriteLine("[ServiceContainer] 상태 전송 타이머 정지");
            }
        }

        /// <summary>
        /// 1분마다 상태 전송
        /// </summary>
        private static void OnStatusTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                MqttMessageHandler?.SendStatus("running");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceContainer] 상태 전송 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 서비스 정리
        /// </summary>
        public static void Dispose()
        {
            // ApplicationLauncher 정리 (관리 중인 프로세스 종료)
            if (AppLauncher != null)
            {
                try
                {
                    Console.WriteLine("[ServiceContainer] ApplicationLauncher 정리 중...");
                    AppLauncher.Cleanup();

                    // 혹시 _managedProcess가 null인 경우를 대비해 프로세스 이름으로도 종료 시도
                    if (Config != null && !string.IsNullOrEmpty(Config.TargetExecutable))
                    {
                        Console.WriteLine($"[ServiceContainer] 프로세스 이름으로 추가 종료 시도: {Config.TargetExecutable}");
                        AppLauncher.CleanupByProcessName(Config.TargetExecutable);
                    }

                    Console.WriteLine("[ServiceContainer] ApplicationLauncher 정리 완료");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServiceContainer] ApplicationLauncher 정리 오류: {ex.Message}");
                }
                AppLauncher = null;
            }

            // 타이머 정리
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Elapsed -= OnStatusTimerElapsed;
                _statusTimer.Dispose();
                _statusTimer = null;
            }

            // 로컬 변수로 캡처하여 스레드 안전성 확보
            var mqttService = MqttService;
            if (mqttService != null)
            {
                try
                {
                    // 이벤트 핸들러 제거 (메모리 누수 방지)
                    mqttService.MessageReceived -= (msg) => MqttMessageHandler?.HandleMessage(msg);
                    mqttService.ConnectionStateChanged -= OnMqttConnectionStateChanged;

                    // 연결되어 있으면 끊기
                    if (mqttService.IsConnected)
                    {
                        mqttService.DisconnectAsync()
                                          .ConfigureAwait(false)
                                          .GetAwaiter()
                                          .GetResult();
                    }
                }
                catch
                {
                    // 연결 해제 실패 무시
                }

                mqttService.Dispose();
                MqttService = null;
            }

            MqttMessageHandler = null;
            Config = null;
        }
    }
}
