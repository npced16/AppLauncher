# Testing Guide for AppLauncher

## Quick Start

```bash
# Clone and navigate
cd /home/jailuser/git

# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~FileLoggerTests"
```

## Test Organization

### Directory Structure