using System;
using Xunit;
using FluentAssertions;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Tests.Features.MqttControl
{
    public class MqttMessageHandlerTests : IDisposable
    {
        private MqttService? _mqttService;
        private MqttMessageHandler? _sut;

        public void Dispose()
        {
            _mqttService?.Dispose();
        }

        [Fact]
        public void Constructor_WithNullMqttService_ThrowsArgumentNullException()
        {
            MqttService nullService = null!;
            var config = new LauncherConfig
            {
                MqttSettings = new MqttSettings { Broker = "test.com", Port = 1883 }
            };
            
            Action act = () => _sut = new MqttMessageHandler(nullService, config);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("mqttService");
        }

        [Fact]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            var settings = new MqttSettings { Broker = "test.com", Port = 1883 };
            _mqttService = new MqttService(settings, "test-client");
            LauncherConfig nullConfig = null!;
            
            Action act = () => _sut = new MqttMessageHandler(_mqttService, nullConfig);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("config");
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var settings = new MqttSettings { Broker = "test.com", Port = 1883 };
            _mqttService = new MqttService(settings, "test-client");
            var config = new LauncherConfig { MqttSettings = settings };
            
            _sut = new MqttMessageHandler(_mqttService, config);
            _sut.Should().NotBeNull();
        }

        [Fact]
        public void SetBalloonTipCallback_DoesNotThrow()
        {
            var settings = new MqttSettings { Broker = "test.com", Port = 1883 };
            _mqttService = new MqttService(settings, "test-client");
            var config = new LauncherConfig { MqttSettings = settings };
            _sut = new MqttMessageHandler(_mqttService, config);
            
            Action act = () => _sut.SetBalloonTipCallback((title, msg, duration) => { });
            act.Should().NotThrow();
        }

        [Fact]
        public void HandleMessage_WithNullMessage_DoesNotThrow()
        {
            var settings = new MqttSettings { Broker = "test.com", Port = 1883 };
            _mqttService = new MqttService(settings, "test-client");
            var config = new LauncherConfig { MqttSettings = settings };
            _sut = new MqttMessageHandler(_mqttService, config);
            
            Action act = () => _sut.HandleMessage(null!);
            act.Should().NotThrow();
        }

        [Fact]
        public void RequestLabViewUpdate_WithDisconnectedService_DoesNotThrow()
        {
            var settings = new MqttSettings { Broker = "test.com", Port = 1883 };
            _mqttService = new MqttService(settings, "test-client");
            var config = new LauncherConfig { MqttSettings = settings };
            _sut = new MqttMessageHandler(_mqttService, config);
            
            Action act = () => _sut.RequestLabViewUpdate("test reason");
            act.Should().NotThrow();
        }
    }
}