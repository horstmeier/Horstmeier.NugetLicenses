# Stdout/Stderr Separation - Implementation Complete

## Summary

Successfully implemented proper stdout/stderr separation. Now:
- ✅ **Only the report** goes to stdout
- ✅ **All logging and informational messages** go to stderr
- ✅ **All warnings** go to stderr

## Changes Made

### 1. New Logging Classes

#### `Logging/StderrConsoleLogger.cs`
- Custom `ILogger` implementation that writes to stderr instead of stdout
- Formats log messages with timestamp and log level
- Writes exceptions to stderr as well

#### `Logging/StderrConsoleLoggerProvider.cs`
- `ILoggerProvider` that creates stderr console loggers
- Configurable minimum log level

#### `Logging/LoggingExtensions.cs`
- Extension methods to easily register stderr console logging
- `AddStderrConsole()` method for DI configuration

### 2. Updated Program.cs
- Added import: `using Horstmeier.NugetLicenses.Logging;`
- Changed logging configuration from `AddConsole()` to `AddStderrConsole()`

## Output Routing

| Output | Stream | Location in Code |
|--------|--------|------------------|
| Report | stdout | `Console.Out.WriteLine(report)` (line ~167) |
| Configuration warnings | stderr | `Console.Error.WriteLine()` (lines ~102-104) |
| Progress messages | stderr | `Console.Error.WriteLine()` (lines ~122-130) |
| Logging (INFO, DEBUG, etc.) | stderr | `AddStderrConsole()` |

## Benefits

1. **Clean output** — Scripts can easily capture just the report
2. **Pipeline friendly** — Can redirect stderr to file while piping stdout elsewhere
3. **Better logging** — All diagnostic information separated from output
4. **Standard practice** — Follows Unix/POSIX convention (tools output to stdout, diagnostics to stderr)

## Usage Examples

```bash
# Get only the report on stdout
nuget-licenses --project-path /path > report.json 2>/dev/null

# Get only the logs/warnings (discard report)
nuget-licenses --project-path /path > /dev/null 2>&1

# Save report and logs separately
nuget-licenses --project-path /path > report.txt 2> logs.txt

# Pipe report to another tool, see logs in console
nuget-licenses --project-path /path | jq .

# Suppress logs, show only report
nuget-licenses --quiet --project-path /path
```

## Files Created

1. `/src/Horstmeier.NugetLicenses/Logging/StderrConsoleLogger.cs`
2. `/src/Horstmeier.NugetLicenses/Logging/StderrConsoleLoggerProvider.cs`
3. `/src/Horstmeier.NugetLicenses/Logging/LoggingExtensions.cs`

## Files Modified

1. `/src/Horstmeier.NugetLicenses/Program.cs`
   - Added logging namespace import
   - Changed `AddConsole()` to `AddStderrConsole()`

## Testing

✅ No compilation errors
✅ Project builds successfully in Release mode
✅ All three logging classes follow Microsoft.Extensions.Logging patterns

