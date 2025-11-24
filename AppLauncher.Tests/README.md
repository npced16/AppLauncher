# AppLauncher.Tests

Comprehensive unit tests for the AppLauncher project.

## Test Coverage

### Shared Components
- **FileLoggerTests** (13 tests): File logging, async writes, rotation, concurrency
- **ServiceContainerTests** (9 tests): Service container initialization and lifecycle
- **UninstallSWServiceTests** (8 tests): Software uninstall operations
- **ConfigManagerTests** (7 tests): Configuration save/load and validation

### Features
- **PendingUpdateManagerTests** (6 tests): Update scheduling and persistence
- **MqttServiceTests** (6 tests): MQTT client service operations
- **MqttMessageHandlerTests** (6 tests): MQTT message processing

## Total: 55 Unit Tests

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~FileLoggerTests"
```

## Test Framework

- **xUnit** 2.9.2
- **FluentAssertions** 6.12.1
- **Moq** 4.20.72
- **Target**: .NET 9.0 Windows