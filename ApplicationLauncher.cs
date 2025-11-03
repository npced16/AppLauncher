using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace AppLauncher
{
    public class ApplicationLauncher
    {
        private readonly MainWindow _window;

        public ApplicationLauncher(MainWindow window)
        {
            _window = window;
        }

        public async Task CheckAndLaunchAsync(LauncherConfig config)
        {
            try
            {
                // 1. 버전 체크
                _window.UpdateStatus("버전 확인 중...");
                _window.UpdateProgress(5);

                var versionChecker = new VersionChecker(
                    config.VersionCheckUrl,
                    config.LocalVersionFile
                );

                var versionResult = await versionChecker.CheckVersionAsync();

                await Task.Delay(500); // UI 표시를 위한 짧은 지연

                // 2. 업데이트 필요 시 업데이트 실행
                if (versionResult.IsUpdateRequired)
                {
                    _window.UpdateStatus($"새 버전 발견: {versionResult.RemoteVersion}");
                    await Task.Delay(1000);

                    var updater = new Updater(
                        config.UpdateDownloadUrl,
                        config.TargetDirectory,
                        config.LocalVersionFile,
                        _window
                    );

                    bool updateSuccess = await updater.UpdateAsync(versionResult.RemoteVersion);

                    if (!updateSuccess)
                    {
                        _window.UpdateStatus("업데이트 실패. 기존 버전으로 실행합니다.", isError: true);
                        await Task.Delay(2000);
                    }
                }
                else
                {
                    _window.UpdateStatus(versionResult.Message);
                    _window.UpdateProgress(50);
                    await Task.Delay(500);
                }

                // 3. 대상 프로그램 실행
                _window.UpdateStatus("프로그램 실행 중...");
                _window.UpdateProgress(90);
                await Task.Delay(500);

                bool launchSuccess = LaunchTargetApplication(config.TargetExecutable, config.WorkingDirectory);

                if (launchSuccess)
                {
                    _window.UpdateStatus("프로그램 실행 완료!");
                    _window.UpdateProgress(100);
                    await Task.Delay(1000);

                    // 4. 런처 종료
                    Application.Current.Shutdown();
                }
                else
                {
                    _window.UpdateStatus("프로그램 실행 실패", isError: true);
                    await Task.Delay(3000);
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                _window.UpdateStatus($"오류 발생: {ex.Message}", isError: true);
                await Task.Delay(3000);
                Application.Current.Shutdown();
            }
        }

        private bool LaunchTargetApplication(string executable, string? workingDirectory)
        {
            try
            {
                if (!File.Exists(executable))
                {
                    _window.UpdateStatus($"실행 파일을 찾을 수 없습니다: {executable}", isError: true);
                    return false;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                };

                if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
                {
                    startInfo.WorkingDirectory = workingDirectory;
                }

                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                _window.UpdateStatus($"실행 오류: {ex.Message}", isError: true);
                return false;
            }
        }
    }
}
