using System;
using System.IO;
using Xunit;
using FluentAssertions;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Tests.Shared.Configuration
{
    public class ConfigManagerTests : IDisposable
    {
        private readonly string _originalDirectory;
        private readonly string _testConfigPath;

        public ConfigManagerTests()
        {
            _originalDirectory = Directory.GetCurrentDirectory();
            _testConfigPath = Path.Combine(Path.GetTempPath(), $"TestConfig_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testConfigPath);
            Directory.SetCurrentDirectory(_testConfigPath);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            if (Directory.Exists(_testConfigPath))
            {
                try { Directory.Delete(_testConfigPath, true); }
                catch { }
            }
        }

        [Fact]
        public void LoadConfig_ReturnsDefaultConfig_WhenFileDoesNotExist()
        {
            var config = ConfigManager.LoadConfig();
            config.Should().NotBeNull();
            config.TargetExecutable.Should().BeEmpty();
            config.MqttSettings.Should().NotBeNull();
        }

        [Fact]
        public void SaveConfig_CreatesConfigFile()
        {
            var config = new LauncherConfig
            {
                TargetExecutable = "test.exe",
                MqttSettings = new MqttSettings
                {
                    Broker = "test.broker.com",
                    Port = 1883
                }
            };
            
            ConfigManager.SaveConfig(config);
            File.Exists("launcher_config.json").Should().BeTrue();
        }

        [Fact]
        public void SaveAndLoad_PreservesAllProperties()
        {
            var originalConfig = new LauncherConfig
            {
                TargetExecutable = "C:\\test\\app.exe",
                MqttSettings = new MqttSettings
                {
                    Broker = "mqtt.example.com",
                    Port = 8883,
                    Username = "user123",
                    Password = "pass456",
                    Location = "Office-Floor2"
                }
            };
            
            ConfigManager.SaveConfig(originalConfig);
            var loadedConfig = ConfigManager.LoadConfig();
            
            loadedConfig.Should().NotBeNull();
            loadedConfig.TargetExecutable.Should().Be(originalConfig.TargetExecutable);
            loadedConfig.MqttSettings.Broker.Should().Be(originalConfig.MqttSettings.Broker);
            loadedConfig.MqttSettings.Port.Should().Be(originalConfig.MqttSettings.Port);
        }

        [Fact]
        public void LoadConfig_HandlesCorruptedFile_ReturnsDefault()
        {
            File.WriteAllText("launcher_config.json", "{ invalid json content ][");
            var config = ConfigManager.LoadConfig();
            config.Should().NotBeNull();
            config.TargetExecutable.Should().BeEmpty();
        }

        [Fact]
        public void SaveConfig_OverwritesExistingFile()
        {
            var config1 = new LauncherConfig { TargetExecutable = "old.exe" };
            var config2 = new LauncherConfig { TargetExecutable = "new.exe" };
            
            ConfigManager.SaveConfig(config1);
            ConfigManager.SaveConfig(config2);
            var loaded = ConfigManager.LoadConfig();
            
            loaded.TargetExecutable.Should().Be("new.exe");
        }

        [Fact]
        public void LoadConfig_HandlesEmptyFile_ReturnsDefault()
        {
            File.WriteAllText("launcher_config.json", "");
            var config = ConfigManager.LoadConfig();
            config.Should().NotBeNull();
        }

        [Fact]
        public void SaveConfig_CreatesValidJson()
        {
            var config = new LauncherConfig
            {
                TargetExecutable = "app.exe",
                MqttSettings = new MqttSettings
                {
                    Broker = "broker.com",
                    Port = 1883
                }
            };
            
            ConfigManager.SaveConfig(config);
            var jsonContent = File.ReadAllText("launcher_config.json");
            
            jsonContent.Should().Contain("TargetExecutable");
            jsonContent.Should().Contain("MqttSettings");
        }
    }
}