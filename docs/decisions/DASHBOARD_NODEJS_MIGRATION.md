# Dashboard: C# to Node.js Migration (Future Consideration)

**Status:** Deferred (2026-09-17)  
**Effort:** ~2-3 hours  
**Priority:** Low (current C# version works, dashboard is optional)

## Context

The dashboard is a lightweight HTTP server with embedded UI. Currently implemented in C# using `HttpListener` with embedded HTML/CSS/JS (DashboardHttpServer.cs, ~400 lines).

While functional, using C# for this simple stateless server feels like overkill:
- No complex business logic
- No dependency on .NET-specific features
- Just endpoint handlers + heartbeat aggregation + JSON serialization
- Mostly I/O bound (not compute-heavy)

## Consideration

Migrate to lightweight Node.js implementation using Express.js:
- Single file or minimal structure
- Same 4 endpoints (`/`, `/api/status`, `/api/publish`, `/api/heartbeat`)
- Same HTML/CSS/JS UI (can be copied as-is)
- Azure SDK already available (`@azure/service-bus`)

## Why Node.js Better Fits Here

✅ No runtime bloat for a stateless HTTP server  
✅ Faster startup  
✅ Cross-platform without .NET dependency  
✅ Easier modification for non-.NET developers  
✅ Single-responsibility alignment (lightweight observability tool, not business service)  

## Why Keep C# (For Now)

✅ Already implemented and working  
✅ Dashboard is optional per ADR-010 (doesn't block messaging)  
✅ Effort on debugging C# startup issues ≈ effort on Node.js port  
✅ Operational simplicity (one runtime, .NET stack)  

## Estimated Effort Breakdown

```
Express.js setup             30 min
Route handlers              30 min
Status registry (copy logic) 45 min
HTML UI (copy as-is)        15 min
Event publishing (SDK)      45 min
Error handling & logging    30 min
Testing/verification        30 min
─────────────────────────────────
Total: ~3.5 hours max
```

## Recommended Next Steps

1. **If dashboard becomes critical path or deployment pain:** Migrate to Node.js
2. **If C# startup continues to be problematic:** Migrate to Node.js
3. **If adding complex dashboard features:** Node.js might be easier to extend
4. **Otherwise:** Keep C# version, prioritize messaging system stability

## Decision

**Defer migration.** Revisit if:
- Dashboard stability becomes a requirement
- Deployment model changes (e.g., cloud-native, containerized per service)
- C# runtime size becomes a concern
- Dashboard needs significant new features

## Related

- ADR-010: Browser Dashboard (explains optional nature)
- `/src/ServiceBusPoc.Dashboard/` (current C# implementation)
- `/scripts/` (startup automation)
