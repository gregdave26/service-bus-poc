# Quick Diagnostics Checklist - Service Bus Emulator

## 🔴 Symptom
Producer fails with: **"No connection could be made because the target machine actively refused it" (Error 10061)**

---

## 📋 Run These Commands NOW

### Step 1: Check Docker (requires admin/PowerShell)
```powershell
# Run PowerShell as Administrator, then:
docker version
docker ps
docker-compose --version
```

**Look for:**
- ✅ Docker version info (Client and Server)
- ✅ Two containers listed: `servicebus-emulator` and `sqledge`
- ✅ Status shows "Up" (not "Exited" or "Created")

**If failed:** Docker is not running or not accessible. Start Docker Desktop first.

---

### Step 2: Check Emulator Port Directly
```powershell
# Non-admin PowerShell:
$socket = New-Object System.Net.Sockets.TcpClient
try {
    $socket.Connect('localhost', 5672)
    Write-Host "✓ Port 5672 is accessible"
    $socket.Close()
} catch {
    Write-Host "✗ Port 5672 is NOT accessible (connection refused)"
}
```

**Result:**
- ✅ "accessible" → Emulator is running, issue is elsewhere
- ❌ "NOT accessible" → Emulator is NOT bound to port 5672 (containers not running or not ready)

---

### Step 3: Check Container Logs (if containers are running)
```powershell
# Admin PowerShell:
docker logs servicebus-emulator | tail -50
docker logs sqledge | tail -50
```

**Look for:**
- "Emulator Ready" (emulator startup complete)
- "successfully registered" (SQL Edge ready)
- Any errors or timeouts

---

## 🔧 Most Likely Fix

The wait time is too short. The emulator needs **45-60+ seconds**, not 15 seconds.

### Edit `scripts/run-dashboard.ps1`

**Find line 147-149:**
```powershell
# OLD (WRONG):
Write-Host "  Waiting 15 seconds for minimal initialization..."
Start-Sleep -Seconds 15
```

**Replace with:** (Copy one of the options below)

#### Option A: Simple Extended Wait (Quickest Fix)
```powershell
Write-Host "  Waiting 60 seconds for emulator to initialize..."
Start-Sleep -Seconds 60
Write-Host "  ✓ Ready to proceed"
```

#### Option B: Smart Wait + Port Check (Best)
```powershell
Write-Host "  Waiting for emulator AMQP port (5672) to become available..."
$maxWait = 120
$found = $false
for ($i = 1; $i -le $maxWait; $i++) {
    try {
        $socket = New-Object System.Net.Sockets.TcpClient
        $async = $socket.BeginConnect('localhost', 5672, $null, $null)
        if ($async.AsyncWaitHandle.WaitOne(1000)) {
            $socket.EndConnect($async)
            $socket.Close()
            Write-Host "  ✓ Port 5672 is responding ($($i)s)"
            $found = $true
            break
        }
    } catch {}
    if (-not $found -and $i % 5 -eq 0) {
        Write-Host "    [$($i)/$maxWait seconds] Still waiting..." -ForegroundColor Gray
    }
    Start-Sleep -Seconds 1
}
if (-not $found) {
    Write-Error "❌ Emulator did not become ready after $maxWait seconds"
    Write-Host "Troubleshooting:"
    Write-Host "  1. Check Docker Desktop is running"
    Write-Host "  2. Run: docker-compose -f infra/servicebus/compose.yaml ps"
    Write-Host "  3. Check logs: docker logs servicebus-emulator"
    exit 1
}
```

### Also Edit `scripts/run-local-poc.ps1`

**Find line 133-134:**
```powershell
# OLD (WRONG):
Write-Host "  Waiting 15 seconds for emulator to be ready..."
Start-Sleep -Seconds 15
```

**Replace with Option A or Option B above**

---

## ✅ After Applying Fix

1. Run dashboard again:
   ```powershell
   .\scripts\run-dashboard.ps1
   ```

2. Check logs:
   ```
   logs/Producer-stdout.log
   ```
   Should show: `Producer service starting... Service Bus connection successful!`

3. Open dashboard:
   ```
   http://localhost:5100
   ```
   Should show all services as **Online** (green)

---

## 🚨 If Still Failing

Run the diagnostic script:
```powershell
.\scripts\test-emulator-startup.ps1
```

This will:
- Clean up old containers
- Start fresh emulator
- Poll port 5672 until ready or timeout
- Show exact startup time and logs

---

## Reference: Why This Happens

1. `docker-compose up -d` = start containers in **background**
2. Doesn't wait for actual services to start
3. SQL Edge needs ~15-30 seconds to initialize
4. Service Bus emulator needs ~10-20 seconds to load config and bind to port
5. Total: 25-60+ seconds (your 15-second wait is too short)

---

## Still Stuck?

See full diagnosis: **EMULATOR_CONNECTIVITY_DIAGNOSIS.md**

Questions to ask:
- Is Docker Desktop running? (check system tray)
- Is port 5672 available? (no firewall rules or port conflicts?)
- Are containers actually running? (`docker ps`)
- What do the container logs say? (`docker logs servicebus-emulator`)
