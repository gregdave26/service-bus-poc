# Log File Timestamp Formatting - FIXED ✅

## Change Applied

**File:** `scripts/run-dashboard.ps1` (lines 240-256)

**Before:**
```
logs/Producer-stdout.log
logs/Producer-stderr.log
logs/Insurance-stdout.log
logs/Insurance-stderr.log
```

**After:**
```
logs/Producer-20260917-105157.stdout.log
logs/Producer-20260917-105157.stderr.log
logs/Insurance-20260917-105157.stdout.log
logs/Insurance-20260917-105157.stderr.log
```

## Benefits

1. ✅ **Run Identification** - Can see which run each log is from
2. ✅ **No Overwriting** - Each run gets new timestamped files
3. ✅ **Easy Sorting** - `ls logs/ | sort` shows runs in order
4. ✅ **Easy Finding** - Latest logs: `ls logs/ -Newest 12` (6 services × 2 streams)

## Timestamp Format

- **Format:** `yyyyMMdd-HHmmss` 
- **Example:** `20260917-105157` = 2026-09-17 at 10:51:57
- **Placement:** Between service name and file type
- **Full path:** `logs/{ServiceName}-{Date}-{Time}.{stdout|stderr}.log`

## How to Use

### Find logs from specific run:
```powershell
# Latest logs
ls logs/ | sort -Descending | head -12

# Logs from specific time
ls logs/Producer-*20260917-10*.log

# All Producer logs
ls logs/Producer-*.log

# All stderr logs (errors)
ls logs/*-*stderr.log
```

### View live logs from a run:
```powershell
Get-Content logs/Producer-20260917-105157.stdout.log -Wait
```

### Compare runs:
```powershell
# Run 1
ls logs/*-20260917-100000.stdout.log

# Run 2  
ls logs/*-20260917-110000.stdout.log
```

## Backwards Compatible

- Works with existing `-Terminals $true` (no log files created in terminal mode)
- Only affects `-Terminals $false` (inline mode with file redirection)
- Can co-exist with old log files in logs/ directory

## Next Steps

After rebuilding, run:
```powershell
.\scripts\run-dashboard.ps1
```

Check logs directory:
```powershell
ls logs/
```

Expected output:
```
Mode  Name                                           LastWriteTime
----  ----                                           ----
-a--- Dashboard-20260917-105157.stdout.log          9/17/2026 10:51:57 AM
-a--- Dashboard-20260917-105157.stderr.log          9/17/2026 10:51:57 AM
-a--- Producer-20260917-105157.stdout.log           9/17/2026 10:51:58 AM
-a--- Producer-20260917-105157.stderr.log           9/17/2026 10:51:58 AM
-a--- DigitalChannels-20260917-105157.stdout.log    9/17/2026 10:51:58 AM
...
```

---

**Status:** ✅ Ready for testing

All log files will now have timestamps, making it easy to identify and track multiple runs.
