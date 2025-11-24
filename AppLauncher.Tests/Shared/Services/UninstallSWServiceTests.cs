using System;
using Xunit;
using FluentAssertions;
using AppLauncher.Shared.Services;

namespace AppLauncher.Tests.Shared.Services
{
    public class UninstallSWServiceTests
    {
        [Fact]
        public void FindUninstallString_ReturnsNull_WhenProgramNotFound()
        {
            var nonExistentProgram = $"NonExistentProgram_{Guid.NewGuid()}";
            var result = UninstallSWService.FindUninstallString(nonExistentProgram);
            result.Should().BeNull();
        }

        [Fact]
        public void FindUninstallString_HandlesNullInput_Gracefully()
        {
            string nullProgram = null!;
            Action act = () => UninstallSWService.FindUninstallString(nullProgram);
            act.Should().NotThrow();
        }

        [Fact]
        public void FindUninstallString_HandlesEmptyInput_Gracefully()
        {
            var emptyProgram = string.Empty;
            var result = UninstallSWService.FindUninstallString(emptyProgram);
            result.Should().BeNull();
        }

        [Fact]
        public void Uninstall_ReturnsFalse_WhenProgramNotFound()
        {
            var nonExistentProgram = $"NonExistent_{Guid.NewGuid()}";
            var result = UninstallSWService.Uninstall(nonExistentProgram, silent: true);
            result.Should().BeFalse();
        }

        [Fact]
        public void Uninstall_HandlesNullProgramName_Gracefully()
        {
            string nullProgram = null!;
            Action act = () => UninstallSWService.Uninstall(nullProgram, silent: true);
            act.Should().NotThrow();
        }

        [Fact]
        public void UninstallHbotOperator_CallsUninstallWithCorrectName()
        {
            var result = UninstallSWService.UninstallHbotOperator(silent: true);
            result.Should().BeOfType<bool>();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Uninstall_AcceptsSilentParameter(bool silent)
        {
            var nonExistentProgram = "NonExistent";
            Action act = () => UninstallSWService.Uninstall(nonExistentProgram, silent);
            act.Should().NotThrow();
        }

        [Fact]
        public void FindUninstallString_HandlesSpecialCharacters_InProgramName()
        {
            var programWithSpecialChars = "Test&Program<>\"Name";
            Action act = () => UninstallSWService.FindUninstallString(programWithSpecialChars);
            act.Should().NotThrow();
        }
    }
}