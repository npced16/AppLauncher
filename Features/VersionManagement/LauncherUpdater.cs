using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AppLauncher.Shared;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Features.VersionManagement
{
    public class LauncherUpdater
    {
        private static void Log(string message) => DebugLogger.Log("LauncherUpdate", message);

        private readonly string _downloadUrl;

        public LauncherUpdater(string downloadUrl)
        {
            _downloadUrl = downloadUrl;
        }

        public async Task<string?> DownloadAndReplaceExeAsync()
        {
            // Program Files 경로로 고정
            string targetExePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AppLauncher",
                "AppLauncher.exe"
            );

            try
            {
                Log($"다운로드 시작: {_downloadUrl}");

                // 임시 파일 경로
                string tempFile = Path.Combine(Path.GetTempPath(), $"AppLauncher_Update_{Guid.NewGuid()}.exe");

                // 다운로드
                bool downloadSuccess = await DownloadFileAsync(_downloadUrl, tempFile);
                if (!downloadSuccess)
                {
                    Log("다운로드 실패");
                    return null;
                }

                Log($"다운로드 완료: {tempFile}");

                // 다운로드한 파일 검증
                if (!ValidateExecutable(tempFile))
                {
                    Log("파일 검증 실패 - 같은 프로그램이 아닙니다");
                    File.Delete(tempFile);
                    return null;
                }

                Log($"파일 교체 시작: {targetExePath}");

                // 목표 디렉토리 생성
                string? targetDir = Path.GetDirectoryName(targetExePath);
                if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    Log($"디렉토리 생성: {targetDir}");
                }

                // 기존 파일이 있으면 이름 변경 (실행 중인 파일도 이름 변경 가능!)
                if (File.Exists(targetExePath))
                {
                    string oldPath = targetExePath + ".old";
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);

                    File.Move(targetExePath, oldPath);
                    Log($"기존 파일 이름 변경: {oldPath}");
                }

                // 새 파일 복사
                File.Copy(tempFile, targetExePath, overwrite: true);
                Log($"새 파일 복사 완료");

                // 임시 파일 삭제
                File.Delete(tempFile);
                Log($"임시 파일 삭제 완료");

                return targetExePath;
            }
            catch (Exception ex)
            {
                Log($"업데이트 실패: {ex.Message}");
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

        /// <summary>
        /// 다운로드한 exe 파일이 같은 프로그램인지 검증
        /// </summary>
        private bool ValidateExecutable(string downloadedExePath)
        {
            try
            {
                // 현재 실행 중인 exe 파일 경로
                string currentExePath = Environment.ProcessPath ??
                    System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";

                if (string.IsNullOrEmpty(currentExePath))
                {
                    Log("현재 실행 파일 경로를 찾을 수 없습니다");
                    return false;
                }

                // 현재 exe의 메타데이터
                var currentVersion = FileVersionInfo.GetVersionInfo(currentExePath);
                // 다운로드한 exe의 메타데이터
                var downloadedVersion = FileVersionInfo.GetVersionInfo(downloadedExePath);

                // ProductName 비교 (가장 중요)
                if (currentVersion.ProductName != downloadedVersion.ProductName)
                {
                    Log($"ProductName 불일치: '{currentVersion.ProductName}' != '{downloadedVersion.ProductName}'");
                    return false;
                }

                // InternalName 비교
                if (!string.IsNullOrEmpty(currentVersion.InternalName) &&
                    !string.IsNullOrEmpty(downloadedVersion.InternalName) &&
                    currentVersion.InternalName != downloadedVersion.InternalName)
                {
                    Log($"InternalName 불일치: '{currentVersion.InternalName}' != '{downloadedVersion.InternalName}'");
                    return false;
                }

                Log("검증 성공 - 같은 프로그램입니다");
                return true;
            }
            catch (Exception ex)
            {
                Log($"검증 중 오류: {ex.Message}");
                return false;
            }
        }

    }
}
