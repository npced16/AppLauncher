using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AppLauncher.Features.MqttControl;
using AppLauncher.Shared.Configuration;

namespace AppLauncher.Tests.Features.MqttControl
{
    public class MqttServiceTests : IDisposable
    {
        private MqttService? _sut;

        public void Dispose()
        {
            _sut?.Dispose();
        }

        [Fact]
        public void Constructor_WithNullSettings_ThrowsArgumentNullException()
        {
            MqttSettings nullSettings = null!;
            var clientId = "test-client";
            
            Action act = () => _sut = new MqttService(nullSettings, clientId);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithNullClientId_ThrowsArgumentNullException()
        {
            var settings = new MqttSettings
            {
                Broker = "test.broker.com",
                Port = 1883
            };
            string nullClientId = null!;
            
            Action act = () => _sut = new MqttService(settings, nullClientId);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            var settings = new MqttSettings
            {
                Broker = "test.broker.com",
                Port = 1883
            };
            var clientId = "test-client";
            
            _sut = new MqttService(settings, clientId);
            
            _sut.Should().NotBeNull();
            _sut.IsConnected.Should().BeFalse();
        }

        [Fact]
        public void IsConnected_InitiallyFalse()
        {
            var settings = new MqttSettings
            {
                Broker = "test.broker.com",
                Port = 1883
            };
            _sut = new MqttService(settings, "test-client");
            
            var isConnected = _sut.IsConnected;
            isConnected.Should().BeFalse();
        }

        [Fact]
        public async Task ConnectAsync_WithInvalidBroker_HandlesGracefully()
        {
            var settings = new MqttSettings
            {
                Broker = "invalid-broker-does-not-exist.local",
                Port = 1883
            };
            _sut = new MqttService(settings, "test-client");
            
            Func<Task> act = async () => await _sut.ConnectAsync();
            await act.Should().NotThrowAsync();
            _sut.IsConnected.Should().BeFalse();
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var settings = new MqttSettings
            {
                Broker = "test.broker.com",
                Port = 1883
            };
            _sut = new MqttService(settings, "test-client");
            
            Action act = () =>
            {
                _sut.Dispose();
                _sut.Dispose();
                _sut.Dispose();
            };
            
            act.Should().NotThrow();
        }
    }
}