using System;
using System.Drawing;
using System.Windows.Forms;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    public class MqttSettingsForm : Form
    {
        private LauncherConfig _config = null!;

        private Label brokerLabel;
        private Label portLabel;
        private Label clientIdLabel;
        private Label locationLabel;
        private TextBox locationTextBox;
        private Button saveButton;
        private Button cancelButton;

        public MqttSettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "MQTT 설정";
            this.Size = new Size(500, 350);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Broker Label (Header)
            var brokerHeaderLabel = new Label
            {
                Text = "브로커 주소:",
                Location = new Point(20, 20),
                Size = new Size(120, 20)
            };
            this.Controls.Add(brokerHeaderLabel);

            // Broker Text (Read-only)
            brokerLabel = new Label
            {
                Text = "localhost",
                Location = new Point(150, 20),
                Size = new Size(300, 20),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };
            this.Controls.Add(brokerLabel);

            // Port Label (Header)
            var portHeaderLabel = new Label
            {
                Text = "포트:",
                Location = new Point(20, 50),
                Size = new Size(120, 20)
            };
            this.Controls.Add(portHeaderLabel);

            // Port Text (Read-only)
            portLabel = new Label
            {
                Text = "1883",
                Location = new Point(150, 50),
                Size = new Size(100, 20),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };
            this.Controls.Add(portLabel);

            // Client ID Label (Header)
            var clientIdHeaderLabel = new Label
            {
                Text = "클라이언트 ID:",
                Location = new Point(20, 80),
                Size = new Size(120, 20)
            };
            this.Controls.Add(clientIdHeaderLabel);

            // Client ID Text (Read-only)
            clientIdLabel = new Label
            {
                Text = "",
                Location = new Point(150, 80),
                Size = new Size(300, 20),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray,
                Font = new Font("Consolas", 9)
            };
            this.Controls.Add(clientIdLabel);

            // Location Label (Editable)
            var locationHeaderLabel = new Label
            {
                Text = "위치 (선택사항):",
                Location = new Point(20, 120),
                Size = new Size(120, 20)
            };
            this.Controls.Add(locationHeaderLabel);

            // Location TextBox
            locationTextBox = new TextBox
            {
                Location = new Point(150, 118),
                Size = new Size(300, 25)
            };
            this.Controls.Add(locationTextBox);

            // Info Label
            var infoLabel = new Label
            {
                Text = "* 브로커, 포트, 클라이언트 ID는 자동으로 설정됩니다.",
                Location = new Point(20, 160),
                Size = new Size(450, 40),
                ForeColor = Color.Gray,
                Font = new Font(Font.FontFamily, 8)
            };
            this.Controls.Add(infoLabel);

            // Save Button
            saveButton = new Button
            {
                Text = "저장",
                Location = new Point(280, 250),
                Size = new Size(80, 35)
            };
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel Button
            cancelButton = new Button
            {
                Text = "취소",
                Location = new Point(370, 250),
                Size = new Size(80, 35)
            };
            cancelButton.Click += (s, e) => this.Close();
            this.Controls.Add(cancelButton);
        }

        private void LoadCurrentSettings()
        {
            try
            {
                _config = ConfigManager.LoadConfig();

                if (_config.MqttSettings != null)
                {
                    // 브로커와 포트는 표시만 (편집 불가)
                    brokerLabel.Text = _config.MqttSettings.Broker;
                    portLabel.Text = _config.MqttSettings.Port.ToString();

                    // 클라이언트 ID (하드웨어 UUID) 표시
                    clientIdLabel.Text = HardwareInfo.GetHardwareUuid();

                    // 편집 가능한 필드들
                    locationTextBox.Text = _config.MqttSettings.Location ?? "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 로드 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // MQTT 설정 업데이트
                if (_config.MqttSettings == null)
                {
                    _config.MqttSettings = new MqttSettings();
                }

                // Location 설정
                _config.MqttSettings.Location = string.IsNullOrWhiteSpace(locationTextBox.Text) ? null : locationTextBox.Text.Trim();

                // 설정 저장
                ConfigManager.SaveConfig(_config);

                MessageBox.Show(
                    "설정이 저장되었습니다.\n\nMQTT 연결을 다시 시작하려면 MQTT 제어창에서 연결을 해제한 후 다시 연결해주세요.",
                    "저장 완료",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
