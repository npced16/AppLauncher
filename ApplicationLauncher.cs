using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace AppLauncher
{
    public class ApplicationLauncher
    {
        private readonly MainWindow? _window;

        public ApplicationLauncher(MainWindow? window)
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

                // 2. 업데이트 필요 시 EXE 다운로드 및 실행
                if (versionResult.IsUpdateRequired)
                {
                    _window!.UpdateStatus($"새 버전 발견: {versionResult.RemoteVersion}");
                    await Task.Delay(1000);

                    if (string.IsNullOrEmpty(versionResult.DownloadUrl))
                    {
                        _window.UpdateStatus("다운로드 URL이 없습니다.", isError: true);
                        await Task.Delay(2000);
                    }
                    else
                    {
                        // ZIP이 아닌 EXE를 다운로드하고 실행
                        _window.UpdateStatus("업데이트 파일 다운로드 중...");
                        _window.UpdateProgress(30);

                        var updater = new BackgroundUpdater(
                            versionResult.DownloadUrl,
                            config.LocalVersionFile,
                            (status) => _window.UpdateStatus(status)
                        );

                        string? exePath = await updater.DownloadAndGetExePathAsync(versionResult.RemoteVersion);

                        if (exePath != null)
                        {
                            _window.UpdateStatus("업데이트 프로그램 실행 중...");
                            _window.UpdateProgress(80);
                            LaunchTargetApplication(exePath, config.WorkingDirectory);
                            _window.UpdateProgress(100);
                            await Task.Delay(1000);
                            Application.Current.Shutdown();
                            return;
                        }
                        else
                        {
                            _window.UpdateStatus("다운로드 실패. 기존 버전으로 실행합니다.", isError: true);
                            await Task.Delay(2000);
                        }
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

        public async Task CheckAndLaunchInBackgroundAsync(LauncherConfig config, Action<string> statusCallback)
        {
            try
            {
                // 1. 버전 체크
                statusCallback("버전 확인 중...");

                var versionChecker = new VersionChecker(
                    config.VersionCheckUrl,
                    config.LocalVersionFile
                );

                var versionResult = await versionChecker.CheckVersionAsync();

                // 2. 업데이트 필요 시 EXE 다운로드 및 실행
                if (versionResult.IsUpdateRequired)
                {
                    statusCallback($"새 버전 발견: {versionResult.RemoteVersion}");
                    await Task.Delay(1000);

                    if (string.IsNullOrEmpty(versionResult.DownloadUrl))
                    {
                        statusCallback("다운로드 URL이 없습니다. 기존 버전으로 실행합니다.");
                        await Task.Delay(2000);

                        // 기존 프로그램 실행
                        LaunchTargetApplication(config.TargetExecutable, config.WorkingDirectory, statusCallback);
                        return;
                    }

                    var updater = new BackgroundUpdater(
                        versionResult.DownloadUrl,
                        config.LocalVersionFile,
                        statusCallback
                    );

                    string? exePath = await updater.DownloadAndGetExePathAsync(versionResult.RemoteVersion);

                    if (exePath == null)
                    {
                        statusCallback("다운로드 실패. 기존 버전으로 실행합니다.");
                        await Task.Delay(2000);

                        // 기존 프로그램 실행
                        LaunchTargetApplication(config.TargetExecutable, config.WorkingDirectory, statusCallback);
                        return;
                    }

                    // 다운로드한 EXE 실행
                    statusCallback("업데이트 프로그램 실행 중...");
                    await Task.Delay(500);

                    bool launchSuccess = LaunchExecutable(exePath, config.WorkingDirectory, statusCallback);

                    if (launchSuccess)
                    {
                        statusCallback("업데이트 프로그램 실행 완료!");
                        await Task.Delay(1000);
                    }
                    else
                    {
                        statusCallback("업데이트 프로그램 실행 실패");
                        await Task.Delay(3000);
                    }
                }
                else
                {
                    statusCallback(versionResult.Message);
                    await Task.Delay(500);

                    // 3. 최신 버전이면 대상 프로그램 실행
                    statusCallback("프로그램 실행 중...");
                    await Task.Delay(500);

                    bool launchSuccess = LaunchTargetApplication(config.TargetExecutable, config.WorkingDirectory, statusCallback);

                    if (launchSuccess)
                    {
                        statusCallback("프로그램 실행 완료!");
                        await Task.Delay(1000);
                    }
                    else
                    {
                        statusCallback("프로그램 실행 실패");
                        await Task.Delay(3000);
                    }
                }
            }
            catch (Exception ex)
            {
                statusCallback($"오류 발생: {ex.Message}");
                await Task.Delay(3000);
            }
        }

        private bool LaunchTargetApplication(string executable, string? workingDirectory)
        {
            try
            {
                if (!File.Exists(executable))
                {
                    _window?.UpdateStatus($"실행 파일을 찾을 수 없습니다: {executable}", isError: true);
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
                _window?.UpdateStatus($"실행 오류: {ex.Message}", isError: true);
                return false;
            }
        }

        private bool LaunchTargetApplication(string executable, string? workingDirectory, Action<string> statusCallback)
        {
            return LaunchExecutable(executable, workingDirectory, statusCallback);
        }

        private bool LaunchExecutable(string executable, string? workingDirectory, Action<string> statusCallback)
        {
            try
            {
                if (!File.Exists(executable))
                {
                    statusCallback($"실행 파일을 찾을 수 없습니다: {executable}");
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
                statusCallback($"실행 오류: {ex.Message}");
                return false;
            }
        }
    }
}
