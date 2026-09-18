# Service Bus POC - Logging Format

## Overview

All AI Team Orchestration services now automatically log their standard output and standard error to the `logs/` folder with a consistent, PID-based naming format.

## Log File Naming Convention

Each service writes two log files:
- **stdout log:** `{service-name}-{DDMMYYYY-HHMMSS}-stdout.log` - Standard output
- **stderr log:** `{service-name}-{DDMMYYYY-HHMMSS}-stderr.log` - Standard error

### Example

When starting services with `run-dashboard.ps1`, you'll see logs like:

```
logs/
├── producer-19092026-123456-stdout.log          # Producer service standard output (PID: 12345)
├── producer-19092026-123456-stderr.log          # Producer service errors (PID: 12345)
├── digitalchannels-19092026-123456-stdout.log   # Digital Channels consumer output (PID: 12346)
├── digitalchannels-19092026-123456-stderr.log   # Digital Channels consumer errors
├── insurance-19092026-123456-stdout.log         # Insurance consumer output (PID: 12347)
├── insurance-19092026-123456-stderr.log
├── parksresorts-19092026-123456-stdout.log      # Parks & Resorts consumer output (PID: 12348)
├── parksresorts-19092026-123456-stderr.log
├── carwash-19092026-123456-stdout.log           # Carwash consumer output (PID: 12349)
├── carwash-19092026-123456-stderr.log
├── dashboard-19092026-123456-stdout.log         # Dashboard service output (PID: 12350)
└── dashboard-19092026-123456-stderr.log
```

## Service Names (Lowercase)

Service names in log files are always lowercase:
- `producer` - Event publisher
- `dashboard` - HTTP dashboard
- `digitalchannels` - Digital Channels consumer
- `insurance` - Insurance consumer
- `parksresorts` - Parks & Resorts consumer
- `carwash` - Carwash consumer
- `verifier` - Verification service

## Accessing Logs

### During Service Execution

When you run any of the startup scripts, the console output shows where logs are being written:

```
✓ Producer (Event Publisher) [PID: 12345]
  📌 Logs: logs/producer-12345-stdout.log | stderr.log
```

### Viewing Logs

Use any text editor or command-line tools:

```powershell
# View the latest Producer stdout log
Get-Content logs/producer-*-stdout.log | Select-Object -Last 50

# Monitor a log in real-time
Get-Content logs/producer-*-stdout.log -Wait

# Search for errors
Select-String "error" logs/*-stderr.log

# Get a summary of all log files
Get-ChildItem logs/ -Filter "*-stdout.log" | 
  ForEach-Object { $_.Name + " (" + (Get-Item $_).Length + " bytes)" }
```

## Scripts Affected

All of the following scripts now capture logs in the new format:

1. **`run-dashboard.ps1`** - Interactive dashboard + all services
2. **`run-local-poc.ps1`** - End-to-end POC with verification
3. **`debug-run.ps1`** - Debug mode with selective role startup
4. **`test-simple.ps1`** - Minimal test (Producer + DigitalChannels)

## Log Format Details

### Datetime DDMMYYYY-HHMMSS

The number in the log filename (e.g., `producer-19092026-123456-stdout.log`) is the Windows Process ID (PID) assigned to the .NET runtime when the service starts. This allows you to:

- Correlate logs with running processes
- Distinguish between multiple runs of the same service
- Use Windows task manager to check if a process is still running

### Temporary Files

During startup, the system creates temporary files with random suffixes (e.g., `producer-temp-1234567890-stdout.log`), then renames them to include the PID once the process starts. These temporary files are cleaned up automatically.

## Troubleshooting

### Missing Log Files

If log files don't appear:

1. **Check the logs folder exists:**
   ```powershell
   Test-Path ./logs
   ```

2. **Verify the service started successfully:**
   - Check the console output for error messages
   - Ensure prerequisites are installed (.NET 10 SDK, Docker Desktop)

3. **Check file permissions:**
   - Ensure you have write access to the `logs/` folder
   - Run PowerShell as Administrator if needed

### Large Log Files

If log files grow very large:

1. **Archive old logs:**
   ```powershell
   Get-ChildItem logs/ | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-1) } | 
     Move-Item -Destination "logs/archived/"
   ```

2. **Clean up logs:**
   ```powershell
   Remove-Item logs/*.log -Confirm
   ```

### Finding a Specific Service's Logs

To find logs for a specific service and PID:

```powershell
# List all producer logs
Get-ChildItem logs/producer-*-stdout.log

# View logs for specific PID
Get-Content logs/producer-19092026-123456-stdout.log
```

## Configuration

Logging is always enabled and cannot be disabled via script parameters. If you need to disable logging output, you can:

1. Redirect the log files to a faster drive (SSD)
2. Configure log rotation using Windows scheduled tasks
3. Implement application-level log filtering via environment variables

## Related

- [run-dashboard.ps1](scripts/run-dashboard.ps1) - Start dashboard with logging
- [run-local-poc.ps1](scripts/run-local-poc.ps1) - Full POC run with logging
- [debug-run.ps1](scripts/debug-run.ps1) - Debug mode with selective logging
- [test-simple.ps1](scripts/test-simple.ps1) - Simple test with logging
