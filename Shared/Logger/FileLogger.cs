using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppLauncher.Shared.Logger
{
    /// <summary>
    /// MQTT 로그를 파일로 저장하고 관리하는 클래스
    /// </summary>
    public class FileLogger : IDisposable
    {
        private static void Log(string message) => DebugLogger.Log(message);
        private readonly string _logDirectory;
        private readonly int _retentionDays;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        public FileLogger(string logDirectory = "Logs", int retentionDays = 90)
        {
            _logDirectory = logDirectory;
            _retentionDays = retentionDays;

            // 로그 디렉토리 생성
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // 오래된 로그 파일 정리
            CleanupOldLogs();
        }

        /// <summary>
        /// 로그 메시지를 파일에 비동기로 기록
        /// </summary>
        public async Task WriteLogAsync(string message, string category = "MQTT")
        {
            if (_isDisposed)
                return;

            try
            {
                await _writeLock.WaitAsync();

                string fileName = GetLogFileName(category);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] {message}\r\n";

                await File.AppendAllTextAsync(fileName, logEntry, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 로그 기록 실패 시 콘솔에 출력 (무한 루프 방지)
                Log($"[FileLogger Error] {ex.Message}");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// 동기 버전의 로그 기록 (fire-and-forget)
        /// </summary>
        public void WriteLog(string message, string category = "MQTT")
        {
            _ = WriteLogAsync(message, category);
        }

        /// <summary>
        /// 현재 날짜에 해당하는 로그 파일 경로 반환
        /// </summary>
        private string GetLogFileName(string category)
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            string fileName = $"{category}_{date}.log";
            return Path.Combine(_logDirectory, fileName);
        }

        /// <summary>
        /// 보관 기간이 지난 로그 파일 삭제
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    return;

                var files = Directory.GetFiles(_logDirectory, "*.log");
                var cutoffDate = DateTime.Now.AddDays(-_retentionDays);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            File.Delete(file);
                            Log($"[FileLogger] Deleted old log file: {fileInfo.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[FileLogger] Failed to delete {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[FileLogger] Cleanup error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _writeLock?.Dispose();
        }
    }
}
