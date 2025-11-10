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
                    // 브로커와 포트는 표시만 (편집 불가)
                    BrokerText.Text = _config.MqttSettings.Broker;
                    PortText.Text = _config.MqttSettings.Port.ToString();

                    // 편집 가능한 필드들
                    ClientIdTextBox.Text = _config.MqttSettings.ClientId;
                    TopicTextBox.Text = _config.MqttSettings.Topic;
                    UsernameTextBox.Text = _config.MqttSettings.Username ?? "";
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
                // 입력값 검증 (브로커와 포트는 읽기 전용이므로 검증 불필요)
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

                // MQTT 설정 업데이트 (브로커와 포트는 제외)
                if (_config.MqttSettings == null)
                {
                    _config.MqttSettings = new MqttSettings();
                }

                // 브로커와 포트는 변경하지 않음 (읽기 전용)
                _config.MqttSettings.ClientId = ClientIdTextBox.Text.Trim();
                _config.MqttSettings.Topic = TopicTextBox.Text.Trim();
                _config.MqttSettings.Username = string.IsNullOrWhiteSpace(UsernameTextBox.Text) ? null : UsernameTextBox.Text.Trim();

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
