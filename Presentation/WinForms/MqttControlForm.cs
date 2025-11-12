using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    public class MqttControlForm : Form
    {
        private MqttService? _mqttService;
        private MqttSettings? _settings;

        private Label connectionStatusLabel;
        private Label brokerInfoLabel;
        private Label clientIdLabel;
        private Label topicLabel;
        private TextBox logTextBox;
        private Button connectButton;
        private Button disconnectButton;
        private Button reconnectButton;
        private Button settingsButton;
        private Button clearLogButton;
        private Button closeButton;

        public MqttControlForm(MqttService? mqttService = null)
        {
            _mqttService = mqttService;
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "MQTT 제어 센터";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Connection Status Label
            connectionStatusLabel = new Label
            {
                Text = "연결 안됨",
                Location = new Point(20, 20),
                Size = new Size(150, 25),
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                ForeColor = Color.Red
            };
            this.Controls.Add(connectionStatusLabel);

            // Broker Info Label
            brokerInfoLabel = new Label
            {
                Text = "브로커: ",
                Location = new Point(20, 50),
                Size = new Size(400, 20)
            };
            this.Controls.Add(brokerInfoLabel);

            // Client ID Label
            clientIdLabel = new Label
            {
                Text = "클라이언트 ID: ",
                Location = new Point(20, 75),
                Size = new Size(400, 20)
            };
            this.Controls.Add(clientIdLabel);

            // Topic Label
            topicLabel = new Label
            {
                Text = "구독 토픽: ",
                Location = new Point(20, 100),
                Size = new Size(400, 20)
            };
            this.Controls.Add(topicLabel);

            // Connect Button
            connectButton = new Button
            {
                Text = "연결",
                Location = new Point(20, 130),
                Size = new Size(100, 35)
            };
            connectButton.Click += ConnectButton_Click;
            this.Controls.Add(connectButton);

            // Disconnect Button
            disconnectButton = new Button
            {
                Text = "연결 해제",
                Location = new Point(130, 130),
                Size = new Size(100, 35),
                Enabled = false
            };
            disconnectButton.Click += DisconnectButton_Click;
            this.Controls.Add(disconnectButton);

            // Reconnect Button
            reconnectButton = new Button
            {
                Text = "재연결",
                Location = new Point(240, 130),
                Size = new Size(100, 35)
            };
            reconnectButton.Click += ReconnectButton_Click;
            this.Controls.Add(reconnectButton);

            // Settings Button
            settingsButton = new Button
            {
                Text = "설정",
                Location = new Point(350, 130),
                Size = new Size(100, 35)
            };
            settingsButton.Click += SettingsButton_Click;
            this.Controls.Add(settingsButton);

            // Log Label
            var logLabel = new Label
            {
                Text = "로그:",
                Location = new Point(20, 180),
                Size = new Size(100, 20)
            };
            this.Controls.Add(logLabel);

            // Log TextBox
            logTextBox = new TextBox
            {
                Location = new Point(20, 205),
                Size = new Size(640, 280),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Text = "로그가 여기에 표시됩니다...",
                Font = new Font("Consolas", 9)
            };
            this.Controls.Add(logTextBox);

            // Clear Log Button
            clearLogButton = new Button
            {
                Text = "로그 지우기",
                Location = new Point(460, 495),
                Size = new Size(100, 35)
            };
            clearLogButton.Click += (s, e) => logTextBox.Text = "로그가 여기에 표시됩니다...";
            this.Controls.Add(clearLogButton);

            // Close Button
            closeButton = new Button
            {
                Text = "닫기",
                Location = new Point(570, 495),
                Size = new Size(90, 35)
            };
            closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(closeButton);
        }

        private void LoadSettings()
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                _settings = config.MqttSettings;

                if (_settings != null)
                {
                    brokerInfoLabel.Text = $"브로커: {_settings.Broker}:{_settings.Port}";
                    string clientId = _mqttService?.ClientId ?? HardwareInfo.GetHardwareUuid();
                    clientIdLabel.Text = $"클라이언트 ID: {clientId}";
                    topicLabel.Text = $"구독 토픽: device/{clientId}/commands";

                    if (_mqttService != null)
                    {
                        // 기존 서비스 사용
                        AttachToExistingService();
                    }
                    else
                    {
                        // 새 서비스 생성
                        InitializeMqttService();
                    }
                }
                else
                {
                    AddLog("MQTT 설정을 찾을 수 없습니다.");
                    connectButton.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 설정 로드 오류: {ex.Message}");
            }
        }

        private void AttachToExistingService()
        {
            if (_mqttService == null) return;

            // 이벤트 구독
            _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
            _mqttService.LogMessage += OnLogMessage;
            _mqttService.MessageReceived += OnMessageReceived;

            AddLog("기존 MQTT 서비스에 연결됨");

            // 현재 연결 상태 업데이트
            OnConnectionStateChanged(_mqttService.IsConnected);
        }

        private void InitializeMqttService()
        {
            if (_settings == null) return;

            // 하드웨어 UUID를 ClientId로 사용
            string clientId = HardwareInfo.GetHardwareUuid();
            _mqttService = new MqttService(_settings, clientId);

            // 이벤트 구독
            _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
            _mqttService.LogMessage += OnLogMessage;
            _mqttService.MessageReceived += OnMessageReceived;

            AddLog("새 MQTT 서비스 초기화 완료");
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<bool>(OnConnectionStateChanged), isConnected);
                return;
            }

            if (isConnected)
            {
                connectionStatusLabel.Text = "연결됨";
                connectionStatusLabel.ForeColor = Color.Green;
                connectButton.Enabled = false;
                disconnectButton.Enabled = true;
            }
            else
            {
                connectionStatusLabel.Text = "연결 안됨";
                connectionStatusLabel.ForeColor = Color.Red;
                connectButton.Enabled = true;
                disconnectButton.Enabled = false;
            }
        }

        private void OnLogMessage(string message)
        {
            AddLog(message);
        }

        private void OnMessageReceived(MqttMessage message)
        {
            AddLog($"[메시지 수신] 토픽: {message.Topic}");

            // JSON 파싱 시도 후 예쁘게 출력
            try
            {
                var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(message.Payload);
                string formattedJson = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                AddLog($"  내용 (JSON):");
                AddLog(formattedJson);
            }
            catch
            {
                // JSON이 아닌 경우 원본 그대로 출력
                AddLog($"  내용: {message.Payload}");
            }
        }

        private void AddLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AddLog), message);
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logEntry = $"[{timestamp}] {message}\r\n";

            if (logTextBox.Text == "로그가 여기에 표시됩니다...")
            {
                logTextBox.Text = logEntry;
            }
            else
            {
                logTextBox.AppendText(logEntry);
            }

            // 자동 스크롤
            logTextBox.SelectionStart = logTextBox.Text.Length;
            logTextBox.ScrollToCaret();
        }

        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
            if (_mqttService == null)
            {
                AddLog("❌ MQTT 서비스가 초기화되지 않았습니다.");
                return;
            }

            try
            {
                connectButton.Enabled = false;
                AddLog("MQTT 연결 시도 중...");

                await _mqttService.ConnectAsync();

                AddLog("✅ MQTT 연결 성공!");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 연결 실패: {ex.Message}");
                connectButton.Enabled = true;
            }
        }

        private async void DisconnectButton_Click(object? sender, EventArgs e)
        {
            if (_mqttService == null) return;

            try
            {
                disconnectButton.Enabled = false;
                AddLog("MQTT 연결 해제 중...");

                await _mqttService.DisconnectAsync();

                AddLog("MQTT 연결 해제 완료");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 연결 해제 오류: {ex.Message}");
            }
            finally
            {
                disconnectButton.Enabled = true;
            }
        }

        private async void ReconnectButton_Click(object? sender, EventArgs e)
        {
            if (_mqttService == null)
            {
                AddLog("❌ MQTT 서비스가 초기화되지 않았습니다.");
                return;
            }

            try
            {
                reconnectButton.Enabled = false;
                AddLog("MQTT 재연결 시도 중...");

                // 연결 해제
                if (_mqttService.IsConnected)
                {
                    await _mqttService.DisconnectAsync();
                    await Task.Delay(500);
                }

                // 재연결
                await _mqttService.ConnectAsync();

                AddLog("✅ MQTT 재연결 성공!");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 재연결 실패: {ex.Message}");
            }
            finally
            {
                reconnectButton.Enabled = true;
            }
        }

        private void SettingsButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var settingsForm = new MqttSettingsForm();
                settingsForm.ShowDialog(this);

                // 설정이 변경되었을 수 있으므로 다시 로드
                var config = ConfigManager.LoadConfig();
                _settings = config.MqttSettings;

                if (_settings != null)
                {
                    brokerInfoLabel.Text = $"브로커: {_settings.Broker}:{_settings.Port}";
                    string clientId = _mqttService?.ClientId ?? HardwareInfo.GetHardwareUuid();
                    clientIdLabel.Text = $"클라이언트 ID: {clientId}";
                    topicLabel.Text = $"구독 토픽: device/{clientId}/commands";
                    AddLog("설정이 업데이트되었습니다. 재연결이 필요할 수 있습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 설정 창 오류: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 창이 닫힐 때 이벤트만 정리 (서비스는 유지)
            if (_mqttService != null)
            {
                _mqttService.ConnectionStateChanged -= OnConnectionStateChanged;
                _mqttService.LogMessage -= OnLogMessage;
                _mqttService.MessageReceived -= OnMessageReceived;
            }

            base.OnFormClosing(e);
        }
    }
}
