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
        private Button closeButton;

        public MqttSettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "MQTT 정보";
            this.Size = new Size(500, 200);
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


            // Close Button
            closeButton = new Button
            {
                Text = "닫기",
                Location = new Point(370, 120),
                Size = new Size(80, 35)
            };
            closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(closeButton);
        }

        private void LoadCurrentSettings()
        {
            try
            {
                _config = ConfigManager.LoadConfig();

                // 브로커와 포트 표시
                brokerLabel.Text = _config.MqttSettings.Broker;
                portLabel.Text = _config.MqttSettings.Port.ToString();

                // 클라이언트 ID (하드웨어 UUID) 표시
                clientIdLabel.Text = HardwareInfo.GetHardwareUuid();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 로드 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
