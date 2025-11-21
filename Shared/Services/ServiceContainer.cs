using AppLauncher.Features.MqttControl;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Shared.Services
{
    /// <summary>
    /// 전역 서비스 컨테이너 (Pinia/Riverpod 스타일)
    /// </summary>
    public static class ServiceContainer
    {
        /// <summary>
        /// MQTT 서비스
        /// </summary>
        public static MqttService? MqttService { get; set; }

        /// <summary>
        /// MQTT 메시지 핸들러
        /// </summary>
        public static MqttMessageHandler? MqttMessageHandler { get; set; }

        /// <summary>
        /// 런처 설정
        /// </summary>
        public static LauncherConfig? Config { get; set; }

        /// <summary>
        /// 모든 서비스 초기화
        /// <summary>
        /// Initializes global services and configures MQTT client and message handling using the provided launcher configuration.
        /// </summary>
        /// <param name="config">Launcher configuration that contains MQTT settings and other initialization parameters.</param>
        public static void Initialize(LauncherConfig config)
        {
            Config = config;

            // MQTT 서비스 초기화
            string clientId = HardwareInfo.GetHardwareUuid();
            MqttService = new MqttService(config.MqttSettings, clientId);

            // MQTT 메시지 핸들러 초기화
            MqttMessageHandler = new MqttMessageHandler(MqttService, config, null);

            // MQTT 메시지 수신 이벤트 연결
            MqttService.MessageReceived += (msg) => MqttMessageHandler?.HandleMessage(msg);
        }

        /// <summary>
        /// 모든 서비스 정리
        /// <summary>
        /// Releases and cleans up all services held by the ServiceContainer.
        /// </summary>
        /// <remarks>
        /// If an MQTT service exists, it will be disconnected and disposed, and all service references (MqttService, MqttMessageHandler, Config) will be cleared.
        /// </remarks>
        public static void Dispose()
        {
            if (MqttService != null)
            {
                MqttService.DisconnectAsync().Wait();
                MqttService.Dispose();
                MqttService = null;
            }

            MqttMessageHandler = null;
            Config = null;
        }
    }
}