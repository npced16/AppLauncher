using System;
using System.IO;
using System.Text.Json;

namespace AppLauncher.Features.VersionManagement
{
    /// <summary>
    /// 예약된 업데이트 정보를 JSON 파일로 관리
    /// </summary>
    public static class PendingUpdateManager
    {
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
        /// 업데이트 예약 저장
        /// </summary>
        public static bool SavePendingUpdate(PendingUpdate update)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };

                string json = JsonSerializer.Serialize(update, options);
                File.WriteAllText(PendingUpdateFilePath, json);

                Console.WriteLine($"[PENDING] Update scheduled and saved: {PendingUpdateFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PENDING] Failed to save pending update: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 예약된 업데이트 읽기
        /// </summary>
        public static PendingUpdate? LoadPendingUpdate()
        {
            try
            {
                if (!File.Exists(PendingUpdateFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(PendingUpdateFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var update = JsonSerializer.Deserialize<PendingUpdate>(json, options);
                Console.WriteLine($"[PENDING] Update loaded: {PendingUpdateFilePath}");

                return update;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PENDING] Failed to load pending update: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 예약된 업데이트 삭제
        /// </summary>
        public static void ClearPendingUpdate()
        {
            try
            {
                if (File.Exists(PendingUpdateFilePath))
                {
                    File.Delete(PendingUpdateFilePath);
                    Console.WriteLine($"[PENDING] Pending update cleared: {PendingUpdateFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PENDING] Failed to clear pending update: {ex.Message}");
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
