using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using AppLauncher.Shared.Configuration;
using AppLauncher.Shared.Logger;
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
        private readonly string _clientId;
        private bool _isConnected;
        private volatile bool _isReconnecting;
        private volatile bool _isDisposed;
        private readonly FileLogger _fileLogger;

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

        public string ClientId => _clientId;

        /// <summary>
        /// 명령 수신 토픽 (device/{deviceId}/commands)
        /// </summary>
        public string CommandTopic => $"device/{_clientId}/commands";

        /// <summary>
        /// 상태 발행 토픽 (device/{deviceId}/status)
        /// </summary>
        public string StatusTopic => $"device/{_clientId}/status";

        /// <summary>
        /// 설치 상태 발행 토픽 (device/{deviceId}/installStatus)
        /// </summary>
        public string InstallStatusTopic => $"device/{_clientId}/installStatus";



        public MqttService(MqttSettings settings, string clientId)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));

            // ProgramData 경로 사용 (C:\ProgramData\AppLauncher\Logs)
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string logPath = System.IO.Path.Combine(programDataPath, "AppLauncher", "Logs");
            _fileLogger = new FileLogger(logPath, 90); // 90일(3개월) 보관

            // LogMessage 이벤트 발생 시 파일에도 기록
            LogMessage += (message) => _fileLogger.WriteLog(message, "MQTT");
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

                // 기존 클라이언트가 있으면 이벤트 핸들러 제거 후 정리
                if (_mqttClient != null)
                {
                    _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
                    _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;
                    _mqttClient.Dispose();
                }

                _mqttClient = factory.CreateMqttClient();

                // 이벤트 핸들러 등록 (중복 방지됨)
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
                            .WithClientId(_clientId)
                            .WithCleanSession(false)
                            .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                            .WithTimeout(TimeSpan.FromSeconds(10))
                            .WithProtocolVersion(version);

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
                    .WithTopicFilter(f => f.WithTopic(CommandTopic))
                    .Build();

                await _mqttClient.SubscribeAsync(subscribeOptions, CancellationToken.None);
                LogMessage?.Invoke($"토픽 구독 완료: {CommandTopic}");
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

            // 송신 메시지 로그 출력 (JSON 형태로)
            LogMessage?.Invoke($"[메시지 송신] 토픽: {topic}");

            try
            {
                var jsonObj = JsonConvert.DeserializeObject(payload);
                string formattedJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                LogMessage?.Invoke($"  내용 (JSON):\n{formattedJson}");
            }
            catch
            {
                // JSON이 아닌 경우 원본 그대로 출력
                LogMessage?.Invoke($"  내용: {payload}");
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
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
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

                // 수신 메시지 로그 출력 (JSON 형태로)
                LogMessage?.Invoke($"[메시지 수신] 토픽: {e.ApplicationMessage.Topic}");

                // JSON 파싱 시도 후 예쁘게 출력
                try
                {
                    var jsonObj = JsonConvert.DeserializeObject(payload);
                    string formattedJson = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                    LogMessage?.Invoke($"  내용:\n{formattedJson}");
                }
                catch
                {
                    // JSON이 아닌 경우 원본 그대로 출력
                    LogMessage?.Invoke($"  내용: {payload}");
                }

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
                LogMessage?.Invoke($"메시지 처리 오류: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            _isConnected = false;
            ConnectionStateChanged?.Invoke(false);

            LogMessage?.Invoke("MQTT 연결이 끊어졌습니다.");

            // 자동 재연결 시작 (Dispose 호출된 경우 제외)
            if (!_isDisposed && !_isReconnecting)
            {
                _ = Task.Run(async () => await AutoReconnectAsync());
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 인터넷 연결 상태 확인
        /// </summary>
        private bool IsInternetAvailable()
        {
            try
            {
                using (var client = new System.Net.NetworkInformation.Ping())
                {
                    // Google DNS로 ping (8.8.8.8)
                    var reply = client.Send("8.8.8.8", 3000);
                    return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 자동 재연결 로직 (1분 간격으로 무한 재시도)
        /// </summary>
        private async Task AutoReconnectAsync()
        {
            _isReconnecting = true;
            int retryCount = 0;

            while (!_isConnected && !_isDisposed)
            {
                try
                {
                    retryCount++;
                    LogMessage?.Invoke($"60초 후 자동 재연결 시도... (시도 #{retryCount})");
                    await Task.Delay(60000); // 60초 대기

                    if (_isDisposed || _isConnected)
                        break;

                    // 인터넷 연결 확인
                    bool internetAvailable = IsInternetAvailable();
                    if (!internetAvailable)
                    {
                        LogMessage?.Invoke($"[재연결 #{retryCount}] 인터넷 연결 없음 - 다음 시도 대기 중...");
                        continue;
                    }

                    LogMessage?.Invoke($"MQTT 재연결 시도 중... (시도 #{retryCount})");
                    await ConnectAsync();

                    if (_isConnected)
                    {
                        LogMessage?.Invoke($"✅ MQTT 재연결 성공! (총 {retryCount}회 시도)");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LogMessage?.Invoke($"재연결 실패 (시도 #{retryCount}): {ex.Message}");
                }
            }

            _isReconnecting = false;
        }

        public void Dispose()
        {
            _isDisposed = true;
            _isReconnecting = false;

            if (_mqttClient != null)
            {
                // 이벤트 핸들러 제거 (메모리 누수 방지)
                _mqttClient.ApplicationMessageReceivedAsync -= OnMessageReceivedAsync;
                _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;

                if (_mqttClient.IsConnected)
                {
                    // ConfigureAwait(false).GetAwaiter().GetResult()가 더 안전
                    _mqttClient.DisconnectAsync()
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
                _mqttClient.Dispose();
                _mqttClient = null;
            }

            // FileLogger 정리
            _fileLogger?.Dispose();

            // 이벤트 구독자 정리
            MessageReceived = null;
            ConnectionStateChanged = null;
            LogMessage = null;
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

        [JsonProperty("url")]
        public string? URL { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("timestamp")]
        public string? TimeStamp { get; set; }

        [JsonProperty("location")]
        public string? Location { get; set; }

        [JsonProperty("settingContent")]
        public string? SettingContent { get; set; }

    }
}
