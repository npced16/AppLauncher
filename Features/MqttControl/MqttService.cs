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

        /// <summary>
        /// 로그 메시지 이벤트
        /// </summary>
        public event Action<string>? LogMessage;

        public bool IsConnected => _isConnected;

        public string LastError { get; private set; } = "";

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
                LogMessage?.Invoke($"MQTT 연결 시도: {_settings.Broker}:{_settings.Port}");

                var factory = new MqttClientFactory();
                _mqttClient = factory.CreateMqttClient();

                _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
                _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

                MqttClientConnectResult? result = null;
                var protocolVersions = new[]
                {
                    (MQTTnet.Formatter.MqttProtocolVersion.V311, "MQTT 3.1.1"),
                    (MQTTnet.Formatter.MqttProtocolVersion.V310, "MQTT 3.1.0"),
                    (MQTTnet.Formatter.MqttProtocolVersion.V500, "MQTT 5.0")
                };

                Exception? lastException = null;

                foreach (var (version, versionName) in protocolVersions)
                {
                    try
                    {
                        LogMessage?.Invoke($"{versionName} 연결 시도 중...");

                        var optionsBuilder = new MqttClientOptionsBuilder()
                            .WithTcpServer(_settings.Broker, _settings.Port)
                            .WithClientId(_settings.ClientId)
                            .WithCleanSession(false)
                            .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                            .WithTimeout(TimeSpan.FromSeconds(10))
                            .WithProtocolVersion(version);

                        if (!string.IsNullOrEmpty(_settings.Username))
                        {
                            optionsBuilder = optionsBuilder.WithCredentials(_settings.Username, "");
                        }

                        var options = optionsBuilder.Build();
                        result = await _mqttClient.ConnectAsync(options, CancellationToken.None);

                        if (result.ResultCode == MqttClientConnectResultCode.Success)
                        {
                            LogMessage?.Invoke($"{versionName} 연결 성공!");
                            break;
                        }
                        else
                        {
                            LogMessage?.Invoke($"{versionName} 연결 실패: {result.ResultCode} - {result.ReasonString}");
                            lastException = new Exception($"{versionName} 연결 실패: {result.ResultCode} - {result.ReasonString}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage?.Invoke($"{versionName} 연결 실패: {ex.Message}");
                        if (ex.InnerException != null)
                        {
                            LogMessage?.Invoke($"  내부 오류: {ex.InnerException.Message}");
                        }
                        lastException = ex;
                    }
                }

                if (result == null || result.ResultCode != MqttClientConnectResultCode.Success)
                {
                    throw lastException ?? new Exception("모든 MQTT 프로토콜 버전에서 연결 실패");
                }

                _isConnected = true;
                LastError = "";
                ConnectionStateChanged?.Invoke(true);

                var usedVersion = _mqttClient.Options.ProtocolVersion;
                LogMessage?.Invoke($"사용된 MQTT 프로토콜 버전: {usedVersion}");

                var subscribeOptions = factory.CreateSubscribeOptionsBuilder()
                    .WithTopicFilter(f => f.WithTopic(_settings.Topic))
                    .Build();

                await _mqttClient.SubscribeAsync(subscribeOptions, CancellationToken.None);
                LogMessage?.Invoke($"토픽 구독 완료: {_settings.Topic}");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                LastError = $"MQTT 연결 오류: {ex.Message}";
                ConnectionStateChanged?.Invoke(false);
                LogMessage?.Invoke(LastError);
                throw new Exception(LastError, ex);
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

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            _isConnected = false;
            ConnectionStateChanged?.Invoke(false);

            LogMessage?.Invoke("MQTT 연결이 끊어졌습니다.");

            // 자동 재연결 비활성화 - 사용자가 수동으로 재연결하도록
            return Task.CompletedTask;
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

        // 업데이트 명령용 필드
        [JsonProperty("version")]
        public string? Version { get; set; }
    }
}
