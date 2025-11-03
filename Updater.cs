using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace AppLauncher
{
    public class Updater
    {
        private readonly string _downloadUrl;
        private readonly string _targetDirectory;
        private readonly string _versionFile;
        private readonly MainWindow _window;

        public Updater(string downloadUrl, string targetDirectory, string versionFile, MainWindow window)
        {
            _downloadUrl = downloadUrl;
            _targetDirectory = targetDirectory;
            _versionFile = versionFile;
            _window = window;
        }

        public async Task<bool> UpdateAsync(string newVersion)
        {
            try
            {
                _window.UpdateStatus("업데이트 파일 다운로드 중...");
                _window.UpdateProgress(10);

                // 임시 디렉토리 생성
                string tempDir = Path.Combine(Path.GetTempPath(), "AppLauncherUpdate");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                // 파일 다운로드
                string downloadPath = Path.Combine(tempDir, "update.zip");
                bool downloadSuccess = await DownloadFileAsync(_downloadUrl, downloadPath);

                if (!downloadSuccess)
                {
                    _window.UpdateStatus("다운로드 실패", isError: true);
                    return false;
                }

                _window.UpdateStatus("압축 해제 중...");
                _window.UpdateProgress(60);

                // 압축 해제
                string extractPath = Path.Combine(tempDir, "extracted");
                ZipFile.ExtractToDirectory(downloadPath, extractPath);

                _window.UpdateStatus("파일 교체 중...");
                _window.UpdateProgress(80);

                // 기존 프로그램 종료 (실행중인 경우)
                await KillTargetProcessAsync();

                // 파일 복사
                CopyDirectory(extractPath, _targetDirectory, true);

                // 버전 파일 업데이트
                File.WriteAllText(_versionFile, newVersion);

                _window.UpdateProgress(100);
                _window.UpdateStatus("업데이트 완료!");

                // 임시 파일 정리
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch { }

                return true;
            }
            catch (Exception ex)
            {
                _window.UpdateStatus($"업데이트 실패: {ex.Message}", isError: true);
                return false;
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

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        var progress = (int)((totalRead * 50) / totalBytes) + 10; // 10-60% for download
                        _window.UpdateProgress(progress);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task KillTargetProcessAsync()
        {
            try
            {
                string targetExe = Path.Combine(_targetDirectory, Path.GetFileName(ConfigManager.LoadConfig().TargetExecutable));
                string processName = Path.GetFileNameWithoutExtension(targetExe);

                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void CopyDirectory(string sourceDir, string destDir, bool overwrite)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir, overwrite);
            }
        }
    }
}
