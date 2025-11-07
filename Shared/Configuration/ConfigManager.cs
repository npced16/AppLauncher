using System;
using System.IO;
using Newtonsoft.Json;

namespace AppLauncher.Shared.Configuration
{
    public class ConfigManager
    {
        private const string ConfigFileName = "launcher_config.json";
        private const string AppName = "AppLauncher";

        // AppData 경로 가져오기
        private static string GetAppDataConfigPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, AppName);

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return Path.Combine(appFolder, ConfigFileName);
        }

        // 기존 설정이 있는지 확인 (이전 버전 호환성)
        private static string GetLegacyConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
        }

        public static LauncherConfig LoadConfig()
        {
            try
            {
                string appDataConfigPath = GetAppDataConfigPath();
                string legacyConfigPath = GetLegacyConfigPath();

                // AppData에 설정이 없고 기존 위치에 있으면 마이그레이션
                if (!File.Exists(appDataConfigPath) && File.Exists(legacyConfigPath))
                {
                    File.Copy(legacyConfigPath, appDataConfigPath, true);
                }

                if (!File.Exists(appDataConfigPath))
                {
                    // 기본 설정 생성
                    var defaultConfig = CreateDefaultConfig();
                    SaveConfig(defaultConfig);
                    return defaultConfig;
                }

                string json = File.ReadAllText(appDataConfigPath);
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
                string configPath = GetAppDataConfigPath();
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"설정 파일 저장 실패: {ex.Message}");
            }
        }

        public static string GetConfigFilePath()
        {
            return GetAppDataConfigPath();
        }

        private static LauncherConfig CreateDefaultConfig()
        {
            return new LauncherConfig
            {
                TargetExecutable = @"C:\Program Files\YourApp\YourApp.exe",
                WorkingDirectory = @"C:\Program Files\YourApp",
                VersionCheckUrl = "https://example.com/version.json",
                LocalVersionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.txt"),
                MqttSettings = new MqttSettings
                {
                    Broker = "localhost",
                    Port = 1883,
                    ClientId = "AppLauncher",
                    Topic = "applauncher/commands",
                    Username = "",
                    Password = ""
                }
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
        /// 버전 정보를 확인할 URL (JSON 형식)
        /// 예: https://example.com/version.json
        /// 내용: {"version": "1.0.5", "downloadUrl": "https://example.com/YourApp-1.0.5.exe"}
        /// </summary>
        [JsonProperty("versionCheckUrl")]
        public string VersionCheckUrl { get; set; } = "";

        /// <summary>
        /// 로컬 버전 파일 경로
        /// </summary>
        [JsonProperty("localVersionFile")]
        public string LocalVersionFile { get; set; } = "";


        /// <summary>
        /// 런처 버전 파일 경로
        /// </summary>
        [JsonProperty("launcherVersionFile")]
        public string? LauncherVersionFile { get; set; }

        /// <summary>
        /// MQTT 연결 설정
        /// </summary>
        [JsonProperty("mqttSettings")]
        public MqttSettings? MqttSettings { get; set; }
    }

    public class MqttSettings
    {
        /// <summary>
        /// MQTT 브로커 주소
        /// </summary>
        [JsonProperty("broker")]
        public string Broker { get; set; } = "localhost";

        /// <summary>
        /// MQTT 브로커 포트 (기본: 1883)
        /// </summary>
        [JsonProperty("port")]
        public int Port { get; set; } = 1883;

        /// <summary>
        /// MQTT 클라이언트 ID
        /// </summary>
        [JsonProperty("clientId")]
        public string ClientId { get; set; } = "AppLauncher";

        /// <summary>
        /// 구독할 MQTT 토픽
        /// </summary>
        [JsonProperty("topic")]
        public string Topic { get; set; } = "applauncher/commands";

        /// <summary>
        /// MQTT 사용자 이름 (선택사항)
        /// </summary>
        [JsonProperty("username")]
        public string? Username { get; set; }

        /// <summary>
        /// MQTT 비밀번호 (선택사항)
        /// </summary>
        [JsonProperty("password")]
        public string? Password { get; set; }
    }
}
