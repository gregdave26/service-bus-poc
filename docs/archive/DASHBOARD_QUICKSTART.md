# 🚀 Getting Started with the Service Bus POC Dashboard

## Quick Start (30 seconds)

```powershell
cd C:\Users\dg15938\development\service-bus-poc.worktrees\service-bus-dashboard-logging
.\scripts\run-dashboard.ps1
```

That's it! The script will:
1. ✅ Start the Azure Service Bus emulator (Docker)
2. ✅ Build all 8 projects
3. ✅ Start the Dashboard (http://localhost:5100)
4. ✅ Start the Producer (publishes events every 3 seconds)
5. ✅ Start all 4 Consumers (filter and log messages)
6. ✅ Open your browser automatically

## What You'll See

### Dashboard (Browser)
- **URL:** http://localhost:5100
- Real-time status for all 5 services (Producer + 4 Consumers)
- Message counts and last-seen timestamps
- **Publish Event** form with capability flags:
  - ☑ Has Insurance
  - ☑ Has Parks/Resorts
  - ☑ Has Carwash Product

### Consumers (Console)
Each consumer logs only messages that pass its subscription filter:

```
Insurance:
  ✓ Received event event-123 (hasInsurance=true)
  
DigitalChannels:
  ✓ Received event event-123 (no filter - always receives)
  
ParksResorts:
  ✓ Received event event-124 (hasParksResorts=true)
  
Carwash:
  ✓ Received event event-125 (hasCarwashProduct=true)
```

## Testing Subscription Filters

1. **Open dashboard:** http://localhost:5100
2. **Click "Publish Event"** button
3. **Toggle capability flags** (Insurance, Parks, Carwash)
4. **Watch consumer consoles** — only matching consumers log the message

### Example Test Scenarios

| Event Flags | Who Receives It? |
|-------------|-----------------|
| None (all false) | DigitalChannels only |
| Insurance only | Insurance + DigitalChannels |
| Parks only | ParksResorts + DigitalChannels |
| Carwash only | Carwash + DigitalChannels |
| All three | All 4 consumers + Producer logs it |

## Console Output Example

```
╔════════════════════════════════════════════════════════════════════════════╗
║          SERVICE BUS POC - DASHBOARD + SERVICES (Interactive)             ║
╚════════════════════════════════════════════════════════════════════════════╝

Configuration:
  Dashboard port: 5100
  Dashboard URL: http://localhost:5100
  Auto-open browser: Yes
  Start emulator: Yes

...

STEP 4: Starting applications...

  ✓ Dashboard (Status & Event Publishing)
  ✓ Producer (Event Publisher)
  ✓ DigitalChannels (Receives all events)
  ✓ Insurance (Receives hasInsurance=true)
  ✓ ParksResorts (Receives hasParksResorts=true)
  ✓ Carwash (Receives hasCarwashProduct=true)

╔════════════════════════════════════════════════════════════════════════════╗
║                            ✓ All services running                         ║
╚════════════════════════════════════════════════════════════════════════════╝

Dashboard:
  📊 http://localhost:5100

Services:
  🔵 Producer      - Publishing sample events every 3 seconds
  🟢 Digital Channels - Receives all events (no filter)
  🟢 Insurance      - Receives hasInsurance=true
  🟢 Parks & Resorts - Receives hasParksResorts=true
  🟢 Carwash        - Receives hasCarwashProduct=true

Press Ctrl+C to stop all services...
```

## Script Options

### Basic (recommended):
```powershell
.\scripts\run-dashboard.ps1
```

### Without browser auto-open:
```powershell
.\scripts\run-dashboard.ps1 -NoBrowser
```

### Custom dashboard port:
```powershell
.\scripts\run-dashboard.ps1 -DashboardPort 8080
```

### Skip emulator (if already running):
```powershell
.\scripts\run-dashboard.ps1 -NoEmulator
```

## Alternative Scripts

### For CI/CD / Automated Testing:
```powershell
.\scripts\run-local-poc.ps1
```
- Runs all services, validates with Verifier, generates report
- Best for PR validation

### For Console Debugging:
```powershell
.\scripts\debug-run.ps1
```
- Console-based without dashboard
- Better for detailed debugging

## Stopping Services

Press **Ctrl+C** in the terminal running the script. It will gracefully shut down:
- All consumer processes
- Producer process
- Dashboard server
- Docker emulator (if started by script)

## Troubleshooting

### "Docker not found or not running"
→ Start Docker Desktop

### "Port 5100 already in use"
→ Use `-DashboardPort 8080` or kill the process using port 5100

### Services start but don't receive messages
→ Check logs: `Get-Content logs/run-dashboard-*.log -Tail 50`
→ Verify emulator: `docker-compose ps`

### Dashboard shows "Offline" for all services
→ Check Service Bus connection: `$env:ServiceBus__ConnectionString`
→ Wait 15-20 seconds (services report heartbeats every 5 seconds)

## What's Happening Behind the Scenes

1. **Azure Service Bus Emulator** — Runs in Docker
   - Topic: `contact.events`
   - Subscriptions: `digital-channels` (no filter), `insurance` (hasInsurance=true), `parks-resorts` (hasParksResorts=true), `carwash` (hasCarwashProduct=true)

2. **Dashboard** — HTTP server
   - Collects heartbeats from all services
   - Ages heartbeats (15 second timeout)
   - Serves status API + publish endpoint
   - Hosts browser UI

3. **Producer** — Publishes events
   - Creates `EventEnvelope<ContactData>` messages
   - Includes capability flags in message properties
   - Reports heartbeats to dashboard

4. **Consumers** — Receive and filter
   - Broker delivers only messages matching subscription filter
   - Consumers log what they receive
   - Report heartbeats with message counts

## Next Steps

- 📊 **Explore the dashboard:** Test different capability flag combinations
- 🔍 **Check filtering:** Verify only matching consumers receive each event
- 🧪 **Verify routing:** Compare published flags with consumer logs
- 📚 **Read docs:** [docs/decisions/010-browser-dashboard.md](../docs/decisions/010-browser-dashboard.md)

## Architecture

```
┌─────────────────────────────────────────────────────┐
│  Browser: http://localhost:5100 (Dashboard UI)    │
│  • View service status                             │
│  • Publish test events                             │
└─────────────────┬───────────────────────────────────┘
                  │ POST /api/publish
                  │ GET /api/status
                  ▼
┌─────────────────────────────────────────────────────┐
│  Dashboard HTTP Server (localhost:5100)             │
│  • Aggregates heartbeats                           │
│  • Ages status (15 sec timeout)                    │
│  • Routes publish requests                         │
└─────────────────┬───────────────────────────────────┘
                  │ Publish to topic
        ┌─────────┴─────────┐
        │                   │
        ▼                   ▼
┌──────────────┐    ┌──────────────┐
│  Producer    │    │   Docker     │
│              │    │   Emulator   │
│ • Publishes  │◄──►│              │
│   events     │    │ • Topic:     │
│ • Heartbeats │    │   contact.   │
│   to API     │    │   events     │
└──────────────┘    │              │
                    │ • Subscri-   │
        ┌───────────┤   ptions &   │
        │           │   filters    │
        ▼           └──────────────┘
┌────────────────────────────────────┐
│ Consumers (4 parallel)             │
│                                    │
│ 🟢 DigitalChannels  (no filter)    │
│ 🟢 Insurance        (hasInsurance) │
│ 🟢 ParksResorts     (hasParks)     │
│ 🟢 Carwash          (hasCarwash)   │
│                                    │
│ Each logs to console:              │
│ ✓ Received event-123               │
└────────────────────────────────────┘
```

---

**For more details, see:**
- `scripts/README.md` — All script options and workflows
- `docs/decisions/010-browser-dashboard.md` — Design decision and rationale
- `PROJECT_BRIEF.md` — Complete project scope
