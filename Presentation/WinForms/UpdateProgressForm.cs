using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.MqttControl;
using AppLauncher.Features.VersionManagement;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    /// <summary>
    /// 전체화면 업데이트 진행 상태 표시 폼
    /// </summary>
    public class UpdateProgressForm : Form
    {
        private static void Log(string message) => DebugLogger.Log("UPDATE", message);

        private Label titleLabel = null!;
        private Label statusLabel = null!;
        private ProgressBar progressBar = null!;
        private Label detailLabel = null!;
        private LaunchCommand _command = null!;
        private LauncherConfig _config = null!;
        private bool _updateCompleted = false;
        private bool _updateSuccess = false;
        private Timer? _progressTimer;
        private int _targetProgress = 0;

        public UpdateProgressForm(LaunchCommand command, LauncherConfig config)
        {
            _command = command;
            _config = config;
            InitializeComponent();
            InitializeProgressAnimation();
        }

        private void InitializeProgressAnimation()
        {
            _progressTimer = new Timer
            {
                Interval = 20 // 20ms마다 업데이트 (부드러운 애니메이션)
            };
            _progressTimer.Tick += ProgressTimer_Tick;
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (progressBar.Value < _targetProgress)
            {
                // 현재 값과 목표 값의 차이에 따라 증가량 조정
                int diff = _targetProgress - progressBar.Value;
                int increment = Math.Max(1, diff / 10); // 최소 1, 최대 차이의 1/10씩 증가

                progressBar.Value = Math.Min(progressBar.Value + increment, _targetProgress);
            }
            else if (progressBar.Value > _targetProgress)
            {
                progressBar.Value = _targetProgress;
            }
            else
            {
                _progressTimer?.Stop();
            }
        }

        private void InitializeComponent()
        {
            // 전체화면 설정
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;

            // 타이틀 레이블
            titleLabel = new Label
            {
                Text = "챔버 소프트웨어 업데이트 중",
                Font = new Font("Segoe UI", 32, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 100
            };
            this.Controls.Add(titleLabel);

            // 중앙 패널
            var centerPanel = new Panel
            {
                Width = 800,
                Height = 300,
                BackColor = Color.FromArgb(45, 45, 48)
            };

            // 프로그레스바
            progressBar = new ProgressBar
            {
                Location = new Point(50, 80),
                Size = new Size(700, 40),
                Style = ProgressBarStyle.Continuous,
                Value = 0,
                Maximum = 100
            };
            centerPanel.Controls.Add(progressBar);

            // 상태 레이블
            statusLabel = new Label
            {
                Text = "업데이트 준비 중...",
                Location = new Point(50, 30),
                Size = new Size(700, 40),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.LightBlue,
                TextAlign = ContentAlignment.MiddleLeft
            };
            centerPanel.Controls.Add(statusLabel);

            // 상세 정보 레이블
            detailLabel = new Label
            {
                Text = "",
                Location = new Point(50, 140),
                Size = new Size(700, 100),
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.LightGray,
                TextAlign = ContentAlignment.TopLeft
            };
            centerPanel.Controls.Add(detailLabel);

            // 중앙 패널을 화면 중앙에 배치
            this.Resize += (s, e) =>
            {
                centerPanel.Left = (this.ClientSize.Width - centerPanel.Width) / 2;
                centerPanel.Top = (this.ClientSize.Height - centerPanel.Height) / 2 + 50;
            };

            this.Controls.Add(centerPanel);

            // ESC 키로 취소 방지
            this.KeyPreview = true;
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                }
            };

            // Alt+F4 방지
            this.FormClosing += (s, e) =>
            {
                if (!_updateCompleted)
                {
                    e.Cancel = true;
                }
            };
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 폼이 표시되면 업데이트 시작
            _ = StartUpdateAsync();
        }

        private async Task StartUpdateAsync()
        {
            try
            {
                // 응답 콜백
                Action<string, string> sendStatusResponse = (status, message) =>
                {
                    UpdateInstallStatus(status);

                };

                // LabViewUpdater 생성
                var updater = new LabViewUpdater(
                    _command,
                    _config,
                    sendStatusResponse
                );

                // 업데이트 시작
                UpdateStatus("다운로드 및 설치 시작...");
                UpdateProgress(10);

                string result = await updater.DownloadAndExecuteAsync();

                // 업데이트 완료
                if (!string.IsNullOrEmpty(result))
                {
                    _updateSuccess = true;
                    UpdateProgress(100);
                    UpdateStatus("업데이트 완료!");
                    statusLabel.ForeColor = Color.LightGreen;

                    await Task.Delay(2000);
                }
                else
                {
                    _updateSuccess = false;
                    UpdateStatus("업데이트 실패 - 기존 프로그램 실행");
                    statusLabel.ForeColor = Color.OrangeRed;

                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                _updateSuccess = false;
                UpdateStatus($"업데이트 오류: {ex.Message}");
                statusLabel.ForeColor = Color.Red;
                Log($"Error: {ex.Message}");

                await Task.Delay(2000);
            }
            finally
            {
                _updateCompleted = true;

                // pending update 정리
                PendingUpdateManager.ClearPendingUpdate();

                if (_updateSuccess)
                {
                    // 업데이트 성공 시 컴퓨터 재시작
                    RestartComputer();
                }
                else
                {
                    // 업데이트 실패 시 기존 프로그램 실행
                    StartExistingApp();
                }
            }
        }

        /// <summary>
        /// 업데이트 실패 시 런처 재시작 (LabVIEW 앱 + TrayApp 정상 실행)
        /// </summary>
        private async void StartExistingApp()
        {
            try
            {
                UpdateStatus("런처를 재시작합니다...");
                statusLabel.ForeColor = Color.LightBlue;

                Log("Restarting launcher after update failure");

                await Task.Delay(2000);
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        this.Close();
                        Application.Restart();
                        Environment.Exit(0);
                    }));
                }
                else
                {
                    this.Close();
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to restart launcher: {ex.Message}");

                this.Close();
            }
        }

        /// <summary>
        /// 컴퓨터 재시작
        /// </summary>
        private void RestartComputer()
        {
            try
            {
                UpdateStatus("컴퓨터를 재시작합니다...");
                statusLabel.ForeColor = Color.LightBlue;

                Log("Restarting computer...");

                // shutdown 명령어로 컴퓨터 재시작 (10초 후)
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "shutdown",
                    Arguments = "/r /t 10 /c \"LabView 업데이트가 완료되었습니다. 10초 후 재시작됩니다.\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                System.Diagnostics.Process.Start(startInfo);

                // 폼 닫기
                Task.Delay(2000).ContinueWith(_ =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => this.Close()));
                    }
                    else
                    {
                        this.Close();
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"Failed to restart computer: {ex.Message}");

                // 재시작 실패 시 그냥 폼만 닫기
                this.Close();
            }
        }

        private void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateStatus(status)));
                return;
            }

            statusLabel.Text = status;

            // 현재 챔버 소프트웨어 버전 가져오기
            string currentVersion = GetCurrentVersion();
            string targetAppVersionInfo = string.IsNullOrEmpty(currentVersion)
                ? $"{_command.Version}"
                : $"{currentVersion} → {_command.Version}";

            detailLabel.Text = $"업데이트 정보:\n런처 버전: {VersionInfo.LAUNCHER_VERSION}\n챔버 소프트웨어 버전: {targetAppVersionInfo}\n시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private string GetCurrentVersion()
        {
            try
            {
                if (!string.IsNullOrEmpty(_config.LocalVersionFile) && System.IO.File.Exists(_config.LocalVersionFile))
                {
                    return System.IO.File.ReadAllText(_config.LocalVersionFile).Trim();
                }
            }
            catch
            {
                // 버전 파일 읽기 실패 시 빈 문자열 반환
            }
            return "";
        }

        private void UpdateInstallStatus(string status)
        {
            switch (status)
            {
                case "download_started":
                    UpdateStatus("설치 파일 다운로드 시작...");
                    UpdateProgress(10);
                    break;

                case "download_complete":
                    UpdateStatus("설치 파일 다운로드 완료");
                    UpdateProgress(30);
                    break;

                case "extract_start":
                    UpdateStatus("압축 해제 중...");
                    UpdateProgress(40);
                    break;

                case "extract_done":
                    UpdateStatus("압축 해제 완료");
                    UpdateProgress(50);
                    break;

                case "installation_start":
                    UpdateStatus("설치 중...");
                    UpdateProgress(60);
                    break;

                case "installation_complete":
                    UpdateStatus("설치 완료");
                    UpdateProgress(70);
                    break;

                case "restore_start":
                    UpdateStatus("환경 설정 중...");
                    UpdateProgress(75);
                    break;

                case "restore_done":
                    UpdateStatus("환경 설정 완료");
                    UpdateProgress(85);
                    break;

                case "version_saved":
                    UpdateStatus("설치 완료");
                    UpdateProgress(95);
                    break;

                default:
                    Log($"Unknown status: {status}");
                    break;
            }
        }

        private void UpdateProgress(int value)
        {
            if (value >= 0 && value <= 100)
            {
                _targetProgress = value;
                _progressTimer?.Start();
            }
        }
    }
}
