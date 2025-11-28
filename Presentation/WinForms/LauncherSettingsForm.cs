using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;
using AppLauncher.Shared.Services;

namespace AppLauncher.Presentation.WinForms
{
    public class LauncherSettingsForm : Form
    {
        private LauncherConfig _config;

        private TextBox targetExecutableTextBox;
        private TextBox locationTextBox;

        private Button browseExecutableButton;
        private Button resetButton;
        private Button saveButton;
        private Button cancelButton;
        private Button requestUpdateButton;
        private Label versionLabel;
        private Label targetAppVersionLabel;

        // MQTT 정보
        private TextBox mqttBrokerTextBox;
        private Label mqttPortLabel;
        private Label mqttClientIdLabel;

        public LauncherSettingsForm()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "설정";
            this.Size = new Size(600, 350);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Target Executable Label
            var targetLabel = new Label
            {
                Text = "대상 실행 파일:",
                Location = new Point(20, 20),
                Size = new Size(120, 20)
            };
            this.Controls.Add(targetLabel);

            // Target Executable TextBox
            targetExecutableTextBox = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(450, 25)
            };
            this.Controls.Add(targetExecutableTextBox);

            // Browse Executable Button
            browseExecutableButton = new Button
            {
                Text = "찾아보기",
                Location = new Point(480, 43),
                Size = new Size(80, 27)
            };
            browseExecutableButton.Click += BrowseExecutableButton_Click;
            this.Controls.Add(browseExecutableButton);

            // === MQTT 정보 섹션 ===
            var mqttSectionLabel = new Label
            {
                Text = "MQTT 정보",
                Location = new Point(20, 85),
                Size = new Size(540, 20),
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            this.Controls.Add(mqttSectionLabel);

            // MQTT 브로커 주소
            var mqttBrokerHeaderLabel = new Label
            {
                Text = "브로커 주소:",
                Location = new Point(30, 115),
                Size = new Size(100, 20)
            };
            this.Controls.Add(mqttBrokerHeaderLabel);

            mqttBrokerTextBox = new TextBox
            {
                Location = new Point(140, 113),
                Size = new Size(320, 25), // 기존보다 줄임
                PlaceholderText = "예: localhost 또는 192.168.1.100"
            };
            this.Controls.Add(mqttBrokerTextBox);

            // 포트 (브로커 주소 오른쪽 배치)
            var mqttPortHeaderLabel = new Label
            {
                Text = "포트:",
                Location = new Point(470, 115),
                Size = new Size(40, 20)
            };
            this.Controls.Add(mqttPortHeaderLabel);

            mqttPortLabel = new Label
            {
                Text = _config?.MqttSettings?.Port.ToString() ?? "1883",
                Location = new Point(510, 115),
                Size = new Size(50, 20),
                ForeColor = Color.Gray
            };
            this.Controls.Add(mqttPortLabel);


            // Location (MQTT)
            var locationLabel = new Label
            {
                Text = "위치:",
                Location = new Point(30, 150),
                Size = new Size(100, 20)
            };
            this.Controls.Add(locationLabel);

            locationTextBox = new TextBox
            {
                Location = new Point(140, 150),
                Size = new Size(420, 25),
                PlaceholderText = "예: 원주 본사/101호"
            };
            this.Controls.Add(locationTextBox);

            // MQTT 클라이언트 ID
            var mqttClientIdHeaderLabel = new Label
            {
                Text = "클라이언트 ID:",
                Location = new Point(30, 180),
                Size = new Size(100, 20)
            };
            this.Controls.Add(mqttClientIdHeaderLabel);

            mqttClientIdLabel = new Label
            {
                Text = "",
                Location = new Point(140, 180),
                Size = new Size(420, 20),
                Font = new Font("Consolas", 9),
                ForeColor = Color.Gray
            };
            this.Controls.Add(mqttClientIdLabel);

            // Request Update Button
            requestUpdateButton = new Button
            {
                Text = "SW 업데이트 요청",
                Location = new Point(150, 210),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(215, 0, 120),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            requestUpdateButton.FlatAppearance.BorderSize = 0;
            requestUpdateButton.Click += RequestUpdateButton_Click;
            this.Controls.Add(requestUpdateButton);

            // Reset Button
            resetButton = new Button
            {
                Text = "기본값 초기화",
                Location = new Point(20, 210),
                Size = new Size(120, 35)
            };
            resetButton.Click += ResetButton_Click;
            this.Controls.Add(resetButton);

            // Save Button
            saveButton = new Button
            {
                Text = "저장",
                Location = new Point(380, 210),
                Size = new Size(80, 35),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel Button
            cancelButton = new Button
            {
                Text = "취소",
                Location = new Point(480, 210),
                Size = new Size(80, 35)
            };
            cancelButton.Click += (s, e) => this.Close();
            this.Controls.Add(cancelButton);

            // Version Section
            var versionHeaderLabel = new Label
            {
                Text = "런처 버전:",
                Location = new Point(20, 250),
                Size = new Size(70, 20),
                ForeColor = Color.Gray
            };
            this.Controls.Add(versionHeaderLabel);

            versionLabel = new Label
            {
                Text = $"{VersionInfo.LAUNCHER_VERSION}",
                Location = new Point(90, 250),
                Size = new Size(100, 20),
                ForeColor = Color.Gray
            };
            this.Controls.Add(versionLabel);

            var targetAppHeaderLabel = new Label
            {
                Text = "챔버 SW 버전:",
                Location = new Point(210, 250),
                Size = new Size(90, 20),
                ForeColor = Color.Gray
            };
            this.Controls.Add(targetAppHeaderLabel);

            targetAppVersionLabel = new Label
            {
                Text = "로드 중...",
                Location = new Point(300, 250),
                Size = new Size(100, 20),
                ForeColor = Color.Gray
            };
            this.Controls.Add(targetAppVersionLabel);
        }

        private void LoadSettings()
        {
            try
            {
                _config = ConfigManager.LoadConfig();

                // 현재 설정 표시
                targetExecutableTextBox.Text = _config.TargetExecutable ?? "";
                locationTextBox.Text = _config.MqttSettings?.Location ?? "";

                // MQTT 정보 표시
                mqttBrokerTextBox.Text = _config.MqttSettings?.Broker ?? "localhost";
                mqttPortLabel.Text = _config.MqttSettings?.Port.ToString() ?? "1883";
                mqttClientIdLabel.Text = HardwareInfo.GetHardwareUuid();

                // 챔버 소프트웨어 버전 로드
                LoadTargetAppVersion();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 로드 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadTargetAppVersion()
        {
            try
            {
                if (!string.IsNullOrEmpty(_config.LocalVersionFile) && File.Exists(_config.LocalVersionFile))
                {
                    string version = File.ReadAllText(_config.LocalVersionFile).Trim();
                    targetAppVersionLabel.Text = version;
                }
                else
                {
                    targetAppVersionLabel.Text = "알 수 없음";
                }
            }
            catch
            {
                targetAppVersionLabel.Text = "로드 실패";
            }
        }

        private void BrowseExecutableButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "실행 파일 선택";
                dialog.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    targetExecutableTextBox.Text = dialog.FileName;
                }
            }
        }



        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // 실행 파일 경로 검증
                if (string.IsNullOrWhiteSpace(targetExecutableTextBox.Text))
                {
                    MessageBox.Show("실행 파일 경로를 입력해주세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!File.Exists(targetExecutableTextBox.Text))
                {
                    var result = MessageBox.Show(
                        "지정한 실행 파일이 존재하지 않습니다.\n그래도 저장하시겠습니까?",
                        "파일 없음",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                // 설정 저장
                _config.TargetExecutable = targetExecutableTextBox.Text.Trim();

                // 브로커 주소 변경 확인
                string oldBroker = _config.MqttSettings?.Broker ?? "localhost";
                string newBroker = string.IsNullOrWhiteSpace(mqttBrokerTextBox.Text) ? "localhost" : mqttBrokerTextBox.Text.Trim();
                bool brokerChanged = oldBroker != newBroker;

                // MQTT 설정 저장
                if (_config.MqttSettings != null)
                {
                    _config.MqttSettings.Location = string.IsNullOrWhiteSpace(locationTextBox.Text) ? null : locationTextBox.Text.Trim();
                    _config.MqttSettings.Broker = newBroker;
                }

                ConfigManager.SaveConfig(_config);

                // MQTT 브로커 주소가 변경된 경우 서비스 재시작
                if (brokerChanged)
                {
                    try
                    {
                        // ServiceContainer 재초기화 (새로운 브로커 주소로 연결)
                        ServiceContainer.Dispose();
                        ServiceContainer.Initialize(_config);

                        MessageBox.Show(
                            $"설정이 저장되었습니다.\nMQTT 브로커 주소가 변경되어 서비스를 재시작했습니다.\n새 주소: {newBroker}",
                            "저장 완료",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
                            $"설정은 저장되었으나 MQTT 서비스 재시작 실패: {ex.Message}",
                            "경고",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("설정이 저장되었습니다.", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "모든 설정을 기본값으로 초기화하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                    "기본값 초기화",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // 기본값으로 초기화
                    ConfigManager.ResetToDefault();

                    // 설정 다시 로드
                    LoadSettings();

                    MessageBox.Show("설정이 기본값으로 초기화되었습니다.", "초기화 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"초기화 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void RequestUpdateButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // MQTT 연결 확인
                if (ServiceContainer.MqttMessageHandler == null || ServiceContainer.MqttService == null || !ServiceContainer.MqttService.IsConnected)
                {
                    MessageBox.Show(
                        "MQTT가 연결되지 않았습니다.\nMQTT 제어 센터에서 연결 상태를 확인해주세요.",
                        "연결 오류",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    "서버에 챔버 소프트웨어 업데이트를 요청하시겠습니까?",
                    "업데이트 요청",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 버튼 비활성화
                    requestUpdateButton.Enabled = false;
                    requestUpdateButton.Text = "요청 중...";

                    // 업데이트 요청
                    await ServiceContainer.MqttMessageHandler.RequestLabViewUpdate("사용자 수동 요청");

                    MessageBox.Show(
                        "업데이트 요청을 전송했습니다.\n서버에서 업데이트 명령을 보낼 때까지 기다려주세요.",
                        "요청 완료",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // 버튼 다시 활성화
                    requestUpdateButton.Enabled = true;
                    requestUpdateButton.Text = "업데이트 요청";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"업데이트 요청 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                requestUpdateButton.Enabled = true;
                requestUpdateButton.Text = "업데이트 요청";
            }
        }
    }
}
