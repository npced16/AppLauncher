using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AppLauncher
{
    public class BackgroundUpdater
    {
        private readonly string _downloadUrl;
        private readonly string _versionFile;
        private readonly Action<string> _statusCallback;

        public BackgroundUpdater(string downloadUrl, string versionFile, Action<string> statusCallback)
        {
            _downloadUrl = downloadUrl;
            _versionFile = versionFile;
            _statusCallback = statusCallback;
        }

        public async Task<string?> DownloadAndGetExePathAsync(string newVersion)
        {
            try
            {
                _statusCallback("업데이트 파일 다운로드 중...");

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
                    _statusCallback("다운로드 실패");
                    return null;
                }

                _statusCallback("다운로드 완료!");

                // 버전 파일 업데이트
                File.WriteAllText(_versionFile, newVersion);

                return downloadPath;
            }
            catch (Exception ex)
            {
                _statusCallback($"업데이트 실패: {ex.Message}");
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
