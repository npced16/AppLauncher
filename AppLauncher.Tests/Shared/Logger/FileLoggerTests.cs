using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using AppLauncher.Shared.Logger;

namespace AppLauncher.Tests.Shared.Logger
{
    public class FileLoggerTests : IDisposable
    {
        private readonly string _testLogPath;
        private readonly FileLogger _sut;

        public FileLoggerTests()
        {
            _testLogPath = Path.Combine(Path.GetTempPath(), $"TestLogs_{Guid.NewGuid()}");
            _sut = new FileLogger(_testLogPath, retentionDays: 7);
        }

        public void Dispose()
        {
            _sut?.Dispose();
            if (Directory.Exists(_testLogPath))
            {
                try { Directory.Delete(_testLogPath, true); }
                catch { }
            }
        }

        [Fact]
        public void Constructor_CreatesLogDirectory_WhenNotExists()
        {
            Directory.Exists(_testLogPath).Should().BeTrue();
        }

        [Fact]
        public async Task WriteLogAsync_CreatesLogFile_WithCorrectFormat()
        {
            var testMessage = "Test log message";
            var category = "TEST";
            
            await _sut.WriteLogAsync(testMessage, category);
            await Task.Delay(200);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            logFiles.Should().HaveCount(1);
            
            var expectedFileName = $"TEST_{DateTime.Now:yyyyMMdd}.log";
            Path.GetFileName(logFiles[0]).Should().Be(expectedFileName);
        }

        [Fact]
        public async Task WriteLogAsync_WritesCorrectContent_ToLogFile()
        {
            var testMessage = "Test message content";
            var category = "INFO";
            
            await _sut.WriteLogAsync(testMessage, category);
            await Task.Delay(200);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            var content = await File.ReadAllTextAsync(logFiles[0]);
            
            content.Should().Contain(testMessage);
            content.Should().MatchRegex(@"\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]");
        }

        [Fact]
        public async Task WriteLog_HandlesMultipleWrites_Correctly()
        {
            var messages = new[] { "Message 1", "Message 2", "Message 3" };
            
            foreach (var msg in messages)
            {
                _sut.WriteLog(msg, "TEST");
            }
            await Task.Delay(500);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            var content = await File.ReadAllTextAsync(logFiles[0]);
            
            foreach (var msg in messages)
            {
                content.Should().Contain(msg);
            }
        }

        [Fact]
        public void WriteLog_HandlesEmptyMessage_Gracefully()
        {
            var emptyMessage = string.Empty;
            Action act = () => _sut.WriteLog(emptyMessage, "TEST");
            act.Should().NotThrow();
        }

        [Fact]
        public void WriteLog_HandlesNullMessage_Gracefully()
        {
            string nullMessage = null!;
            Action act = () => _sut.WriteLog(nullMessage, "TEST");
            act.Should().NotThrow();
        }

        [Fact]
        public void WriteLog_HandlesNullCategory_Gracefully()
        {
            var message = "Test message";
            string nullCategory = null!;
            Action act = () => _sut.WriteLog(message, nullCategory);
            act.Should().NotThrow();
        }

        [Fact]
        public async Task WriteLog_HandlesLongMessages_Correctly()
        {
            var longMessage = new string('A', 10000);
            
            _sut.WriteLog(longMessage, "TEST");
            await Task.Delay(300);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            var content = await File.ReadAllTextAsync(logFiles[0]);
            content.Should().Contain(longMessage);
        }

        [Fact]
        public async Task WriteLog_HandlesSpecialCharacters_Correctly()
        {
            var specialMessage = "Test with special chars: \n\r\t äöü ñ 中文";
            
            _sut.WriteLog(specialMessage, "TEST");
            await Task.Delay(200);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            var content = await File.ReadAllTextAsync(logFiles[0]);
            content.Should().Contain("Test with special chars:");
        }

        [Fact]
        public async Task WriteLog_HandlesConcurrentWrites_Safely()
        {
            var tasks = Enumerable.Range(0, 10).Select(i =>
                Task.Run(() =>
                {
                    for (int j = 0; j < 10; j++)
                    {
                        _sut.WriteLog($"Thread {i} Message {j}", "CONCURRENT");
                    }
                })
            ).ToArray();
            
            await Task.WhenAll(tasks);
            await Task.Delay(1000);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            logFiles.Should().HaveCount(1);
            
            var content = await File.ReadAllTextAsync(logFiles[0]);
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.Should().BeGreaterThan(50);
        }

        [Theory]
        [InlineData("ERROR")]
        [InlineData("WARNING")]
        [InlineData("INFO")]
        [InlineData("DEBUG")]
        [InlineData("MQTT")]
        public async Task WriteLog_HandlesVariousCategories_Correctly(string category)
        {
            var message = $"Test message for {category}";
            
            await _sut.WriteLogAsync(message, category);
            await Task.Delay(200);
            
            var logFiles = Directory.GetFiles(_testLogPath, "*.log");
            var content = await File.ReadAllTextAsync(logFiles[0]);
            content.Should().Contain(message);
        }

        [Fact]
        public void Constructor_WithZeroRetentionDays_DoesNotThrow()
        {
            Action act = () =>
            {
                using var logger = new FileLogger(_testLogPath, retentionDays: 0);
                logger.WriteLog("Test", "TEST");
            };
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var testLogger = new FileLogger(_testLogPath, 7);
            Action act = () =>
            {
                testLogger.Dispose();
                testLogger.Dispose();
                testLogger.Dispose();
            };
            act.Should().NotThrow();
        }
    }
}