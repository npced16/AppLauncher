using System;
using Xunit;
using FluentAssertions;
using AppLauncher.Features.VersionManagement;
using AppLauncher.Features.MqttControl;

namespace AppLauncher.Tests.Features.VersionManagement
{
    public class PendingUpdateManagerTests : IDisposable
    {
        public void Dispose()
        {
            PendingUpdateManager.ClearPendingUpdate();
        }

        [Fact]
        public void SavePendingUpdate_CreatesFile()
        {
            var command = new LaunchCommand
            {
                Command = "LABVIEW_UPDATE",
                URL = "https://example.com/update.zip",
                Version = "1.0.0"
            };
            
            var result = PendingUpdateManager.SavePendingUpdate(command);
            result.Should().BeTrue();
        }

        [Fact]
        public void LoadPendingUpdate_ReturnsNull_WhenNoFileExists()
        {
            var command = PendingUpdateManager.LoadPendingUpdate();
            command.Should().BeNull();
        }

        [Fact]
        public void SaveAndLoad_PreservesCommandData()
        {
            var originalCommand = new LaunchCommand
            {
                Command = "LABVIEW_UPDATE",
                URL = "https://example.com/file.zip",
                Version = "2.5.0",
                Location = "TestLocation"
            };
            
            PendingUpdateManager.SavePendingUpdate(originalCommand);
            var loadedCommand = PendingUpdateManager.LoadPendingUpdate();
            
            loadedCommand.Should().NotBeNull();
            loadedCommand!.Command.Should().Be(originalCommand.Command);
            loadedCommand.URL.Should().Be(originalCommand.URL);
            loadedCommand.Version.Should().Be(originalCommand.Version);
        }

        [Fact]
        public void ClearPendingUpdate_RemovesFile()
        {
            var command = new LaunchCommand
            {
                Command = "TEST",
                Version = "1.0.0"
            };
            PendingUpdateManager.SavePendingUpdate(command);
            
            PendingUpdateManager.ClearPendingUpdate();
            var loaded = PendingUpdateManager.LoadPendingUpdate();
            
            loaded.Should().BeNull();
        }

        [Fact]
        public void ClearPendingUpdate_DoesNotThrow_WhenNoFileExists()
        {
            Action act = () => PendingUpdateManager.ClearPendingUpdate();
            act.Should().NotThrow();
        }

        [Fact]
        public void SavePendingUpdate_OverwritesExistingFile()
        {
            var command1 = new LaunchCommand { Version = "1.0.0" };
            var command2 = new LaunchCommand { Version = "2.0.0" };
            
            PendingUpdateManager.SavePendingUpdate(command1);
            PendingUpdateManager.SavePendingUpdate(command2);
            var loaded = PendingUpdateManager.LoadPendingUpdate();
            
            loaded.Should().NotBeNull();
            loaded!.Version.Should().Be("2.0.0");
        }
    }
}