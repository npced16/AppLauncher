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
        private Button closeButton;

        public MqttSettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "MQTT 정보";
            this.Size = new Size(500, 300);
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

            // Location Label (Header)
            var locationHeaderLabel = new Label
            {
                Text = "위치:",
                Location = new Point(20, 120),
                Size = new Size(120, 20)
            };
            this.Controls.Add(locationHeaderLabel);

            // Location Text (Read-only)
            locationLabel = new Label
            {
                Text = "",
                Location = new Point(150, 120),
                Size = new Size(300, 20),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };
            this.Controls.Add(locationLabel);

            // Info Label
            var infoLabel = new Label
            {
                Text = "* 위치는 일반 설정 창에서 수정할 수 있습니다.",
                Location = new Point(20, 160),
                Size = new Size(450, 40),
                ForeColor = Color.Gray,
                Font = new Font(Font.FontFamily, 8)
            };
            this.Controls.Add(infoLabel);

            // Close Button
            closeButton = new Button
            {
                Text = "닫기",
                Location = new Point(370, 210),
                Size = new Size(80, 35)
            };
            closeButton.Click += (s, e) => this.Close();
            this.Controls.Add(closeButton);
        }

        /// <summary>
        /// Loads the launcher configuration and applies MQTT-related values to the form's read-only labels.
        /// </summary>
        /// <remarks>
        /// Updates brokerLabel and portLabel from the configuration, sets clientIdLabel to the hardware UUID, and sets locationLabel to the configured location or "미설정" when the location is null. If loading fails, an error message box is shown.
        /// </remarks>
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

                // Location 표시 (읽기 전용)
                locationLabel.Text = _config.MqttSettings.Location ?? "미설정";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 로드 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}