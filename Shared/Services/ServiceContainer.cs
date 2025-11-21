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
        /// </summary>
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
        /// </summary>
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
