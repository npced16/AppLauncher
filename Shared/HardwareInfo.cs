using System;
using System.Management;

namespace AppLauncher.Shared
{
    /// <summary>
    /// 하드웨어 정보를 가져오는 유틸리티 클래스
    /// </summary>
    public static class HardwareInfo
    {
        /// <summary>
        /// 하드웨어 UUID (마더보드 UUID)를 가져옵니다
        /// </summary>
        public static string GetHardwareUuid()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var uuid = obj["UUID"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(uuid))
                        {
                            return uuid.Trim();
                        }
                    }
                }
                return "Unknown";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"하드웨어 UUID 가져오기 실패: {ex.Message}");
                return "Unknown";
            }
        }
    }
}
