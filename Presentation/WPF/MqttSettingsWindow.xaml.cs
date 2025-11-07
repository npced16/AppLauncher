using System;
using System.Windows;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WPF
{
    public partial class MqttSettingsWindow : Window
    {
        private LauncherConfig _config = null!;

        public MqttSettingsWindow()
        {
            InitializeComponent();
            Loaded += MqttSettingsWindow_Loaded;
        }

        private void MqttSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                _config = ConfigManager.LoadConfig();

                if (_config.MqttSettings != null)
                {
                    BrokerTextBox.Text = _config.MqttSettings.Broker ?? "localhost";
                    PortTextBox.Text = _config.MqttSettings.Port.ToString();
                    ClientIdTextBox.Text = _config.MqttSettings.ClientId ?? "AppLauncher";
                    TopicTextBox.Text = _config.MqttSettings.Topic ?? "applauncher/commands";
                    UsernameTextBox.Text = _config.MqttSettings.Username ?? "";
                    PasswordBox.Password = _config.MqttSettings.Password ?? "";
                }
                else
                {
                    // 기본값 설정
                    BrokerTextBox.Text = "localhost";
                    PortTextBox.Text = "1883";
                    ClientIdTextBox.Text = "AppLauncher";
                    TopicTextBox.Text = "applauncher/commands";
                    UsernameTextBox.Text = "";
                    PasswordBox.Password = "";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 입력값 검증
                if (string.IsNullOrWhiteSpace(BrokerTextBox.Text))
                {
                    MessageBox.Show("브로커 주소를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BrokerTextBox.Focus();
                    return;
                }

                if (!int.TryParse(PortTextBox.Text, out int port) || port <= 0 || port > 65535)
                {
                    MessageBox.Show("올바른 포트 번호를 입력해주세요. (1-65535)", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    PortTextBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(ClientIdTextBox.Text))
                {
                    MessageBox.Show("클라이언트 ID를 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ClientIdTextBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(TopicTextBox.Text))
                {
                    MessageBox.Show("토픽을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    TopicTextBox.Focus();
                    return;
                }

                // MQTT 설정 업데이트
                if (_config.MqttSettings == null)
                {
                    _config.MqttSettings = new MqttSettings();
                }

                _config.MqttSettings.Broker = BrokerTextBox.Text.Trim();
                _config.MqttSettings.Port = port;
                _config.MqttSettings.ClientId = ClientIdTextBox.Text.Trim();
                _config.MqttSettings.Topic = TopicTextBox.Text.Trim();
                _config.MqttSettings.Username = string.IsNullOrWhiteSpace(UsernameTextBox.Text) ? null : UsernameTextBox.Text.Trim();
                _config.MqttSettings.Password = string.IsNullOrWhiteSpace(PasswordBox.Password) ? null : PasswordBox.Password;

                // 설정 저장
                ConfigManager.SaveConfig(_config);

                MessageBox.Show(
                    "설정이 저장되었습니다.\n\nMQTT 연결을 다시 시작하려면 MQTT 제어창에서 연결을 해제한 후 다시 연결해주세요.",
                    "저장 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
