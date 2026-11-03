# Service Bus POC - Aspire Dashboard Guide

## Quick Start

To start the complete Service Bus POC development environment with Aspire:

```powershell
.\scripts\run-aspire.ps1
```

This will:
1. ✓ Start the Service Bus emulator
2. ✓ Start the Node.js dashboard UI (landing page)
3. ✓ Start the Aspire-managed services
4. ✓ Open the landing page in your browser (unless `-NoBrowser`)

## Dashboard URLs

After `run-aspire.ps1` starts, you'll have access to:

### 📊 Primary Landing Page (Node.js Dashboard)
- **URL**: http://localhost:5100
- **Purpose**: Event publishing UI and system status visualization
- **Automatically opens**: in your default browser on startup

### 🔧 Aspire Dashboard
- **URL**: Printed to the console as `https://localhost:<port>/login?t=<token>`. The port and token change on every run, so copy the link from the console output.
- **Purpose**: Service management, logs, traces, resource monitoring
- **Shows**: Status and telemetry for the Aspire-managed services

### 🛒 Digital Site Services
- **CommerceApi (BFF)**: http://localhost:5200
- **CommerceToolsStub**: http://localhost:5201

## All Services

The Aspire orchestration manages 10 services:

### Contact Events (Service Bus topology)
1. **Contact Event Simulator** (`producer` service, Aspire resource `contact-event-simulator`) - Publishes one sample contact event per run, then shows `Completed`. Completed is not an error and does not age to `Offline`. Use the landing page's publish form to send further events. Continuous randomised simulation is on the backlog.
2. **digital-channels** - Consumes all events
3. **insurance** - Consumes events where hasInsurance=true
4. **parks-resorts** - Consumes events where hasParksResorts=true
5. **carwash** - Consumes events where hasCarwashProduct=true
6. **verifier** - Connectivity probe service

### Digital Site (Commerce)
7. **commerce-tools-stub** - commercetools API mock (port 5201)
8. **commerce-api** - Digital Site backend-for-frontend (port 5200)
9. **cart-processor** - Creates orders from authorized payments
10. **fulfilment-stub** - D365 F&O provisioning mock

## Usage Options

### Standard Usage
```powershell
# Start everything (emulator, services, dashboard UI, Aspire dashboard)
.\scripts\run-aspire.ps1

# Start but don't open browser
.\scripts\run-aspire.ps1 -NoBrowser

# Start without the Node.js dashboard UI
.\scripts\run-aspire.ps1 -NoUI

# Skip build and assume emulator is already running
.\scripts\run-aspire.ps1 -NoEmulator -NoBuild
```

### Development Tips

1. **Accessing the landing page**: Open http://localhost:5100 in your browser
2. **Publishing events**: Use the landing page's publish form to send test contact events (`POST /api/publish`)
3. **Monitoring services**: Check the Aspire dashboard for live logs and traces
4. **Testing commerce APIs**: Use CommerceApi on http://localhost:5200

## About the Old Dashboard Script

The old `run-dashboard.ps1` script is superseded by `run-aspire.ps1`.

**Why the change?**
- `run-aspire.ps1` uses Aspire's orchestration for the .NET services and telemetry
- The Node.js dashboard UI is still started by the launcher, so the landing page is unchanged

**Can I still use it?**
- `run-dashboard.ps1` is still in the repository but is no longer recommended. It can be removed once the team confirms it is not needed.

**Migration status**: The .NET services run under the Aspire AppHost. The Node.js UI is started by `run-aspire.ps1`, not by the AppHost.

## Troubleshooting

### Dashboard UI doesn't start
- Ensure Node.js is installed: `node --version`
- Check logs in `logs/dashboard-ui-*.log`
- Run with `-NoUI` and access Aspire dashboard directly

### Aspire CLI not found
- Install: `dotnet tool install -g Aspire.CLI`
- Or ensure `~/.nuget/packages/aspire.cli.win-x64` has the latest version

### Services fail to start
- Check `.env` file for required configuration
- Verify Service Bus emulator is running (should start automatically)
- Review service logs in Aspire dashboard

### Port already in use
- Check what's using port 5100 (`$dashboardUiPort` in `run-aspire.ps1`), 5200, or 5201 (set in `src/ServiceBusPoc.AppHost/Program.cs`)
- Change the ports in those files if needed
- Restart any conflicting services

## Architecture

```
┌─ run-aspire.ps1
│  ├─ Service Bus Emulator (Podman/Docker)
│  ├─ Node.js Dashboard UI (http://localhost:5100)
│  │  └─ Landing page for event publishing
│  └─ Aspire AppHost (dashboard URL printed at startup)
│     ├─ Service orchestration
│     ├─ 6 Contact Events services
│     ├─ 4 Digital Site services
│     └─ Dashboard with logs, traces, telemetry
```

## Next Steps

1. Run `.\scripts\run-aspire.ps1`
2. Navigate to http://localhost:5100 (auto-opens)
3. Use the landing page to publish test events
4. Monitor services in the Aspire dashboard
5. Develop and test your features

---

For more information on Aspire, see: https://learn.microsoft.com/dotnet/aspire
