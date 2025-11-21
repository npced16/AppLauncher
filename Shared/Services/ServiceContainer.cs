using System;
using System.Threading.Tasks;
using AppLauncher.Features.MqttControl;
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
        private static readonly object _lock = new object();
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
        /// 모든 서비스 정리
        /// </summary>
        public static void Dispose()
        {
            // 로컬 변수로 캡처하여 스레드 안전성 확보
            var mqttService = MqttService;
            if (mqttService != null)
            {
                try
                {
                    // 이벤트 핸들러 제거 (메모리 누수 방지)
                    mqttService.MessageReceived -= (msg) => MqttMessageHandler?.HandleMessage(msg);

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
