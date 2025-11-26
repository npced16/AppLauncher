# CLAUDE.md - AI Assistant Guide for AppLauncher

> **Last Updated**: 2025-11-26
> **Project**: AppLauncher - HBOT Chamber Software Auto-Launcher and Remote Update Manager
> **Framework**: .NET 9.0 Windows Forms
> **Language**: C# with Korean comments and documentation

## Table of Contents

1. [Project Overview](#project-overview)
2. [Architecture](#architecture)
3. [Codebase Structure](#codebase-structure)
4. [Key Patterns & Conventions](#key-patterns--conventions)
5. [Development Workflows](#development-workflows)
6. [Testing & Debugging](#testing--debugging)
7. [MQTT Communication](#mqtt-communication)
8. [Update Mechanisms](#update-mechanisms)
9. [Common Pitfalls](#common-pitfalls)
10. [Build & Deployment](#build--deployment)

---

## Project Overview

### Purpose
AppLauncher is a system tray application that:
- Automatically launches and monitors HBOT Operator (chamber software)
- Manages remote updates via MQTT for both the target application and itself
- Provides centralized logging and configuration management
- Ensures system resilience through automatic restart and recovery mechanisms

### Key Features
1. **Auto-Launch**: Starts target application on system boot via Task Scheduler
2. **Process Monitoring**: Automatically restarts target application if it crashes
3. **Remote Updates**: Downloads and installs updates for both chamber software (ZIP) and launcher (EXE)
4. **MQTT Control**: Receives commands for updates, location changes, and status reporting
5. **Configuration Backup**: Automatically backs up and restores settings during updates
6. **Log Management**: Maintains 90-day rotating logs for MQTT communications

### Technology Stack
- **.NET 9.0** (Windows Forms, self-contained deployment)
- **MQTTnet 5.0.1.1416** - MQTT client library
- **Newtonsoft.Json 13.0.3** - JSON serialization
- **System.Management 9.0.0** - Hardware info and WMI queries

---

## Architecture

### Design Pattern: Feature-Based Architecture

```
AppLauncher/
├── Features/              # Domain-specific features (self-contained modules)
│   ├── AppLaunching/      # Target application lifecycle management
│   ├── MqttControl/       # MQTT communication and message handling
│   ├── TrayApp/           # System tray application context
│   └── VersionManagement/ # Update logic for launcher and target app
├── Presentation/          # UI layer (Windows Forms)
│   └── WinForms/          # All form implementations
├── Shared/                # Cross-cutting concerns and utilities
│   ├── Configuration/     # Config file management
│   ├── Logger/            # File-based logging (MQTT logs)
│   └── Services/          # Global service container and utilities
├── Properties/            # Assembly metadata
└── Program.cs             # Entry point and initialization
```

### Core Principles

1. **Feature Independence**: Each feature module is self-contained with minimal coupling
2. **Service Container Pattern**: Global services managed via `ServiceContainer` (DI-lite pattern)
3. **Separation of Concerns**: UI (Presentation) separated from business logic (Features)
4. **Configuration as Code**: JSON-based configuration with type-safe models
5. **Defensive Programming**: Null-safe code with nullable reference types enabled

### Service Container Pattern

The `ServiceContainer` class provides a lightweight dependency injection mechanism:

```csharp
// Services are initialized once during startup
ServiceContainer.Initialize(config);

// Accessed globally throughout the application
var mqttService = ServiceContainer.MqttService;
var config = ServiceContainer.Config;
```

**Available Services:**
- `MqttService` - MQTT client instance
- `MqttMessageHandler` - Processes incoming MQTT messages
- `Config` - Application configuration
- `AppLauncher` - Application lifecycle manager

---

## Codebase Structure

### Key Files by Responsibility

#### Entry Point & Initialization
- **`Program.cs`** (505 lines)
  - Application entry point with `[STAThread]`
  - Mutex-based single instance enforcement
  - Auto-installation to `C:\Program Files\AppLauncher\`
  - Old version cleanup (`.old` file deletion)
  - Task Scheduler registration for auto-start
  - Pending update detection and execution

#### Configuration Management
- **`Shared/Configuration/ConfigManager.cs`**
  - Loads/saves `launcher_config.json` from `C:\ProgramData\AppLauncher\Data\`
  - Type-safe configuration models: `LauncherConfig`, `MqttSettings`
  - Default configuration generation
  - File path: `Environment.SpecialFolder.CommonApplicationData`

#### MQTT Communication
- **`Features/MqttControl/MqttService.cs`**
  - MQTT client wrapper with auto-reconnect (max 100 retries)
  - Connection state management
  - Topic subscription: `down/{clientId}`
  - Publishing to: `up/{clientId}`
  - Event-driven architecture: `MessageReceived`, `ConnectionStateChanged`

- **`Features/MqttControl/MqttMessageHandler.cs`**
  - Command dispatcher for MQTT messages
  - Supported commands: `LABVIEW_UPDATE`, `LAUNCHER_UPDATE`, `LOCATION_CHANGE`, `STATUS`, `SETTINGS_UPDATE`, `SETTINGS_GET`
  - Status reporting to server with hardware info

#### Version Management
- **`Features/VersionManagement/LabViewUpdater.cs`**
  - Chamber software update logic
  - ZIP download → Extract → Validate metadata → Install via PowerShell
  - Setting backup/restore (`setting.ini`)
  - `fonts_install.exe` process monitoring
  - Installation log management (1-year retention)

- **`Features/VersionManagement/LauncherUpdater.cs`**
  - Self-update mechanism
  - Download → Validate → Rename current EXE to `.old` → Save new EXE
  - Applied on next restart (cleanup in `Program.cs`)

- **`Features/VersionManagement/PendingUpdateManager.cs`**
  - Stores update commands to `pending_update.json`
  - Enables deferred updates (apply on next restart)
  - Prevents update loss during unexpected shutdowns

#### Application Lifecycle
- **`Features/AppLaunching/ApplicationLauncher.cs`**
  - Launches and monitors target executable (HBOT Operator)
  - Automatic restart on crash
  - Process cleanup on shutdown

#### System Tray UI
- **`Features/TrayApp/TrayApplicationContext.cs`**
  - System tray icon and context menu
  - Menu items: MQTT Control, Settings, Exit
  - Runs in WinForms message loop

#### Logging
- **`Shared/Logger/FileLogger.cs`**
  - Rotating file logger for MQTT activity
  - Format: `MQTT_YYYYMMDD.log`
  - Auto-cleanup: Deletes logs older than 90 days
  - Thread-safe file operations

- **`Shared/DebugLogger.cs`**
  - Conditional compilation logger (DEBUG only)
  - Used throughout codebase for development debugging
  - **Critical**: Does NOT produce output in Release builds

#### Utilities
- **`Shared/HardwareInfo.cs`**
  - Generates unique hardware UUID (CPU ID + Motherboard serial + MAC address)
  - Used as MQTT client ID

- **`Shared/TaskSchedulerManager.cs`**
  - Registers/unregisters Windows Task Scheduler tasks
  - Ensures launcher starts on system boot with admin privileges

- **`Shared/VersionInfo.cs`**
  - Retrieves assembly version information

---

## Key Patterns & Conventions

### 1. Logging Pattern

**Always use the local Log helper:**
```csharp
// Define at class level
private static void Log(string message) => DebugLogger.Log("ModuleName", message);

// Usage
Log("Operation started");
Log($"Processing file: {filePath}");
```

**Important**: `DebugLogger` only outputs in DEBUG builds. For production logging, use `FileLogger` (MQTT logs).

### 2. Null Safety

**Nullable reference types are ENABLED** (`<Nullable>enable</Nullable>`):
```csharp
// Always use null-conditional operators
var service = ServiceContainer.MqttService;
if (service?.IsConnected == true)
{
    // Safe to use service
}

// Null-forgiving operator only when absolutely certain
string path = Environment.ProcessPath!; // Use with caution
```

### 3. Async/Await Pattern

**Fire-and-forget pattern**:
```csharp
// Acceptable for background tasks that don't need to block UI
_ = Task.Run(async () =>
{
    try
    {
        await SomeAsyncOperation();
    }
    catch (Exception ex)
    {
        Log($"Background task failed: {ex.Message}");
    }
});
```

**Proper async method signature**:
```csharp
public async Task UpdateLabView(LaunchCommand command, bool immediate)
{
    try
    {
        await updater.ScheduleUpdate(immediate);
    }
    catch (Exception ex)
    {
        Log($"Update failed: {ex.Message}");
        _sendStatusResponse?.Invoke("error", ex.Message);
    }
}
```

### 4. Configuration Pattern

**Always load config via ConfigManager**:
```csharp
// Load
var config = ConfigManager.LoadConfig();

// Modify
config.MqttSettings.Broker = "new.broker.com";

// Save
ConfigManager.SaveConfig(config);
```

**Never hardcode paths** - use `LauncherConfig` properties:
```csharp
// Good
string targetExe = config.TargetExecutable;

// Bad
string targetExe = @"C:\Program Files (x86)\HBOT Operator\HBOT Operator.exe";
```

### 5. Event Handling Pattern

**Memory leak prevention**:
```csharp
// Store handler reference for later cleanup
private Action<MqttMessage>? _messageHandler;

public void Initialize()
{
    _messageHandler = (msg) => ProcessMessage(msg);
    mqttService.MessageReceived += _messageHandler;
}

public void Dispose()
{
    if (_messageHandler != null && mqttService != null)
    {
        mqttService.MessageReceived -= _messageHandler;
        _messageHandler = null;
    }
}
```

### 6. Resource Disposal Pattern

**Always implement proper cleanup**:
```csharp
public static void Dispose()
{
    // Stop timers
    _statusTimer?.Stop();
    _statusTimer?.Dispose();

    // Unsubscribe events (CRITICAL to prevent leaks)
    if (_messageHandler != null)
    {
        mqttService.MessageReceived -= _messageHandler;
    }

    // Dispose services
    mqttService?.Dispose();

    // Null out references
    mqttService = null;
}
```

### 7. Error Handling

**Defensive error handling**:
```csharp
try
{
    // Risky operation
    await DownloadFile(url);
}
catch (HttpRequestException ex)
{
    Log($"Network error: {ex.Message}");
    _sendStatusResponse?.Invoke("error", "네트워크 오류");
}
catch (Exception ex)
{
    Log($"Unexpected error: {ex.Message}");
    _sendStatusResponse?.Invoke("error", ex.Message);
}
```

**Retry logic pattern**:
```csharp
for (int i = 0; i < 3; i++)
{
    try
    {
        File.Delete(filePath);
        Log("File deleted successfully");
        break;
    }
    catch
    {
        if (i < 2)
        {
            Thread.Sleep(500);
        }
        else
        {
            Log("File deletion failed after 3 attempts");
        }
    }
}
```

---

## Development Workflows

### Initial Setup

1. **Clone and Build**:
   ```bash
   git clone <repository-url>
   cd AppLauncher
   dotnet restore
   dotnet build
   ```

2. **Run in Development Mode**:
   ```bash
   dotnet watch run
   ```
   - Runs as console application (OutputType: Exe)
   - Debug logs visible in console
   - No auto-install to Program Files

3. **Configuration**:
   - Edit `launcher_config.json` in project root (for testing)
   - Production config location: `C:\ProgramData\AppLauncher\Data\launcher_config.json`

### Making Changes

#### Adding a New MQTT Command

1. Add command case in `MqttMessageHandler.HandleMessage()`:
   ```csharp
   case "NEW_COMMAND":
       HandleNewCommand(command);
       break;
   ```

2. Implement handler method:
   ```csharp
   private void HandleNewCommand(LaunchCommand command)
   {
       try
       {
           Log($"Processing NEW_COMMAND: {command.Url}");
           // Implementation
           _sendStatusResponse?.Invoke("success", "Command executed");
       }
       catch (Exception ex)
       {
           Log($"NEW_COMMAND failed: {ex.Message}");
           _sendStatusResponse?.Invoke("error", ex.Message);
       }
   }
   ```

3. Update JSON command model if needed:
   ```csharp
   public class LaunchCommand
   {
       [JsonProperty("command")]
       public string Command { get; set; } = "";

       [JsonProperty("newField")]
       public string? NewField { get; set; }
   }
   ```

#### Adding a New Feature Module

1. Create folder under `Features/`:
   ```
   Features/
   └── NewFeature/
       ├── NewFeatureService.cs
       └── NewFeatureHandler.cs
   ```

2. Register in `ServiceContainer` if global access needed:
   ```csharp
   public static class ServiceContainer
   {
       public static NewFeatureService? NewFeature { get; private set; }

       public static void Initialize(LauncherConfig config)
       {
           NewFeature = new NewFeatureService();
       }
   }
   ```

#### Modifying Configuration Schema

1. Update models in `ConfigManager.cs`:
   ```csharp
   public class LauncherConfig
   {
       [JsonProperty("newSetting")]
       public string NewSetting { get; set; } = "default";
   }
   ```

2. Update `CreateDefaultConfig()` method

3. **Migration**: Consider backward compatibility if users have existing configs

---

## Testing & Debugging

### Debug Builds

**Characteristics**:
- Console window visible (OutputType: Exe)
- `DebugLogger` outputs to console
- No auto-install to Program Files (`#if !DEBUG` skips installation)
- Easier to attach debugger

**Running**:
```bash
dotnet run
# or
dotnet watch run  # Auto-rebuild on file changes
```

### Release Builds

**Characteristics**:
- No console window (OutputType: WinExe)
- `DebugLogger` produces no output
- Auto-installs to `C:\Program Files\AppLauncher\`
- Single-file self-contained executable

**Building**:
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

**Output**: `bin/Release/net9.0-windows/win-x64/publish/AppLauncher.exe`

### Debugging MQTT Communication

1. **Check MQTT logs**:
   - Location: `C:\ProgramData\AppLauncher\Logs\MQTT_YYYYMMDD.log`
   - Contains all MQTT events: connections, messages, errors

2. **Monitor in real-time** (Debug mode):
   ```csharp
   // DebugLogger outputs are visible in console
   DebugLogger.Log("MQTT", "Your debug message");
   ```

3. **Test MQTT commands manually**:
   - Use MQTT client (e.g., MQTT Explorer)
   - Publish to: `down/{hardware-uuid}`
   - Example payload:
     ```json
     {
       "command": "STATUS"
     }
     ```

### Common Debugging Scenarios

#### "Application not starting target executable"

1. Check `launcher_config.json` - verify `targetExecutable` path
2. Verify file exists: `File.Exists(config.TargetExecutable)`
3. Check `DebugLogger` output for launch errors
4. Ensure admin privileges (required for Task Scheduler)

#### "MQTT not connecting"

1. Verify broker address in config: `config.MqttSettings.Broker`
2. Check firewall rules (port 1883)
3. Review MQTT logs for connection attempts
4. Verify hardware UUID generation: `HardwareInfo.GetHardwareUuid()`

#### "Update not applying"

1. Check `pending_update.json` exists: `C:\ProgramData\AppLauncher\Data\`
2. Verify metadata validation (ProductName, CompanyName in EXE/ZIP)
3. Review installation logs: `C:\ProgramData\AppLauncher\Logs\install_log_*.txt`
4. Check admin privileges

---

## MQTT Communication

### Topic Structure

- **Subscribe (Receive)**: `down/{clientId}`
- **Publish (Send)**: `up/{clientId}`

**Client ID**: Hardware UUID generated from CPU ID + Motherboard Serial + MAC Address

### Message Format

All MQTT messages use JSON format:

```json
{
  "command": "COMMAND_NAME",
  "URL": "https://example.com/file.zip",
  "version": "1.0.0.285",
  "additionalField": "value"
}
```

### Supported Commands

#### 1. LABVIEW_UPDATE / LABVIEWUPDATE
Downloads and installs chamber software update.

**Request**:
```json
{
  "command": "LABVIEW_UPDATE",
  "URL": "https://cdn.example.com/HBOTOperator_v1.0.285.zip",
  "version": "1.0.0.285"
}
```

**Process**:
1. Download ZIP file
2. Extract to `C:\ProgramData\AppLauncher\Downloads\Volume\`
3. Validate metadata (ProductName, CompanyName)
4. Backup `setting.ini`
5. Run installer via PowerShell
6. Monitor `fonts_install.exe` process
7. Restore `setting.ini`
8. Reboot system

**Response**:
```json
{
  "status": "success",
  "message": "업데이트 완료"
}
```

#### 2. LABVIEW_UPDATE_IMMEDIATE / LABVIEWUPDATEIMMEDIATE
Same as LABVIEW_UPDATE but restarts launcher immediately to apply update.

#### 3. LAUNCHER_UPDATE / LAUNCHERUPDATE
Updates the launcher itself.

**Request**:
```json
{
  "command": "LAUNCHER_UPDATE",
  "URL": "https://cdn.example.com/AppLauncher.exe"
}
```

**Process**:
1. Download new EXE
2. Validate metadata
3. Rename current EXE to `AppLauncher.exe.old`
4. Save new EXE as `AppLauncher.exe`
5. Show notification to restart
6. On next start, cleanup `.old` file

#### 4. LOCATION_CHANGE / LOCATIONCHANGE
Updates MQTT location setting.

**Request**:
```json
{
  "command": "LOCATION_CHANGE",
  "location": "Seoul"
}
```

**Process**:
1. Update `config.MqttSettings.Location`
2. Save config
3. Reconnect MQTT with new location

#### 5. STATUS
Requests current system status.

**Request**:
```json
{
  "command": "STATUS"
}
```

**Response**:
```json
{
  "status": "response",
  "message": "AppLauncher v1.2.3 | HBOT Operator v1.0.285 | Location: Seoul | CPU: Intel Core i7 | RAM: 16GB"
}
```

#### 6. SETTINGS_UPDATE / SETTINGSUPDATE
Updates `setting.ini` file for HBOT Operator.

**Request**:
```json
{
  "command": "SETTINGS_UPDATE",
  "URL": "https://cdn.example.com/setting.ini"
}
```

#### 7. SETTINGS_GET / SETTINGSGET
Retrieves current `setting.ini` content.

**Response**: File content sent via MQTT

### Status Reporting

**Automatic Status Updates**:
- Sent every 60 seconds while MQTT connected
- Sent immediately on connection
- Sent in response to STATUS command

**Status Message Format**:
```csharp
_mqttService.PublishAsync($"up/{clientId}", new MqttMessage
{
    Topic = $"up/{clientId}",
    Payload = JsonConvert.SerializeObject(new
    {
        status = "running",
        message = "System info here"
    })
});
```

---

## Update Mechanisms

### Chamber Software Update Flow

```
[MQTT Command] → [MqttMessageHandler]
                      ↓
              [LabViewUpdater.ScheduleUpdate()]
                      ↓
        [PendingUpdateManager.SavePendingUpdate()]
                      ↓
            [Restart Launcher if immediate]
                      ↓
              [Program.Main() detects pending]
                      ↓
              [UpdateProgressForm shown]
                      ↓
         [LabViewUpdater.ExecuteUpdate()]
                      ↓
    [Download → Extract → Validate → Backup → Install → Restore]
                      ↓
              [System Reboot]
```

### Self-Update Flow

```
[MQTT Command] → [MqttMessageHandler]
                      ↓
              [LauncherUpdater.UpdateAsync()]
                      ↓
         [Download EXE → Validate Metadata]
                      ↓
    [Rename current.exe → current.exe.old]
                      ↓
         [Save new.exe → current.exe]
                      ↓
            [Show toast notification]
                      ↓
        [User restarts or system reboots]
                      ↓
       [Program.CleanupOldVersion() deletes .old]
```

### Pending Update Mechanism

**Purpose**: Ensures updates survive launcher crashes/restarts

**Storage**: `C:\ProgramData\AppLauncher\Data\pending_update.json`

**Lifecycle**:
1. Save: `PendingUpdateManager.SavePendingUpdate(command)`
2. Check: `PendingUpdateManager.HasPendingUpdate()` in `Program.Main()`
3. Load: `PendingUpdateManager.LoadPendingUpdate()`
4. Clear: `PendingUpdateManager.ClearPendingUpdate()` after completion

**Critical**: Always clear pending update after successful completion to avoid infinite update loops.

---

## Common Pitfalls

### 1. Memory Leaks from Event Handlers

**Problem**:
```csharp
// ❌ BAD: Lambda creates closure, hard to unsubscribe
mqttService.MessageReceived += (msg) => ProcessMessage(msg);
```

**Solution**:
```csharp
// ✅ GOOD: Store handler reference for cleanup
_messageHandler = (msg) => ProcessMessage(msg);
mqttService.MessageReceived += _messageHandler;

// In Dispose():
mqttService.MessageReceived -= _messageHandler;
```

### 2. ObjectDisposedException in WinForms

**Problem**: Accessing disposed controls after form closes

**Solution**:
```csharp
// Check if disposed before invoking
if (!IsDisposed && InvokeRequired)
{
    Invoke(new Action(() => UpdateUI()));
}
```

### 3. Hardcoded Paths

**Problem**:
```csharp
// ❌ BAD: Breaks on different systems
string path = @"C:\Program Files (x86)\HBOT Operator\HBOT Operator.exe";
```

**Solution**:
```csharp
// ✅ GOOD: Use configuration
string path = config.TargetExecutable;

// ✅ GOOD: Use special folders
string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
```

### 4. Async Void Methods

**Problem**:
```csharp
// ❌ BAD: Cannot catch exceptions, cannot await
private async void UpdateLabView() { }
```

**Solution**:
```csharp
// ✅ GOOD: Return Task
private async Task UpdateLabView() { }

// ✅ ACCEPTABLE: Fire-and-forget with try-catch
_ = Task.Run(async () =>
{
    try { await UpdateLabView(); }
    catch (Exception ex) { Log($"Error: {ex}"); }
});
```

### 5. Not Checking MQTT Connection State

**Problem**:
```csharp
// ❌ BAD: May throw if not connected
await _mqttService.PublishAsync(topic, message);
```

**Solution**:
```csharp
// ✅ GOOD: Check connection first
if (_mqttService?.IsConnected == true)
{
    await _mqttService.PublishAsync(topic, message);
}
```

### 6. Forgetting Pending Update Cleanup

**Problem**: Update runs on every launcher start

**Solution**:
```csharp
// Always clear after successful update
try
{
    await ExecuteUpdate();
    PendingUpdateManager.ClearPendingUpdate(); // ✅ CRITICAL
}
catch (Exception ex)
{
    Log($"Update failed: {ex}");
    // Leave pending update for retry on next start
}
```

### 7. Process Kill Without Waiting

**Problem**:
```csharp
// ❌ BAD: Process may not fully terminate
process.Kill();
File.Delete(process.MainModule.FileName); // May fail
```

**Solution**:
```csharp
// ✅ GOOD: Wait for exit
process.Kill();
process.WaitForExit(5000); // Wait up to 5 seconds
Thread.Sleep(500); // Extra safety margin
File.Delete(filePath);
```

### 8. Ignoring Admin Privileges

**Problem**: Task Scheduler operations fail silently

**Solution**:
- Ensure `app.manifest` has `requireAdministrator`
- Check `IsAdministrator()` before critical operations
- Inform user if privileges are insufficient

---

## Build & Deployment

### Build Configurations

#### Debug Build
```bash
dotnet build -c Debug
```

**Output**: `bin/Debug/net9.0-windows/AppLauncher.exe`

**Characteristics**:
- Console window visible
- Debug symbols included
- No code trimming
- Multiple files (dependencies separate)

#### Release Build
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

**Output**: `bin/Release/net9.0-windows/win-x64/publish/AppLauncher.exe`

**Characteristics**:
- Single-file executable (~60-80 MB)
- Self-contained (no .NET runtime required)
- No console window
- No debug symbols
- Optimized for size with compression

### Build Optimizations

**Project settings** (`AppLauncher.csproj`):
```xml
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<PublishReadyToRun>false</PublishReadyToRun>
<DebugType>none</DebugType>
<DebugSymbols>false</DebugSymbols>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<InvariantGlobalization>true</InvariantGlobalization>
<EventSourceSupport>false</EventSourceSupport>
```

**Size reduction techniques**:
1. `InvariantGlobalization=true` - Removes culture-specific resources
2. `EventSourceSupport=false` - Disables event tracing
3. `EnableCompressionInSingleFile=true` - Compresses embedded DLLs
4. `PublishTrimmed=false` - Disabled to avoid reflection issues with WinForms

### Deployment Paths

#### Program Files (Executable)
```
C:\Program Files\AppLauncher\
└── AppLauncher.exe
```

#### ProgramData (Configuration & Data)
```
C:\ProgramData\AppLauncher\
├── Data\
│   ├── launcher_config.json
│   ├── labview_version.txt
│   └── pending_update.json
├── Downloads\
│   └── Volume\
│       └── [extracted installer files]
├── Logs\
│   ├── MQTT_YYYYMMDD.log
│   └── install_log_*.txt
└── Backup\
    └── setting_*.ini
```

#### User Documents (Target App Settings)
```
C:\Users\[Username]\Documents\HBOT\Setting\
└── setting.ini
```

### Installation Process

**Automatic** (Release builds):
1. When run from non-Program Files location, copies itself to `C:\Program Files\AppLauncher\`
2. Registers Task Scheduler task for auto-start
3. Restarts from Program Files location
4. Original executable can be deleted

**Manual**:
1. Copy `AppLauncher.exe` to desired location
2. Run as Administrator
3. Automatic installation will trigger

### Task Scheduler Configuration

**Task Name**: `AppLauncher_AutoStart`

**Trigger**: At logon (any user)

**Action**: Run `C:\Program Files\AppLauncher\AppLauncher.exe`

**Settings**:
- Run with highest privileges (admin)
- Run whether user is logged on or not

**Registration**:
```csharp
TaskSchedulerManager.RegisterTask(exePath);
```

**Verification**:
```csharp
bool isRegistered = TaskSchedulerManager.IsTaskRegistered();
```

---

## File Paths Reference

### Configuration Files

| File | Location | Purpose |
|------|----------|---------|
| `launcher_config.json` | `C:\ProgramData\AppLauncher\Data\` | Main configuration |
| `labview_version.txt` | `C:\ProgramData\AppLauncher\Data\` | Installed chamber software version |
| `pending_update.json` | `C:\ProgramData\AppLauncher\Data\` | Scheduled update command |

### Log Files

| File | Location | Retention | Purpose |
|------|----------|-----------|---------|
| `MQTT_YYYYMMDD.log` | `C:\ProgramData\AppLauncher\Logs\` | 90 days | MQTT communication logs |
| `install_log_*.txt` | `C:\ProgramData\AppLauncher\Logs\` | 1 year | Installation logs |

### Temporary Files

| Directory | Purpose | Cleanup |
|-----------|---------|---------|
| `C:\ProgramData\AppLauncher\Downloads\` | Downloaded update files | Manual/on update |
| `C:\ProgramData\AppLauncher\Downloads\Volume\` | Extracted installer | After successful install |
| `C:\ProgramData\AppLauncher\Backup\` | Setting backups | Manual |

---

## Code Style Guidelines

### Naming Conventions

- **Private fields**: `_camelCase` with underscore prefix
  ```csharp
  private MqttService _mqttService;
  private readonly HttpClient _httpClient;
  ```

- **Local variables**: `camelCase`
  ```csharp
  var config = LoadConfig();
  string filePath = GetPath();
  ```

- **Methods**: `PascalCase`
  ```csharp
  public async Task UpdateLabView() { }
  private void ProcessMessage(MqttMessage msg) { }
  ```

- **Constants**: `PascalCase` or `UPPER_CASE`
  ```csharp
  private const string ConfigFileName = "launcher_config.json";
  private const int MAX_RETRY_COUNT = 3;
  ```

### Comments and Documentation

- **Korean comments** for implementation details
- **English** for code identifiers (class/method names)
- Use XML documentation for public APIs:
  ```csharp
  /// <summary>
  /// MQTT 메시지 수신 처리
  /// </summary>
  /// <param name="message">수신된 MQTT 메시지</param>
  public void HandleMessage(MqttMessage message)
  ```

### File Organization

1. **Using statements** - grouped and sorted
2. **Namespace** - matches folder structure
3. **Class definition**
4. **Fields** - private first, then public
5. **Constructor**
6. **Public methods**
7. **Private methods**
8. **Nested classes** - at end if needed

### Error Messages

- **Korean** for user-facing messages
- **Include context** in log messages:
  ```csharp
  Log($"파일 다운로드 실패: {url} - {ex.Message}");
  ```

---

## Git Workflow

### Branch Strategy

- **`main`**: Production-ready code
- **Feature branches**: `feature/description` or auto-generated (e.g., `claude/claude-md-...`)
- **Hotfix branches**: `hotfix/issue-description`

### Commit Message Style

**Format** (Korean):
```
<type>: <short description>

<detailed description if needed>
```

**Types**:
- `feat`: 새로운 기능
- `fix`: 버그 수정
- `refactor`: 코드 리팩토링
- `docs`: 문서 변경
- `chore`: 빌드/설정 변경

**Examples**:
```
feat: MQTT 설정 반영 및 업데이트 요청 오류 처리 추가
fix: Dispose 메서드에서 base.Dispose() 호출 추가
refactor: DebugLogger 전역 사용으로 로깅 통일
```

### Pull Request Guidelines

1. **Title**: Clear, concise description in Korean
2. **Description**: What changed and why
3. **Testing**: How you tested the changes
4. **Breaking changes**: Clearly marked if any

---

## Troubleshooting Guide

### Problem: "Launcher won't start"

**Symptoms**: Double-click does nothing

**Solutions**:
1. Check if already running (system tray icon)
2. Run as Administrator
3. Check Windows Event Viewer for errors
4. Delete `C:\ProgramData\AppLauncher\Data\pending_update.json` if stuck in update loop

### Problem: "Target application not launching"

**Symptoms**: Launcher runs but HBOT Operator doesn't start

**Solutions**:
1. Verify path in `launcher_config.json`
2. Check if target EXE exists
3. Look for errors in debug logs (if debug build)
4. Ensure target application is not already running

### Problem: "MQTT not connecting"

**Symptoms**: Status not updating, commands not received

**Solutions**:
1. Check broker address in config
2. Verify network connectivity
3. Check firewall rules for port 1883
4. Review `MQTT_YYYYMMDD.log` for connection errors
5. Verify MQTT broker is running

### Problem: "Update fails with 'Metadata validation failed'"

**Symptoms**: Update downloads but doesn't install

**Solutions**:
1. Check EXE/ZIP metadata (ProductName, CompanyName)
2. Ensure file is not corrupted (re-download)
3. Review installation logs: `C:\ProgramData\AppLauncher\Logs\install_log_*.txt`

### Problem: "High CPU usage"

**Symptoms**: AppLauncher consuming excessive CPU

**Solutions**:
1. Check for infinite MQTT reconnect loop (max 100 retries)
2. Look for process monitoring issues
3. Check if target application is crashing repeatedly
4. Review logs for error patterns

---

## Security Considerations

### Admin Privileges

**Required for**:
- Task Scheduler registration
- Installation to Program Files
- System restart operations

**Manifest setting**:
```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

### File Operations

**Safe practices**:
1. Validate downloaded files before execution
2. Check metadata (ProductName, CompanyName) before installing
3. Backup critical files before updates
4. Use secure download (HTTPS only)

### Process Management

**Considerations**:
1. Only kill processes you created or explicitly manage
2. Verify process path before termination
3. Wait for graceful exit before forceful kill
4. Clean up child processes (`fonts_install.exe`)

---

## Performance Optimization

### MQTT Message Processing

**Pattern**:
```csharp
// Fire-and-forget for long operations
case "LABVIEW_UPDATE":
    _ = UpdateLabView(command, false); // Don't block MQTT thread
    break;
```

### File Downloads

**Best practices**:
1. Use async I/O: `await httpClient.GetAsync()`
2. Stream large files instead of loading into memory
3. Implement progress reporting for user feedback

### Logging

**Efficient logging**:
```csharp
// ✅ GOOD: Conditional compilation (zero cost in Release)
DebugLogger.Log("Info", "Debug message");

// ❌ BAD: Always evaluates string interpolation
if (debugEnabled)
    Console.WriteLine($"Debug: {expensiveOperation()}");
```

---

## Future Improvement Ideas

### Potential Enhancements

1. **Telemetry**: Send anonymous usage statistics to server
2. **Auto-rollback**: Revert to previous version if update fails
3. **Configuration UI**: GUI for editing `launcher_config.json`
4. **Update scheduling**: Allow deferred updates to specific time
5. **Crash reporting**: Automatic error reporting to server
6. **Multi-language support**: English/Korean UI (currently Korean only)
7. **Digital signatures**: Verify update file signatures
8. **Delta updates**: Download only changed files, not full ZIP

### Technical Debt

1. **Test coverage**: Add unit tests for critical paths
2. **Dependency injection**: Migrate to proper DI container (e.g., Microsoft.Extensions.DependencyInjection)
3. **Configuration validation**: JSON schema validation
4. **Structured logging**: Migrate to Serilog or NLog
5. **Error handling**: Centralized exception handling middleware

---

## Quick Reference

### Common Code Snippets

**Load configuration**:
```csharp
var config = ConfigManager.LoadConfig();
```

**Send MQTT status**:
```csharp
ServiceContainer.MqttMessageHandler?.SendStatus("message");
```

**Log debug message**:
```csharp
DebugLogger.Log("Tag", "Message");
```

**Get hardware UUID**:
```csharp
string uuid = HardwareInfo.GetHardwareUuid();
```

**Schedule update**:
```csharp
PendingUpdateManager.SavePendingUpdate(command);
```

**Publish MQTT message**:
```csharp
if (ServiceContainer.MqttService?.IsConnected == true)
{
    await ServiceContainer.MqttService.PublishAsync(topic, message);
}
```

### Useful Paths

```csharp
// ProgramData folder
string appData = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "AppLauncher"
);

// Config file
string configPath = Path.Combine(appData, "Data", "launcher_config.json");

// Log directory
string logPath = Path.Combine(appData, "Logs");

// Documents folder
string docs = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "HBOT", "Setting"
);
```

---

## Glossary

- **LabView**: Chamber software being managed (HBOT Operator)
- **Launcher**: This application (AppLauncher)
- **Chamber**: HBOT hyperbaric oxygen therapy chamber
- **Tray App**: System tray application context
- **Pending Update**: Scheduled update waiting for launcher restart
- **ServiceContainer**: Global service registry (DI-lite)
- **Hardware UUID**: Unique identifier generated from CPU ID + Motherboard Serial + MAC
- **Fire-and-forget**: Async operation started without awaiting completion

---

## Additional Resources

### External Documentation

- [.NET 9.0 Documentation](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-9)
- [Windows Forms Guide](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/)
- [MQTTnet Documentation](https://github.com/dotnet/MQTTnet)
- [Newtonsoft.Json](https://www.newtonsoft.com/json/help/html/Introduction.htm)

### Project Files

- **README.md**: User-facing documentation (Korean)
- **CLAUDE.md**: This file - AI assistant guide
- **AppLauncher.csproj**: Build configuration
- **app.manifest**: Admin privilege settings

---

## Changelog

### Version History

See `README.md` for user-facing changelog.

**Recent architectural changes**:
- **2025-01-21**: ServiceContainer pattern introduced, MQTT file logging added
- **2024**: Initial development, feature-based architecture established

---

## Contact & Support

For questions about this codebase:
1. Review this CLAUDE.md file
2. Check README.md for user documentation
3. Review git commit history for context
4. Examine relevant source files

---

**End of CLAUDE.md**
