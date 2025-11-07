using System;
using System.Threading.Tasks;
using System.Windows;
using AppLauncher.Shared.Configuration;
using AppLauncher.Features.AppLaunching;

namespace AppLauncher.Presentation.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigManager.LoadConfig();
                var launcher = new ApplicationLauncher(this);
                await launcher.CheckAndLaunchAsync(config);
            }
            catch (Exception ex)
            {
                UpdateStatus($"오류 발생: {ex.Message}", isError: true);
                await Task.Delay(3000);
                Application.Current.Shutdown();
            }
        }

        public void UpdateStatus(string message, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                if (isError)
                {
                    StatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red color
                }
            });
        }

        public void UpdateProgress(int percentage, string text = "")
        {
            Dispatcher.Invoke(() =>
            {
                ProgressBar.Value = percentage;
                ProgressText.Text = string.IsNullOrEmpty(text) ? $"{percentage}%" : text;
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // 창만 닫고 앱은 트레이에 유지
            Close();
        }
    }
}
