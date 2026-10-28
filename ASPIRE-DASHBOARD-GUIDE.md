# Service Bus POC - Aspire Dashboard Guide

## Quick Start

To start the complete Service Bus POC development environment with Aspire:

```powershell
.\scripts\run-aspire.ps1
```

This will:
1. ✓ Start the Service Bus emulator
2. ✓ Start the Node.js dashboard UI (landing page)
3. ✓ Start all 10 Aspire-managed services
4. ✓ Open the dashboard in your browser

## Dashboard URLs

After `run-aspire.ps1` starts, you'll have access to:

### 📊 Primary Landing Page (Node.js Dashboard)
- **URL**: http://localhost:5100
- **Purpose**: Event publishing UI and system status visualization
- **Automatically opens**: in your default browser on startup

### 🔧 Aspire Dashboard
- **URL**: Printed to console (typically https://localhost:17149 or similar)
- **Purpose**: Service management, logs, traces, resource monitoring
- **Shows**: Status of all 10 services with detailed telemetry

### 🛒 Digital Site Services
- **CommerceApi (BFF)**: http://localhost:5200
- **CommerceToolsStub**: http://localhost:5201

## All Services

The Aspire orchestration manages 10 services:

### Contact Events (Service Bus topology)
1. **producer** - Publishes sample events every 3 seconds
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
2. **Publishing events**: Use the Node.js dashboard UI to publish test contact events
3. **Monitoring services**: Check the Aspire dashboard for live logs and traces
4. **Testing commerce APIs**: Use CommerceApi on http://localhost:5200

## About the Old Dashboard Script

The old `run-dashboard.ps1` script is now **superseded** by `run-aspire.ps1`.

**Why the change?**
- `run-aspire.ps1` uses Aspire's standardized orchestration (better for .NET development)
- Aspire provides superior service lifecycle management and observability
- The Node.js dashboard UI is still included for the familiar landing page experience

**Can I still use it?**
- The old script is kept for reference but no longer recommended
- It can be archived if you're fully migrated to Aspire

**Migration status**: ✓ **Complete** - All services are now Aspire-managed

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
- Check what's using port 5100, 5200, 5201
- Modify ports in `Program.cs` if needed
- Restart any conflicting services

## Architecture

```
┌─ run-aspire.ps1
│  ├─ Service Bus Emulator (Podman/Docker)
│  ├─ Node.js Dashboard UI (http://localhost:5100)
│  │  └─ Landing page for event publishing
│  └─ Aspire AppHost (https://localhost:PORT)
│     ├─ Service orchestration
│     ├─ 6 Contact Events services
│     ├─ 4 Digital Site services
│     └─ Dashboard with logs, traces, telemetry
```

## Next Steps

1. Run `.\scripts\run-aspire.ps1`
2. Navigate to http://localhost:5100 (auto-opens)
3. Use the dashboard to publish test events
4. Monitor services in the Aspire dashboard
5. Develop and test your features

---

For more information on Aspire, see: https://learn.microsoft.com/dotnet/aspire
