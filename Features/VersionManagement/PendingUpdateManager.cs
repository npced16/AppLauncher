using System;
using System.IO;
using System.Text.Json;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared;

namespace AppLauncher.Features.VersionManagement
{
    /// <summary>
    /// 예약된 업데이트 정보를 JSON 파일로 관리
    /// </summary>
    public static class PendingUpdateManager
    {
        private static void Log(string message) => DebugLogger.Log("PENDING", message);

        private static readonly string PendingUpdateFilePath;

        static PendingUpdateManager()
        {
            // C:\ProgramData\AppLauncher\pending_update.json
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appDataDir = Path.Combine(programDataPath, "AppLauncher");

            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }

            PendingUpdateFilePath = Path.Combine(appDataDir, "pending_update.json");
        }

        /// <summary>
        /// 업데이트 예약 저장 (LaunchCommand 직접 저장)
        /// </summary>
        public static bool SavePendingUpdate(LaunchCommand command)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                string json = JsonSerializer.Serialize(command, options);
                File.WriteAllText(PendingUpdateFilePath, json);

                Log($"Update scheduled and saved: {PendingUpdateFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Failed to save pending update: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 예약된 업데이트 읽기 (LaunchCommand 직접 반환)
        /// </summary>
        public static LaunchCommand? LoadPendingUpdate()
        {
            try
            {
                if (!File.Exists(PendingUpdateFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(PendingUpdateFilePath);

                // 빈 파일이거나 무효화된 파일 처리
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                {
                    Log($"Pending update file is empty or invalidated, cleaning up");
                    ClearPendingUpdate();
                    return null;
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var command = JsonSerializer.Deserialize<LaunchCommand>(json, options);

                // 필수 필드가 없으면 무효한 업데이트
                if (command == null || string.IsNullOrEmpty(command.Version))
                {
                    Log($"Pending update has no valid command, cleaning up");
                    ClearPendingUpdate();
                    return null;
                }

                Log($"Update loaded: {PendingUpdateFilePath}");
                return command;
            }
            catch (Exception ex)
            {
                Log($"Failed to load pending update: {ex.Message}");
                // 파싱 실패 시 파일 정리
                ClearPendingUpdate();
                return null;
            }
        }

        /// <summary>
        /// 예약된 업데이트 삭제 (재시도 로직 포함)
        /// </summary>
        public static void ClearPendingUpdate()
        {
            if (!File.Exists(PendingUpdateFilePath))
            {
                Log($"No pending update file to clear");
                return;
            }

            const int maxRetries = 5;
            const int delayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // 파일 속성 초기화 (읽기 전용 해제)
                    File.SetAttributes(PendingUpdateFilePath, FileAttributes.Normal);
                    File.Delete(PendingUpdateFilePath);
                    Log($"Pending update cleared: {PendingUpdateFilePath}");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"Delete attempt {i + 1}/{maxRetries} failed: {ex.Message}");

                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(delayMs);
                    }
                }
            }

            // 마지막 시도: 파일 내용을 비워서 무효화
            try
            {
                File.WriteAllText(PendingUpdateFilePath, "{}");
                Log($"Could not delete file, cleared contents instead");
            }
            catch (Exception ex)
            {
                Log($"Failed to clear file contents: {ex.Message}");
            }
        }

        /// <summary>
        /// 예약된 업데이트 존재 여부 확인
        /// </summary>
        public static bool HasPendingUpdate()
        {
            return File.Exists(PendingUpdateFilePath);
        }
    }
}
