using System;
using System.IO;
using Newtonsoft.Json;

namespace AppLauncher.Shared.Configuration
{
    public class ConfigManager
    {
        private const string ConfigFileName = "launcher_config.json";
        private const string AppName = "AppLauncher";

        // ProgramData 경로 가져오기
        private static string GetAppDataConfigPath()
        {
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appFolder = Path.Combine(programDataPath, AppName, "Data");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            return Path.Combine(appFolder, ConfigFileName);
        }


        public static LauncherConfig LoadConfig()
        {
            try
            {
                string appDataConfigPath = GetAppDataConfigPath();

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

        /// <summary>
        /// 설정을 기본값으로 초기화
        /// </summary>
        public static void ResetToDefault()
        {
            var defaultConfig = CreateDefaultConfig();
            SaveConfig(defaultConfig);
        }

        private static LauncherConfig CreateDefaultConfig()
        {
            string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string appFolder = Path.Combine(programDataPath, AppName, "Data");

            return new LauncherConfig
            {
                TargetExecutable = @"C:\Program Files (x86)\HBOT Operator\HBOT Operator.exe",
                LocalVersionFile = Path.Combine(appFolder, "labview_version.txt"),  // LabView 버전
                MqttSettings = new MqttSettings
                {
                    Broker = "localhost",
                    Port = 1883
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
        /// LabView/대상 앱 버전 파일 경로 (labview_version.txt)
        /// </summary>
        [JsonProperty("localVersionFile")]
        public string LocalVersionFile { get; set; } = "";

        /// <summary>
        /// MQTT 연결 설정
        /// </summary>
        [JsonProperty("mqttSettings")]
        public MqttSettings MqttSettings { get; set; } = new MqttSettings();
    }

    public class MqttSettings
    {
        /// <summary>
        /// MQTT 브로커 주소
        /// </summary>
        [JsonProperty("broker")]
        public string Broker { get; set; } = "";

        /// <summary>
        /// MQTT 브로커 포트 (기본: 1883)
        /// </summary>
        [JsonProperty("port")]
        public int Port { get; set; } = 1883;

        /// <summary>
        /// 설치 지점
        /// </summary>
        [JsonProperty("location")]
        public string? Location { get; set; }
    }
}
