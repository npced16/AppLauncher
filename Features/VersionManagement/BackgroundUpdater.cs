using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using AppLauncher.Shared.Configuration;
namespace AppLauncher.Features.VersionManagement
{
    public class BackgroundUpdater
    {
        private readonly string _downloadUrl;
        private readonly string _versionFile;

        public BackgroundUpdater(string downloadUrl, string versionFile)
        {
            _downloadUrl = downloadUrl;
            _versionFile = versionFile;
        }

        /// <summary>
        /// 실행 중인 프로세스를 종료합니다
        /// </summary>
        public bool TryKillRunningProcess(string exePath)
        {
            try
            {
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                    return false;

                string processName = Path.GetFileNameWithoutExtension(exePath);
                string fullPath = Path.GetFullPath(exePath);

                var runningProcesses = Process.GetProcessesByName(processName)
                    .Where(p =>
                    {
                        try
                        {
                            return p.MainModule?.FileName != null &&
                                   Path.GetFullPath(p.MainModule.FileName).Equals(fullPath, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToList();

                if (runningProcesses.Count == 0)
                    return true; // 실행 중이 아님

                foreach (var process in runningProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(5000); // 5초 대기
                    }
                    catch
                    {
                        // 프로세스 종료 실패 무시
                    }
                }

                Task.Delay(1000).Wait(); // 프로세스가 완전히 종료될 때까지 대기

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> DownloadAndReplaceExeAsync(string newVersion, string targetExePath)
        {
            try
            {
                // 임시 디렉토리 생성
                string tempDir = Path.Combine(Path.GetTempPath(), "AppLauncherUpdate");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                // EXE 파일 다운로드
                string fileName = Path.GetFileName(new Uri(_downloadUrl).LocalPath);
                if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    fileName = "update.exe";

                string downloadPath = Path.Combine(tempDir, fileName);
                bool downloadSuccess = await DownloadFileAsync(_downloadUrl, downloadPath);

                if (!downloadSuccess)
                {
                    return null;
                }

                // 대상 파일이 존재하고 실행 중이면 종료
                if (File.Exists(targetExePath))
                {
                    TryKillRunningProcess(targetExePath);

                    // 기존 파일 백업
                    string backupPath = targetExePath + ".backup";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    try
                    {
                        File.Move(targetExePath, backupPath);
                    }
                    catch
                    {
                        // 백업 실패는 계속 진행
                    }
                }

                // 새 파일을 대상 위치로 복사
                try
                {
                    File.Copy(downloadPath, targetExePath, true);

                    // 버전 파일 업데이트
                    File.WriteAllText(_versionFile, newVersion);

                    // 백업 파일 삭제
                    string backupPath = targetExePath + ".backup";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            File.Delete(backupPath);
                        }
                        catch { /* 백업 삭제 실패는 무시 */ }
                    }

                    return targetExePath;
                }
                catch
                {
                    // 실패 시 백업 복구 시도
                    string backupPath = targetExePath + ".backup";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            File.Move(backupPath, targetExePath);
                        }
                        catch
                        {
                            // 백업 복구 실패
                        }
                    }

                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private async Task<bool> DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10);

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
