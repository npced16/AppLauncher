using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using AppLauncher.Shared.Configuration;
using Newtonsoft.Json;

namespace AppLauncher.Features.MqttControl
{
    /// <summary>
    /// MQTT 통신을 담당하는 서비스 클래스
    /// </summary>
    public class MqttService : IDisposable
    {
        private IMqttClient? _mqttClient;
        private readonly MqttSettings _settings;
        private bool _isConnected;

        /// <summary>
        /// MQTT 메시지 수신 이벤트
        /// </summary>
        public event Action<MqttMessage>? MessageReceived;

        /// <summary>
        /// 연결 상태 변경 이벤트
        /// </summary>
        public event Action<bool>? ConnectionStateChanged;

        public bool IsConnected => _isConnected;

        public MqttService(MqttSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// MQTT 브로커에 연결
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                var factory = new MqttClientFactory();
                _mqttClient = factory.CreateMqttClient();

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithTcpServer(_settings.Broker, _settings.Port)
                    .WithClientId(_settings.ClientId);

                // 사용자 이름/비밀번호가 있으면 추가
                if (!string.IsNullOrEmpty(_settings.Username))
                {
                    optionsBuilder = optionsBuilder.WithCredentials(_settings.Username, _settings.Password);
                }

                var options = optionsBuilder.Build();

                // 메시지 수신 핸들러 등록
                _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

                // 연결 끊김 핸들러 등록
                _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

                // 브로커 연결
                await _mqttClient.ConnectAsync(options, CancellationToken.None);

                _isConnected = true;
                ConnectionStateChanged?.Invoke(true);

                // 토픽 구독
                var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => f.WithTopic(_settings.Topic))
                    .Build();

                await _mqttClient.SubscribeAsync(subscribeOptions, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                ConnectionStateChanged?.Invoke(false);
                throw new Exception($"MQTT 연결 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// MQTT 브로커 연결 해제
        /// </summary>
        public async Task DisconnectAsync()
        {
            if (_mqttClient != null && _mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }
            _isConnected = false;
            ConnectionStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 메시지 발행
        /// </summary>
        public async Task PublishAsync(string topic, string payload)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
            {
                throw new InvalidOperationException("MQTT 클라이언트가 연결되어 있지 않습니다.");
            }

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);
        }

        /// <summary>
        /// 객체를 JSON으로 직렬화하여 메시지 발행
        /// </summary>
        public async Task PublishJsonAsync<T>(string topic, T data)
        {
            string json = JsonConvert.SerializeObject(data);
            await PublishAsync(topic, json);
        }

        private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            try
            {
                var payloadSequence = e.ApplicationMessage.Payload;
                byte[] payloadBytes = new byte[payloadSequence.Length];
                payloadSequence.CopyTo(payloadBytes);
                string payload = Encoding.UTF8.GetString(payloadBytes);

                var message = new MqttMessage
                {
                    Topic = e.ApplicationMessage.Topic,
                    Payload = payload,
                    Timestamp = DateTime.Now
                };

                MessageReceived?.Invoke(message);
            }
            catch (Exception ex)
            {
                // 로깅 또는 에러 처리
                Console.WriteLine($"메시지 처리 오류: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            _isConnected = false;
            ConnectionStateChanged?.Invoke(false);

            // 재연결 시도
            if (e.ClientWasConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));

                try
                {
                    await ConnectAsync();
                }
                catch
                {
                    // 재연결 실패 시 추가 재시도는 사용자가 수동으로 하도록
                }
            }
        }

        public void Dispose()
        {
            if (_mqttClient != null)
            {
                if (_mqttClient.IsConnected)
                {
                    _mqttClient.DisconnectAsync().Wait();
                }
                _mqttClient.Dispose();
                _mqttClient = null;
            }
        }
    }

    /// <summary>
    /// MQTT 메시지 모델
    /// </summary>
    public class MqttMessage
    {
        public string Topic { get; set; } = "";
        public string Payload { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 앱 실행 명령 메시지
    /// </summary>
    public class LaunchCommand
    {
        [JsonProperty("command")]
        public string Command { get; set; } = "";

        [JsonProperty("executable")]
        public string? Executable { get; set; }

        [JsonProperty("downloadUrl")]
        public string? DownloadUrl { get; set; }

        [JsonProperty("workingDirectory")]
        public string? WorkingDirectory { get; set; }

        [JsonProperty("arguments")]
        public string? Arguments { get; set; }
    }
}
