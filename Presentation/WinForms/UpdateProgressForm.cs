using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppLauncher.Features.VersionManagement;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    /// <summary>
    /// 전체화면 업데이트 진행 상태 표시 폼
    /// </summary>
    public class UpdateProgressForm : Form
    {
        private Label titleLabel = null!;
        private Label statusLabel = null!;
        private ProgressBar progressBar = null!;
        private Label detailLabel = null!;
        private PendingUpdate _pendingUpdate = null!;
        private LauncherConfig _config = null!;
        private bool _updateCompleted = false;
        private bool _updateSuccess = false;

        public UpdateProgressForm(PendingUpdate pendingUpdate, LauncherConfig config)
        {
            _pendingUpdate = pendingUpdate;
            _config = config;
            InitializeComponent();
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
                Text = "LabView 업데이트 중",
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
                // 상태 콜백
                Action<string> statusCallback = (status) =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => UpdateStatus(status)));
                    }
                    else
                    {
                        UpdateStatus(status);
                    }
                };

                // 설치 상태 콜백
                Action<string>? installStatusCallback = (status) =>
                {
                    if (InvokeRequired)
                    {
                        Invoke(new Action(() => UpdateInstallStatus(status)));
                    }
                    else
                    {
                        UpdateInstallStatus(status);
                    }
                };

                // 응답 콜백
                Action<string, string> sendStatusResponse = (status, message) =>
                {
                    Console.WriteLine($"[UPDATE_RESPONSE] {status}: {message}");
                };

                // LabViewUpdater 생성
                var updater = new LabViewUpdater(
                    _pendingUpdate.Command,
                    _config,
                    statusCallback,
                    installStatusCallback,
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
                    UpdateStatus("업데이트 실패");
                    statusLabel.ForeColor = Color.OrangeRed;

                    await Task.Delay(3000);
                }
            }
            catch (Exception ex)
            {
                _updateSuccess = false;
                UpdateStatus($"업데이트 오류: {ex.Message}");
                statusLabel.ForeColor = Color.Red;
                Console.WriteLine($"[UPDATE_ERROR] {ex.Message}");

                await Task.Delay(3000);
            }
            finally
            {
                _updateCompleted = true;

                // pending update 정리
                PendingUpdateManager.ClearPendingUpdate();

                // 업데이트 완료 후 컴퓨터 재시작
                if (InvokeRequired)
                {
                    Invoke(new Action(() => RestartComputer()));
                }
                else
                {
                    RestartComputer();
                }
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

                Console.WriteLine("[UPDATE] Restarting computer...");

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
                Console.WriteLine($"[UPDATE] Failed to restart computer: {ex.Message}");

                // 재시작 실패 시 그냥 폼만 닫기
                this.Close();
            }
        }

        private void UpdateStatus(string status)
        {
            statusLabel.Text = status;
            detailLabel.Text = $"업데이트 정보:\n버전: {_pendingUpdate.Command.Version}\n시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private void UpdateInstallStatus(string status)
        {
            if (status == "다운로드 중")
            {
                UpdateProgress(20);
            }
            else if (status == "압축 해제 중")
            {
                UpdateProgress(40);
            }
            else if (status == "설치 중")
            {
                UpdateProgress(60);
            }
            else if (status == "대기 중")
            {
                // 설치 완료 후 대기 중이면 80%
                if (progressBar.Value < 80)
                {
                    UpdateProgress(80);
                }
            }
        }

        private void UpdateProgress(int value)
        {
            if (value >= 0 && value <= 100)
            {
                progressBar.Value = value;
            }
        }
    }
}
