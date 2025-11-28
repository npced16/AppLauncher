using System;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private Button clearLogButton;
        private Button closeButton;

        // 그룹 박스
        private GroupBox statusGroupBox;
        private GroupBox controlGroupBox;
        private GroupBox logGroupBox;

        // 툴팁
        private ToolTip toolTip;

        public MqttControlForm()
        {
            InitializeComponent();
            LoadSettings();
            LoadTodayLogFile();
        }

        private void InitializeComponent()
        {
            this.Text = "MQTT 제어 센터";
            this.Size = new Size(780, 860);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(240, 240, 245);

            // 툴팁 초기화
            toolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 300,
                ReshowDelay = 100,
                ShowAlways = true
            };

            int currentY = 20;

            // ==================== 연결 상태 그룹 박스 ====================
            statusGroupBox = new GroupBox
            {
                Text = " 연결 정보 ",
                Location = new Point(20, currentY),
                Size = new Size(720, 135),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102)
            };

            // 연결 상태 표시
            var statusHeaderLabel = new Label
            {
                Text = "상태:",
                Location = new Point(15, 30),
                Size = new Size(60, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50),
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusGroupBox.Controls.Add(statusHeaderLabel);

            connectionStatusLabel = new Label
            {
                Text = "● 연결 안됨",
                Location = new Point(80, 30),
                Size = new Size(150, 25),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 53, 69),
                TextAlign = ContentAlignment.MiddleLeft
            };
            statusGroupBox.Controls.Add(connectionStatusLabel);

            // 브로커 정보
            var brokerHeaderLabel = new Label
            {
                Text = "브로커:",
                Location = new Point(15, 63),
                Size = new Size(60, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            statusGroupBox.Controls.Add(brokerHeaderLabel);

            brokerInfoLabel = new Label
            {
                Text = "로드 중...",
                Location = new Point(80, 63),
                Size = new Size(620, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            statusGroupBox.Controls.Add(brokerInfoLabel);

            // 클라이언트 ID
            var clientIdHeaderLabel = new Label
            {
                Text = "ID:",
                Location = new Point(15, 88),
                Size = new Size(60, 22),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            statusGroupBox.Controls.Add(clientIdHeaderLabel);

            clientIdLabel = new Label
            {
                Text = "로드 중...",
                Location = new Point(80, 88),
                Size = new Size(620, 22),
                Font = new Font("Consolas", 9.5f),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoEllipsis = true
            };
            statusGroupBox.Controls.Add(clientIdLabel);

            // 구독 토픽
            var topicHeaderLabel = new Label
            {
                Text = "토픽:",
                Location = new Point(15, 113),
                Size = new Size(60, 22),
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            statusGroupBox.Controls.Add(topicHeaderLabel);

            topicLabel = new Label
            {
                Text = "로드 중...",
                Location = new Point(80, 113),
                Size = new Size(620, 22),
                Font = new Font("Consolas", 9.5f),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoEllipsis = true
            };
            statusGroupBox.Controls.Add(topicLabel);

            this.Controls.Add(statusGroupBox);
            currentY += 150;

            // ==================== 제어 버튼 그룹 박스 ====================
            controlGroupBox = new GroupBox
            {
                Text = " 연결 제어 ",
                Location = new Point(20, currentY),
                Size = new Size(720, 75),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102)
            };

            // 연결 버튼
            connectButton = new Button
            {
                Text = "연결",
                Location = new Point(15, 28),
                Size = new Size(115, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            connectButton.FlatAppearance.BorderSize = 0;
            connectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 136, 56);
            connectButton.Click += ConnectButton_Click;
            toolTip.SetToolTip(connectButton, "MQTT 브로커에 연결합니다");
            controlGroupBox.Controls.Add(connectButton);

            // 연결 해제 버튼
            disconnectButton = new Button
            {
                Text = "연결 해제",
                Location = new Point(140, 28),
                Size = new Size(115, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            disconnectButton.FlatAppearance.BorderSize = 0;
            disconnectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 35, 51);
            disconnectButton.Click += DisconnectButton_Click;
            toolTip.SetToolTip(disconnectButton, "MQTT 연결을 해제합니다");
            controlGroupBox.Controls.Add(disconnectButton);

            // 재연결 버튼
            reconnectButton = new Button
            {
                Text = "재연결",
                Location = new Point(265, 28),
                Size = new Size(115, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            reconnectButton.FlatAppearance.BorderSize = 0;
            reconnectButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 100, 195);
            reconnectButton.Click += ReconnectButton_Click;
            toolTip.SetToolTip(reconnectButton, "MQTT 연결을 다시 시도합니다");
            controlGroupBox.Controls.Add(reconnectButton);

            this.Controls.Add(controlGroupBox);
            currentY += 90;

            // ==================== 로그 그룹 박스 ====================
            logGroupBox = new GroupBox
            {
                Text = " 실시간 로그 ",
                Location = new Point(20, currentY),
                Size = new Size(720, 515),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102)
            };

            // 로그 텍스트박스
            logTextBox = new TextBox
            {
                Location = new Point(15, 28),
                Size = new Size(690, 430),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220),
                Text = "로그가 여기에 표시됩니다...",
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            toolTip.SetToolTip(logTextBox, "MQTT 메시지 및 이벤트 로그");
            logGroupBox.Controls.Add(logTextBox);

            // 로그 지우기 버튼
            clearLogButton = new Button
            {
                Text = "🗑️ 로그 지우기",
                Location = new Point(15, 468),
                Size = new Size(125, 35),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            clearLogButton.FlatAppearance.BorderSize = 0;
            clearLogButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 98, 104);
            clearLogButton.Click += (s, e) => logTextBox.Text = "로그가 여기에 표시됩니다...";
            toolTip.SetToolTip(clearLogButton, "화면의 로그를 지웁니다");
            logGroupBox.Controls.Add(clearLogButton);

            // 닫기 버튼
            closeButton = new Button
            {
                Text = "✖️ 닫기",
                Location = new Point(600, 468),
                Size = new Size(105, 35),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 98, 104);
            closeButton.Click += (s, e) => this.Close();
            toolTip.SetToolTip(closeButton, "제어 센터를 닫습니다 (연결은 유지됨)");
            logGroupBox.Controls.Add(closeButton);

            this.Controls.Add(logGroupBox);
        }

        private void LoadSettings()
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                _settings = config.MqttSettings;

                brokerInfoLabel.Text = $"브로커: {_settings.Broker}:{_settings.Port}";
                string clientId = _mqttService.ClientId;
                clientIdLabel.Text = $"{clientId}";
                topicLabel.Text = $"device/{clientId}/commands";

                // 전역 MQTT 서비스에 연결
                AttachToExistingService();
            }
            catch (Exception ex)
            {
                AddLog($"❌ 설정 로드 오류: {ex.Message}");
            }
        }

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

        private void OnConnectionStateChanged(bool isConnected)
        {
            // Form이 이미 Dispose된 경우 무시
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action<bool>(OnConnectionStateChanged), isConnected);
                }
                catch (ObjectDisposedException)
                {
                    // Form이 닫히는 중이면 무시
                }
                return;
            }

            try
            {
                if (isConnected)
                {
                    connectionStatusLabel.Text = "● 연결됨";
                    connectionStatusLabel.ForeColor = Color.FromArgb(40, 167, 69);
                    connectButton.Enabled = false;
                    disconnectButton.Enabled = true;
                }
                else
                {
                    connectionStatusLabel.Text = "● 연결 안됨";
                    connectionStatusLabel.ForeColor = Color.FromArgb(220, 53, 69);
                    connectButton.Enabled = true;
                    disconnectButton.Enabled = false;
                }
            }
            catch (ObjectDisposedException)
            {
                // Form이 닫히는 중이면 무시
            }
        }

        private void OnLogMessage(string message)
        {
            AddLog(message);
        }

        private void OnMessageReceived(MqttMessage message)
        {
            // Form이 이미 Dispose된 경우 무시
            if (IsDisposed)
                return;

            AddLog($"[메시지 수신] 토픽: {message.Topic}");

            // JSON 파싱 시도 후 예쁘게 출력
            try
            {
                var jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(message.Payload);
                string formattedJson = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
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
            // Form이나 TextBox가 이미 Dispose된 경우 무시
            if (IsDisposed || logTextBox.IsDisposed)
                return;

            if (InvokeRequired)
            {
                try
                {
                    Invoke(new Action<string>(AddLog), message);
                }
                catch (ObjectDisposedException)
                {
                    // Form이 닫히는 중이면 무시
                }
                return;
            }

            try
            {
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
            catch (ObjectDisposedException)
            {
                // Form이 닫히는 중이면 무시
            }
        }

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

        /// <summary>
        /// 오늘의 로그 파일을 불러와서 표시
        /// </summary>
        private void LoadTodayLogFile()
        {
            try
            {
                // 로그 파일 경로 (C:\ProgramData\AppLauncher\Logs\MQTT_YYYYMMDD.log)
                string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string logDirectory = Path.Combine(programDataPath, "AppLauncher", "Logs");
                string logFileName = $"MQTT_{DateTime.Now:yyyyMMdd}.log";
                string logFilePath = Path.Combine(logDirectory, logFileName);

                if (File.Exists(logFilePath))
                {
                    // 파일 내용 읽기 (마지막 500줄만)
                    var lines = File.ReadAllLines(logFilePath);
                    int startIndex = Math.Max(0, lines.Length - 500);
                    var recentLines = lines.Skip(startIndex);

                    logTextBox.Text = string.Join("\r\n", recentLines);
                    logTextBox.SelectionStart = logTextBox.Text.Length;
                    logTextBox.ScrollToCaret();

                    AddLog($"--- 로그 파일 로드 완료 (최근 {recentLines.Count()}줄) ---");
                }
                else
                {
                    AddLog("--- 오늘의 로그 파일이 아직 없습니다 ---");
                }
            }
            catch (Exception ex)
            {
                AddLog($"--- 로그 파일 로드 오류: {ex.Message} ---");
            }
        }

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
