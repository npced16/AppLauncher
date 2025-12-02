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

        // 그룹 박스
        private GroupBox targetGroupBox;
        private GroupBox mqttGroupBox;
        private GroupBox versionGroupBox;

        // 툴팁
        private ToolTip toolTip;

        public LauncherSettingsForm()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "런처 설정";
            this.Size = new Size(620, 580);
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

            // ==================== 대상 실행 파일 그룹 박스 ====================
            targetGroupBox = new GroupBox
            {
                Text = " 대상 실행 파일 ",
                Location = new Point(20, currentY),
                Size = new Size(560, 95),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102)
            };

            var targetLabel = new Label
            {
                Text = "실행 파일 경로:",
                Location = new Point(15, 30),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            targetGroupBox.Controls.Add(targetLabel);

            targetExecutableTextBox = new TextBox
            {
                Location = new Point(15, 53),
                Size = new Size(425, 25),
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle
            };
            toolTip.SetToolTip(targetExecutableTextBox, "실행할 대상 프로그램의 전체 경로를 입력하세요");
            targetGroupBox.Controls.Add(targetExecutableTextBox);

            browseExecutableButton = new Button
            {
                Text = "찾아보기...",
                Location = new Point(448, 51),
                Size = new Size(95, 29),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(240, 240, 245),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            browseExecutableButton.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 200);
            browseExecutableButton.Click += BrowseExecutableButton_Click;
            toolTip.SetToolTip(browseExecutableButton, "실행 파일을 선택합니다");
            targetGroupBox.Controls.Add(browseExecutableButton);

            this.Controls.Add(targetGroupBox);
            currentY += 110;

            // ==================== MQTT 설정 그룹 박스 ====================
            mqttGroupBox = new GroupBox
            {
                Text = " MQTT 연결 설정 ",
                Location = new Point(20, currentY),
                Size = new Size(560, 200),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102)
            };

            // MQTT 브로커 주소
            var mqttBrokerLabel = new Label
            {
                Text = "브로커 주소:",
                Location = new Point(15, 30),
                Size = new Size(85, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            mqttGroupBox.Controls.Add(mqttBrokerLabel);

            mqttBrokerTextBox = new TextBox
            {
                Location = new Point(105, 28),
                Size = new Size(280, 25),
                Font = new Font("Segoe UI", 9),
                PlaceholderText = "예: localhost",
                BorderStyle = BorderStyle.FixedSingle
            };
            toolTip.SetToolTip(mqttBrokerTextBox, "MQTT 브로커 서버 주소 (예: localhost, 192.168.1.100)");
            mqttGroupBox.Controls.Add(mqttBrokerTextBox);

            // 포트 레이블 및 값
            var mqttPortHeaderLabel = new Label
            {
                Text = "포트:",
                Location = new Point(400, 30),
                Size = new Size(40, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            mqttGroupBox.Controls.Add(mqttPortHeaderLabel);

            mqttPortLabel = new Label
            {
                Text = "1883",
                Location = new Point(445, 30),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            toolTip.SetToolTip(mqttPortLabel, "MQTT 브로커 포트 번호");
            mqttGroupBox.Controls.Add(mqttPortLabel);

            // 위치 정보
            var locationLabel = new Label
            {
                Text = "위치 정보:",
                Location = new Point(15, 70),
                Size = new Size(85, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            mqttGroupBox.Controls.Add(locationLabel);

            locationTextBox = new TextBox
            {
                Location = new Point(105, 68),
                Size = new Size(438, 25),
                Font = new Font("Segoe UI", 9),
                PlaceholderText = "예: 원주 본사/101호",
                BorderStyle = BorderStyle.FixedSingle
            };
            toolTip.SetToolTip(locationTextBox, "설치 위치를 입력하세요 (선택사항)");
            mqttGroupBox.Controls.Add(locationTextBox);

            // 클라이언트 ID
            var mqttClientIdHeaderLabel = new Label
            {
                Text = "클라이언트 ID:",
                Location = new Point(15, 110),
                Size = new Size(85, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            mqttGroupBox.Controls.Add(mqttClientIdHeaderLabel);

            mqttClientIdLabel = new Label
            {
                Text = "",
                Location = new Point(105, 110),
                Size = new Size(438, 20),
                Font = new Font("Consolas", 9),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoEllipsis = true
            };
            toolTip.SetToolTip(mqttClientIdLabel, "하드웨어 기반 고유 식별자 (자동 생성)");
            mqttGroupBox.Controls.Add(mqttClientIdLabel);

            // 업데이트 요청 버튼
            requestUpdateButton = new Button
            {
                Text = "SW 업데이트 요청",
                Location = new Point(15, 148),
                Size = new Size(528, 38),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            requestUpdateButton.FlatAppearance.BorderSize = 0;
            requestUpdateButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 35, 51);
            requestUpdateButton.Click += RequestUpdateButton_Click;
            toolTip.SetToolTip(requestUpdateButton, "서버에 챔버 소프트웨어 업데이트를 요청합니다");
            mqttGroupBox.Controls.Add(requestUpdateButton);

            this.Controls.Add(mqttGroupBox);
            currentY += 215;

            // ==================== 버전 정보 그룹 박스 ====================
            versionGroupBox = new GroupBox
            {
                Text = " 버전 정보 ",
                Location = new Point(20, currentY),
                Size = new Size(560, 80),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 102)
            };

            var versionHeaderLabel = new Label
            {
                Text = "런처 버전:",
                Location = new Point(15, 30),
                Size = new Size(80, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            versionGroupBox.Controls.Add(versionHeaderLabel);

            versionLabel = new Label
            {
                Text = $"{VersionInfo.LAUNCHER_VERSION}",
                Location = new Point(100, 30),
                Size = new Size(150, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            versionGroupBox.Controls.Add(versionLabel);

            var targetAppHeaderLabel = new Label
            {
                Text = "챔버 SW 버전:",
                Location = new Point(270, 30),
                Size = new Size(100, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(50, 50, 50)
            };
            versionGroupBox.Controls.Add(targetAppHeaderLabel);

            targetAppVersionLabel = new Label
            {
                Text = "로드 중...",
                Location = new Point(375, 30),
                Size = new Size(170, 20),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            versionGroupBox.Controls.Add(targetAppVersionLabel);

            this.Controls.Add(versionGroupBox);
            currentY += 95;

            // ==================== 하단 버튼들 ====================
            var buttonY = currentY + 10;

            // 기본값 초기화 버튼
            resetButton = new Button
            {
                Text = "🔄 기본값 초기화",
                Location = new Point(20, buttonY),
                Size = new Size(130, 40),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            resetButton.FlatAppearance.BorderSize = 0;
            resetButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 98, 104);
            resetButton.Click += ResetButton_Click;
            toolTip.SetToolTip(resetButton, "모든 설정을 기본값으로 초기화합니다");
            this.Controls.Add(resetButton);

            // 취소 버튼
            cancelButton = new Button
            {
                Text = "취소",
                Location = new Point(390, buttonY),
                Size = new Size(90, 40),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            cancelButton.FlatAppearance.BorderSize = 0;
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(90, 98, 104);
            cancelButton.Click += (s, e) => this.Close();
            toolTip.SetToolTip(cancelButton, "변경사항을 저장하지 않고 닫습니다");
            this.Controls.Add(cancelButton);

            // 저장 버튼
            saveButton = new Button
            {
                Text = "💾 저장",
                Location = new Point(490, buttonY),
                Size = new Size(90, 40),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            saveButton.FlatAppearance.BorderSize = 0;
            saveButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(33, 136, 56);
            saveButton.Click += SaveButton_Click;
            toolTip.SetToolTip(saveButton, "설정을 저장하고 닫습니다");
            this.Controls.Add(saveButton);
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
