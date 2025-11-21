using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    public class MainForm : Form
    {
        private LauncherConfig _config = null!;
        private Label statusLabel = null!;
        private ProgressBar progressBar = null!;

        public MainForm()
        {
            InitializeComponent();
            LoadConfig();

            // 폼이 표시되면 자동으로 앱 실행
            this.Shown += async (s, e) => await LaunchApplication();
        }

        private void InitializeComponent()
        {
            this.Text = "App Launcher";
            this.Size = new Size(500, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Title Label
            var titleLabel = new Label
            {
                Text = "App Launcher",
                Location = new Point(20, 20),
                Size = new Size(450, 30),
                Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            this.Controls.Add(titleLabel);

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new Point(20, 80),
                Size = new Size(450, 30),
                Visible = true,
                Style = ProgressBarStyle.Continuous,
                Value = 0
            };
            this.Controls.Add(progressBar);

            // Status Label
            statusLabel = new Label
            {
                Text = "실행 준비 중...",
                Location = new Point(20, 130),
                Size = new Size(450, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Blue,
                Font = new Font(Font.FontFamily, 10, FontStyle.Regular)
            };
            this.Controls.Add(statusLabel);
        }

        private void LoadConfig()
        {
            try
            {
                _config = ConfigManager.LoadConfig();

                if (string.IsNullOrWhiteSpace(_config.TargetExecutable))
                {
                    statusLabel.Text = "설정에서 대상 앱을 지정해주세요";
                    statusLabel.ForeColor = Color.Red;
                }
                else if (!File.Exists(_config.TargetExecutable))
                {
                    statusLabel.Text = "대상 파일이 존재하지 않습니다";
                    statusLabel.ForeColor = Color.Orange;
                }
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"설정 로드 실패: {ex.Message}";
                statusLabel.ForeColor = Color.Red;
            }
        }

        private async System.Threading.Tasks.Task LaunchApplication()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_config.TargetExecutable) || !File.Exists(_config.TargetExecutable))
                {
                    statusLabel.Text = "실행할 파일이 존재하지 않습니다";
                    statusLabel.ForeColor = Color.Red;
                    await System.Threading.Tasks.Task.Delay(2000);
                    this.Close();
                    return;
                }

                // UI 업데이트: 진행 시작
                progressBar.Visible = true;
                progressBar.Value = 0;

                // 1단계: 준비
                statusLabel.Text = "실행 준비 중...";
                statusLabel.ForeColor = Color.Blue;
                progressBar.Value = 25;
                await System.Threading.Tasks.Task.Delay(300);

                // 2단계: 프로세스 설정
                statusLabel.Text = "프로세스 설정 중...";
                progressBar.Value = 50;

                var startInfo = new ProcessStartInfo
                {
                    FileName = _config.TargetExecutable,
                };

                // 작업 디렉토리는 실행 파일의 디렉토리로 자동 설정
                string workingDir = Path.GetDirectoryName(_config.TargetExecutable) ?? "";
                if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                await System.Threading.Tasks.Task.Delay(200);

                // 3단계: 앱 실행
                statusLabel.Text = "앱 실행 중...";
                progressBar.Value = 75;
                await System.Threading.Tasks.Task.Delay(200);

                var process = Process.Start(startInfo);

                // 4단계: 완료
                progressBar.Value = 100;
                statusLabel.Text = "실행 완료!";
                statusLabel.ForeColor = Color.Green;

                // 앱이 실행되면 1초 후 창 닫기
                await System.Threading.Tasks.Task.Delay(1000);
                this.Close();
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                statusLabel.Text = "실행 실패";
                statusLabel.ForeColor = Color.Red;
                MessageBox.Show($"실행 오류: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

                await System.Threading.Tasks.Task.Delay(2000);
                this.Close();
            }
        }
    }
}
