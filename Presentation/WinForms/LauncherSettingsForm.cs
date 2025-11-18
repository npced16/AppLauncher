using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Presentation.WinForms
{
    public class LauncherSettingsForm : Form
    {
        private LauncherConfig _config;

        private TextBox targetExecutableTextBox;
        private TextBox workingDirectoryTextBox;
        private TextBox localVersionFileTextBox;

        private Button browseExecutableButton;
        private Button browseDirectoryButton;
        private Button resetButton;
        private Button saveButton;
        private Button cancelButton;
        private Label versionLabel;

        public LauncherSettingsForm()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "설정";
            this.Size = new Size(600, 370);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Target Executable Label
            var targetLabel = new Label
            {
                Text = "대상 실행 파일:",
                Location = new Point(20, 20),
                Size = new Size(120, 20)
            };
            this.Controls.Add(targetLabel);

            // Target Executable TextBox
            targetExecutableTextBox = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(450, 25)
            };
            this.Controls.Add(targetExecutableTextBox);

            // Browse Executable Button
            browseExecutableButton = new Button
            {
                Text = "찾아보기",
                Location = new Point(480, 43),
                Size = new Size(80, 27)
            };
            browseExecutableButton.Click += BrowseExecutableButton_Click;
            this.Controls.Add(browseExecutableButton);

            // Browse Directory Button
            browseDirectoryButton = new Button
            {
                Text = "찾아보기",
                Location = new Point(480, 108),
                Size = new Size(80, 27)
            };
            browseDirectoryButton.Click += BrowseDirectoryButton_Click;
            this.Controls.Add(browseDirectoryButton);

            // Local Version File Label
            var versionFileLabel = new Label
            {
                Text = "버전 파일:",
                Location = new Point(20, 150),
                Size = new Size(120, 20)
            };
            this.Controls.Add(versionFileLabel);

            // Local Version File TextBox
            localVersionFileTextBox = new TextBox
            {
                Location = new Point(20, 175),
                Size = new Size(450, 25),
                Text = "version.txt"
            };
            this.Controls.Add(localVersionFileTextBox);

            // Reset Button
            resetButton = new Button
            {
                Text = "기본값 초기화",
                Location = new Point(20, 250),
                Size = new Size(120, 35)
            };
            resetButton.Click += ResetButton_Click;
            this.Controls.Add(resetButton);

            // Save Button
            saveButton = new Button
            {
                Text = "저장",
                Location = new Point(380, 250),
                Size = new Size(80, 35)
            };
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel Button
            cancelButton = new Button
            {
                Text = "취소",
                Location = new Point(480, 250),
                Size = new Size(80, 35)
            };
            cancelButton.Click += (s, e) => this.Close();
            this.Controls.Add(cancelButton);

            // Version Label
            versionLabel = new Label
            {
                Text = $"런처 버전: {VersionInfo.LAUNCHER_VERSION}",
                Location = new Point(20, 300),
                Size = new Size(200, 20),
                ForeColor = Color.Gray
            };
            this.Controls.Add(versionLabel);
        }

        private void LoadSettings()
        {
            try
            {
                _config = ConfigManager.LoadConfig();

                // 현재 설정 표시
                targetExecutableTextBox.Text = _config.TargetExecutable ?? "";
                workingDirectoryTextBox.Text = _config.WorkingDirectory ?? "";
                localVersionFileTextBox.Text = _config.LocalVersionFile ?? "version.txt";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 로드 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BrowseExecutableButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "실행 파일 선택";
                dialog.Filter = "실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*";
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    targetExecutableTextBox.Text = dialog.FileName;
                }
            }
        }

        private void BrowseDirectoryButton_Click(object? sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "작업 디렉토리 선택";
                dialog.ShowNewFolderButton = true;

                if (!string.IsNullOrWhiteSpace(workingDirectoryTextBox.Text) && Directory.Exists(workingDirectoryTextBox.Text))
                {
                    dialog.SelectedPath = workingDirectoryTextBox.Text;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    workingDirectoryTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            try
            {
                // 실행 파일 경로 검증
                if (string.IsNullOrWhiteSpace(targetExecutableTextBox.Text))
                {
                    MessageBox.Show("실행 파일 경로를 입력해주세요.", "입력 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!File.Exists(targetExecutableTextBox.Text))
                {
                    var result = MessageBox.Show(
                        "지정한 실행 파일이 존재하지 않습니다.\n그래도 저장하시겠습니까?",
                        "파일 없음",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                // 작업 디렉토리 검증 (선택사항)
                if (!string.IsNullOrWhiteSpace(workingDirectoryTextBox.Text) && !Directory.Exists(workingDirectoryTextBox.Text))
                {
                    var result = MessageBox.Show(
                        "지정한 작업 디렉토리가 존재하지 않습니다.\n그래도 저장하시겠습니까?",
                        "디렉토리 없음",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning);

                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }

                // 설정 저장
                _config.TargetExecutable = targetExecutableTextBox.Text.Trim();
                _config.WorkingDirectory = workingDirectoryTextBox.Text.Trim();
                _config.LocalVersionFile = localVersionFileTextBox.Text.Trim();

                ConfigManager.SaveConfig(_config);

                MessageBox.Show("설정이 저장되었습니다.", "저장 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "모든 설정을 기본값으로 초기화하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                    "기본값 초기화",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // 기본값으로 초기화
                    ConfigManager.ResetToDefault();

                    // 설정 다시 로드
                    LoadSettings();

                    MessageBox.Show("설정이 기본값으로 초기화되었습니다.", "초기화 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"초기화 실패: {ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
