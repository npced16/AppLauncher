using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AppLauncher.Shared.Services;
using AppLauncher.Features.AppLaunching;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Tests.Shared.Services
{
    public class ServiceContainerTests : IDisposable
    {
        public ServiceContainerTests()
        {
            ServiceContainer.Dispose();
        }

        public void Dispose()
        {
            ServiceContainer.Dispose();
        }

        [Fact]
        public void AppLauncher_InitiallyNull_BeforeSet()
        {
            var launcher = ServiceContainer.AppLauncher;
            launcher.Should().BeNull();
        }

        [Fact]
        public void AppLauncher_CanBeSet_AndRetrieved()
        {
            var launcher = new ApplicationLauncher();
            ServiceContainer.AppLauncher = launcher;
            ServiceContainer.AppLauncher.Should().BeSameAs(launcher);
        }

        [Fact]
        public void AppLauncher_CanBeSetToNull()
        {
            ServiceContainer.AppLauncher = new ApplicationLauncher();
            ServiceContainer.AppLauncher = null;
            ServiceContainer.AppLauncher.Should().BeNull();
        }

        [Fact]
        public void Initialize_SetsConfigProperty()
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
            
            ServiceContainer.Initialize(config);
            ServiceContainer.Config.Should().BeSameAs(config);
        }

        [Fact]
        public void Initialize_CreatesMqttService()
        {
            var config = new LauncherConfig
            {
                MqttSettings = new MqttSettings
                {
                    Broker = "test.broker.com",
                    Port = 1883
                }
            };
            
            ServiceContainer.Initialize(config);
            ServiceContainer.MqttService.Should().NotBeNull();
        }

        [Fact]
        public void Initialize_CreatesMqttMessageHandler()
        {
            var config = new LauncherConfig
            {
                MqttSettings = new MqttSettings
                {
                    Broker = "test.broker.com",
                    Port = 1883
                }
            };
            
            ServiceContainer.Initialize(config);
            ServiceContainer.MqttMessageHandler.Should().NotBeNull();
        }

        [Fact]
        public void Dispose_ClearsAllServices()
        {
            var config = new LauncherConfig
            {
                MqttSettings = new MqttSettings { Broker = "test.com", Port = 1883 }
            };
            ServiceContainer.Initialize(config);
            ServiceContainer.AppLauncher = new ApplicationLauncher();
            
            ServiceContainer.Dispose();
            
            ServiceContainer.MqttService.Should().BeNull();
            ServiceContainer.MqttMessageHandler.Should().BeNull();
            ServiceContainer.Config.Should().BeNull();
            ServiceContainer.AppLauncher.Should().BeNull();
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var config = new LauncherConfig
            {
                MqttSettings = new MqttSettings { Broker = "test.com", Port = 1883 }
            };
            ServiceContainer.Initialize(config);
            
            Action act = () =>
            {
                ServiceContainer.Dispose();
                ServiceContainer.Dispose();
                ServiceContainer.Dispose();
            };
            
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_DoesNotThrow_WhenNotInitialized()
        {
            Action act = () => ServiceContainer.Dispose();
            act.Should().NotThrow();
        }
    }
}