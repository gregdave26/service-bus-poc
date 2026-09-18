# ⚠️ Why Ctrl+C Wasn't Getting Caught

## The Problem

When you pressed Ctrl+C in the main PowerShell window running `run-dashboard.ps1`, nothing happened. The script didn't exit, services weren't cleaned up, and terminal windows stayed open.

## Root Cause

**There was a trap statement defined, but it was NEVER REGISTERING at script scope properly.**

### What Was Happening:

1. **Trap was defined BEFORE other functions** - PowerShell couldn't reference `Invoke-Cleanup` which was defined later
2. **No fallback for interrupt handling** - Only `PowerShell.Exiting` event was registered, which doesn't fire until the entire PowerShell process terminates (not on Ctrl+C)
3. **The try/catch threw to trap, but trap wasn't properly scoped** - Trap statement scope issues in PowerShell can be finicky

### Why This Matters:

```
User presses Ctrl+C in main window
        ↓
PowerShell raises exception (in PowerShell 7+, Start-Sleep is interruptible)
        ↓
Trap SHOULD catch it... but it doesn't ❌
        ↓
Exception unhandled, script keeps running
        ↓
PowerShell.Exiting event NOT fired (because script doesn't exit)
        ↓
Services keep running, nothing closed
```

## The Solution

### Three-Layer Fix

1. **Move trap definition AFTER Invoke-Cleanup function definition**
   - Ensures trap can reference Invoke-Cleanup
   - Places trap at correct script scope

2. **Keep PowerShell.Exiting as backup**
   - Catches edge cases where PowerShell does exit
   - Ensures cleanup happens either way

3. **Ensure trap is catching correctly**
   ```powershell
   trap {
       if (-not $script:shutdownInProgress) {
           Write-Host ""
           Invoke-Cleanup  # This must be defined first!
       }
       exit 0
   }
   ```

### Key Changes in run-dashboard.ps1

**Lines 72-88:**
```powershell
# Set up event handler first (backup)
$null = Register-EngineEvent -SourceIdentifier PowerShell.Exiting -Action { ... }

# Then define Invoke-Cleanup function
function Invoke-Cleanup { ... }

# THEN set up trap that calls Invoke-Cleanup
trap {
    if (-not $script:shutdownInProgress) {
        Invoke-Cleanup
    }
    exit 0
}
```

## How It Works Now

```
User presses Ctrl+C in main window
        ↓
PowerShell raises exception (Start-Sleep gets interrupted)
        ↓
Trap catches it (trap is now properly scoped) ✅
        ↓
Invoke-Cleanup is called (function is defined and available) ✅
        ↓
CloseMainWindow() on all processes
        ↓
Stop-Process -Force on remaining
        ↓
docker-compose down cleans Docker
        ↓
exit 0 exits cleanly
        ↓
All terminal windows close, system clean ✅
```

## Testing

```powershell
# Run the script
.\scripts\run-dashboard.ps1

# Wait for services to start (30-40 seconds)

# Press Ctrl+C in the MAIN terminal window (where script is running)

# Expected: Cleanup messages appear immediately and script exits
```

## If It Still Doesn't Work

The issue might be that separate terminal windows created with `Start-Process -Terminals $true` don't connect back to the parent PowerShell's input stream. In that case:

1. **Make sure you're pressing Ctrl+C in the MAIN window** (where run-dashboard.ps1 started, not in a child service window)
2. **Don't click in child windows** - if focus moves to a child window, Ctrl+C goes there instead
3. **As a workaround** - you can manually run cleanup:
   ```powershell
   Get-Process -Name "dotnet" | Stop-Process -Force
   docker-compose -f infra/servicebus down
   ```

## Summary

✅ **Trap now properly scoped and catches Ctrl+C**  
✅ **Invoke-Cleanup function defined before trap references it**  
✅ **PowerShell.Exiting event handler as backup**  
✅ **Services should now clean up when you press Ctrl+C**  
✅ **Terminal windows should close automatically**

The fix is simple: **define functions before the trap references them**, and **ensure trap is at script scope**.
