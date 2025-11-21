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

        /// <summary>
        /// 모든 서비스 초기화
        /// </summary>
        public static void Initialize(LauncherConfig config)
        {
            // 기존 서비스가 있으면 먼저 정리
            Dispose();

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
            // 로컬 변수로 캡처하여 스레드 안전성 확보
            var mqttService = MqttService;
            if (mqttService != null)
            {
                try
                {
                    // 연결되어 있으면 끊기
                    if (mqttService.IsConnected)
                    {
                        Task.Run(async () => await mqttService.DisconnectAsync())
                            .Wait(TimeSpan.FromSeconds(3));
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
