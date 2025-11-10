using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WPF
{
    public partial class MqttControlWindow : Window
    {
        private MqttService? _mqttService;
        private MqttSettings? _settings;

        // 외부에서 MqttService를 주입받을 수 있도록
        public MqttControlWindow(MqttService? mqttService = null)
        {
            InitializeComponent();
            _mqttService = mqttService;
            Loaded += MqttControlWindow_Loaded;
        }

        private void MqttControlWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                _settings = config.MqttSettings;

                if (_settings != null)
                {
                    BrokerInfoText.Text = $"브로커: {_settings.Broker}:{_settings.Port}";
                    ClientIdText.Text = $"클라이언트 ID: {_settings.ClientId}";
                    TopicText.Text = $"구독 토픽: {_settings.Topic}";

                    if (_mqttService != null)
                    {
                        // 기존 서비스 사용
                        AttachToExistingService();
                    }
                    else
                    {
                        // 새 서비스 생성 (트레이에서 실행하지 않은 경우)
                        InitializeMqttService();
                    }
                }
                else
                {
                    AddLog("MQTT 설정을 찾을 수 없습니다.");
                    ConnectButton.IsEnabled = false;
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

            _mqttService = new MqttService(_settings);

            // 이벤트 구독
            _mqttService.ConnectionStateChanged += OnConnectionStateChanged;
            _mqttService.LogMessage += OnLogMessage;
            _mqttService.MessageReceived += OnMessageReceived;

            AddLog("새 MQTT 서비스 초기화 완료");
        }

        private void OnConnectionStateChanged(bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                if (isConnected)
                {
                    ConnectionStatusText.Text = "연결됨";
                    ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94)); // Green
                    ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));

                    ConnectButton.IsEnabled = false;
                    DisconnectButton.IsEnabled = true;
                }
                else
                {
                    ConnectionStatusText.Text = "연결 안됨";
                    ConnectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                    ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));

                    ConnectButton.IsEnabled = true;
                    DisconnectButton.IsEnabled = false;
                }
            });
        }

        private void OnLogMessage(string message)
        {
            AddLog(message);
        }

        private void OnMessageReceived(MqttMessage message)
        {
            AddLog($"[메시지 수신] 토픽: {message.Topic}");
            AddLog($"  내용: {message.Payload}");
        }

        private void AddLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}\n";

                if (LogTextBox.Text == "로그가 여기에 표시됩니다...")
                {
                    LogTextBox.Text = logEntry;
                }
                else
                {
                    LogTextBox.Text += logEntry;
                }

                // 자동 스크롤
                var scrollViewer = FindScrollViewer(LogTextBox);
                scrollViewer?.ScrollToEnd();
            });
        }

        private ScrollViewer? FindScrollViewer(DependencyObject obj)
        {
            if (obj == null) return null;

            var parent = VisualTreeHelper.GetParent(obj);
            if (parent is ScrollViewer scrollViewer)
                return scrollViewer;

            return FindScrollViewer(parent);
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mqttService == null)
            {
                AddLog("❌ MQTT 서비스가 초기화되지 않았습니다.");
                return;
            }

            try
            {
                ConnectButton.IsEnabled = false;
                AddLog("MQTT 연결 시도 중...");

                await _mqttService.ConnectAsync();

                AddLog("✅ MQTT 연결 성공!");
            }
            catch (Exception ex)
            {
                AddLog($"❌ 연결 실패: {ex.Message}");
                ConnectButton.IsEnabled = true;
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mqttService == null) return;

            try
            {
                DisconnectButton.IsEnabled = false;
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
                DisconnectButton.IsEnabled = true;
            }
        }

        private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mqttService == null)
            {
                AddLog("❌ MQTT 서비스가 초기화되지 않았습니다.");
                return;
            }

            try
            {
                ReconnectButton.IsEnabled = false;
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
                ReconnectButton.IsEnabled = true;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new MqttSettingsWindow();
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();

                // 설정이 변경되었을 수 있으므로 다시 로드
                var config = ConfigManager.LoadConfig();
                _settings = config.MqttSettings;

                if (_settings != null)
                {
                    BrokerInfoText.Text = $"브로커: {_settings.Broker}:{_settings.Port}";
                    ClientIdText.Text = $"클라이언트 ID: {_settings.ClientId}";
                    TopicText.Text = $"구독 토픽: {_settings.Topic}";
                    AddLog("설정이 업데이트되었습니다. 재연결이 필요할 수 있습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"❌ 설정 창 오류: {ex.Message}");
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Text = "로그가 여기에 표시됩니다...";
        }

        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 창이 닫힐 때 이벤트만 정리 (서비스는 유지)
            if (_mqttService != null)
            {
                _mqttService.ConnectionStateChanged -= OnConnectionStateChanged;
                _mqttService.LogMessage -= OnLogMessage;
                _mqttService.MessageReceived -= OnMessageReceived;
            }

            base.OnClosing(e);
        }
    }
}
