using System;
using System.IO;
using Newtonsoft.Json;

namespace AppLauncher
{
    public class ConfigManager
    {
        private const string ConfigFileName = "launcher_config.json";

        public static LauncherConfig LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

                if (!File.Exists(configPath))
                {
                    // 기본 설정 생성
                    var defaultConfig = CreateDefaultConfig();
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }

                string json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<LauncherConfig>(json);

                return config ?? CreateDefaultConfig();
            }
            catch (Exception ex)
            {
                throw new Exception($"설정 파일 로드 실패: {ex.Message}");
            }
        }

        public static void SaveConfig(LauncherConfig config)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"설정 파일 저장 실패: {ex.Message}");
            }
        }

        private static LauncherConfig CreateDefaultConfig()
        {
            return new LauncherConfig
            {
                TargetExecutable = @"C:\Program Files\YourApp\YourApp.exe",
                WorkingDirectory = @"C:\Program Files\YourApp",
                VersionCheckUrl = "https://example.com/version.txt",
                UpdateDownloadUrl = "https://example.com/update.zip",
                LocalVersionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt"),
                TargetDirectory = @"C:\Program Files\YourApp"
            };
        }
    }

    public class LauncherConfig
    {
        /// <summary>
        /// 실행할 대상 프로그램의 전체 경로
        /// </summary>
        [JsonProperty("targetExecutable")]
        public string TargetExecutable { get; set; } = "";

        /// <summary>
        /// 대상 프로그램의 작업 디렉토리 (선택사항)
        /// </summary>
        [JsonProperty("workingDirectory")]
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// 버전 정보를 확인할 URL (텍스트 또는 JSON 형식)
        /// 예: https://example.com/version.txt (내용: "1.0.5")
        /// 또는: https://example.com/version.json (내용: {"version": "1.0.5"})
        /// </summary>
        [JsonProperty("versionCheckUrl")]
        public string VersionCheckUrl { get; set; } = "";

        /// <summary>
        /// 업데이트 파일(ZIP)을 다운로드할 URL
        /// </summary>
        [JsonProperty("updateDownloadUrl")]
        public string UpdateDownloadUrl { get; set; } = "";

        /// <summary>
        /// 로컬 버전 파일 경로
        /// </summary>
        [JsonProperty("localVersionFile")]
        public string LocalVersionFile { get; set; } = "";

        /// <summary>
        /// 업데이트 파일을 압축 해제할 대상 디렉토리
        /// </summary>
        [JsonProperty("targetDirectory")]
        public string TargetDirectory { get; set; } = "";
    }
}
