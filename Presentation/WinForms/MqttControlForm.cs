using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared;
using AppLauncher.Shared.Services;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    public class MqttControlForm : Form
    {
        private MqttService _mqttService => ServiceContainer.MqttService!;
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

        /// <summary>
        /// Initializes the form's user interface and loads MQTT configuration, attaching the UI to the global MQTT service.
        /// </summary>
        public MqttControlForm()
        {
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

        /// <summary>
        /// Loads MQTT settings from configuration, updates the form's broker/client/topic labels, and attaches to the global MQTT service.
        /// </summary>
        /// <remarks>
        /// Loads configuration via ConfigManager, stores the result in the form's _settings, updates
        /// brokerInfoLabel, clientIdLabel, and topicLabel using the service's ClientId, and then
        /// subscribes to the global MQTT service events by calling AttachToExistingService.
        /// Any exceptions encountered during loading are logged via AddLog with the prefix "❌ 설정 로드 오류".
        /// </remarks>
        private void LoadSettings()
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                _settings = config.MqttSettings;

                brokerInfoLabel.Text = $"브로커: {_settings.Broker}:{_settings.Port}";
                string clientId = _mqttService.ClientId;
                clientIdLabel.Text = $"클라이언트 ID: {clientId}";
                topicLabel.Text = $"구독 토픽: device/{clientId}/commands";

                // 전역 MQTT 서비스에 연결
                AttachToExistingService();
            }
            catch (Exception ex)
            {
                AddLog($"❌ 설정 로드 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// Subscribes the form to the global MQTT service events and updates the UI to reflect the service's current connection state.
        /// </summary>
        /// <remarks>
        /// Attaches handlers for connection state changes, log messages, and incoming messages, and records a log entry indicating the attachment.
        /// </remarks>
        private void AttachToExistingService()
        {
            // 이벤트 구독
            _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
            _mqttService.LogMessage += OnLogMessage;
            _mqttService.MessageReceived += OnMessageReceived;

            AddLog("전역 MQTT 서비스에 연결됨");

            // 현재 연결 상태 업데이트
            OnConnectionStateChanged(_mqttService.IsConnected);
        }

        /// <summary>
        /// Updates UI controls to reflect the current MQTT connection state.
        /// </summary>
        /// <param name="isConnected">`true` when the MQTT service is connected; `false` otherwise.</param>
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

        /// <summary>
        /// Initiates an MQTT connection in response to the Connect button being clicked.
        /// </summary>
        /// <remarks>
        /// Disables the Connect button and logs the attempt, then attempts to connect via the application's MQTT service.
        /// On success logs a confirmation message; on failure logs the error message and re-enables the Connect button.
        /// </remarks>
        private async void ConnectButton_Click(object? sender, EventArgs e)
        {
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

        /// <summary>
        /// Initiates an orderly MQTT disconnect, updates the connect/disconnect button states, and records progress and any errors to the log.
        /// </summary>
        /// <param name="sender">The control that raised the click event.</param>
        /// <param name="e">Click event arguments (unused).</param>
        private async void DisconnectButton_Click(object? sender, EventArgs e)
        {
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

        /// <summary>
        /// Initiates a full MQTT reconnection sequence when the Reconnect button is clicked.
        /// </summary>
        /// <remarks>
        /// Disables the reconnect button, logs the attempt, disconnects first if already connected (with a short delay),
        /// then attempts to connect. Logs success or failure and always re-enables the button when finished.
        /// </remarks>
        /// <param name="sender">The control that raised the event.</param>
        /// <param name="e">Event data for the click event.</param>
        private async void ReconnectButton_Click(object? sender, EventArgs e)
        {
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

        /// <summary>
        /// Detaches the form's MQTT event handlers when the form is closing so the form stops receiving updates; the global MQTT service instance is not disposed.
        /// </summary>
        /// <param name="e">Event data describing the form closing operation.</param>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 창이 닫힐 때 이벤트만 정리 (서비스는 유지)
            _mqttService.ConnectionStateChanged -= OnConnectionStateChanged;
            _mqttService.LogMessage -= OnLogMessage;
            _mqttService.MessageReceived -= OnMessageReceived;

            base.OnFormClosing(e);
        }
    }
}