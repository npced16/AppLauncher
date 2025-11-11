using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Features.AppLaunching
{
    public class ApplicationLauncher
    {
        public ApplicationLauncher()
        {
        }

        /// <summary>
        /// 백그라운드에서 대상 프로그램 실행 (자동 버전 체크 제거)
        /// </summary>
        public async Task CheckAndLaunchInBackgroundAsync(LauncherConfig config, Action<string> statusCallback)
        {
            try
            {
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
            catch (Exception ex)
            {
                statusCallback($"오류 발생: {ex.Message}");
                await Task.Delay(3000);
            }
        }

        private bool LaunchTargetApplication(string executable, string? workingDirectory, Action<string> statusCallback)
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

                // workingDirectory가 없으면 실행 파일이 있는 디렉토리를 자동으로 사용
                string finalWorkingDir = workingDirectory ?? "";
                if (string.IsNullOrWhiteSpace(finalWorkingDir))
                {
                    finalWorkingDir = Path.GetDirectoryName(executable) ?? "";
                }

                if (!string.IsNullOrWhiteSpace(finalWorkingDir) && Directory.Exists(finalWorkingDir))
                {
                    startInfo.WorkingDirectory = finalWorkingDir;
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
