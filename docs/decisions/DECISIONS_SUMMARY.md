# Technical Decisions Summary

**Date:** 2026-09-15  
**Status:** All Approved ✅  
**Document:** Reference for team during implementation

---

## Decision Matrix

| # | Decision | Approval | Rationale | File |
|---|----------|----------|-----------|------|
| 1 | Emulator-first validation | ✅ Yes | Fast iteration, deterministic tests, zero cost | [ADR-001](001-emulator-first-validation.md) |
| 2 | In-memory Carwash storage | ✅ In-memory (MVP) | Simple, zero dependencies, acceptable for MVP | [ADR-002](002-carwash-in-memory-storage.md) |
| 3 | Carwash API testing | ✅ Mock Pulse client | Self-contained testing, no external dependencies | [ADR-003](003-carwash-api-mock-pulse.md) |
| 4 | Async/await strategy | ✅ Async-first | Efficient resource utilization, production-grade design | [ADR-004](004-async-first.md) |
| 5 | Schema validation | ✅ JSON Schema + annotations | Standard .NET approach, no extra dependencies | [ADR-005](005-schema-validation.md) |
| 6 | Configuration & secrets | ✅ Environment variables | Simple, zero friction, no risk of secret leakage | [ADR-006](006-configuration-env-vars.md) |
| 7 | Application architecture | ✅ Separate console apps | Clear separation of concerns, independent testing | [ADR-007](007-separate-apps.md) |
| 8 | Infrastructure as Code | ✅ Bicep templates | Azure-native, clean syntax, purpose-built | [ADR-008](008-bicep-iac.md) |

---

## Key Technology Selections

### .NET & Runtime
- **Language:** C# .NET 10 (nullable enabled)
- **Async Model:** Async-first (`async Task`, `await`)
- **Configuration:** `IConfiguration` + `IOptions<T>` (environment variables only for MVP)
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection`
- **Logging:** `ILogger` with structured JSON output

### Messaging & Integration
- **Service Bus:** Azure Service Bus SDK (cloud + emulator)
- **Topology:** Local Docker Compose emulator → Phase 4 Bicep cloud deployment
- **Filter Routing:** SQL filter expressions on subscriptions
- **Carwash API:** HTTP endpoints (minimal WebAPI or console-based listener)
- **Pulse Integration:** Mock client in MVP → real HTTP client post-MVP

### Data & Storage
- **Carwash Contact Store:** In-memory `ConcurrentDictionary` (MVP)
- **Event Serialization:** JSON with data annotations validation
- **Schema Definition:** JSON Schema + C# DTOs with `[Required]`, `[EmailAddress]`, etc.

### Testing & Validation
- **Test Framework:** xUnit
- **Mocking:** Moq
- **Scenarios:** Deterministic end-to-end in `ScenarioVerifier`
- **Coverage Target:** ≥80% meaningful code coverage
- **CI/CD:** `dotnet build`, `dotnet test`, `az bicep build`

### Infrastructure
- **IaC Language:** Bicep (Phase 4+)
- **Deployment:** Azure CLI (`az deployment group create`)
- **Local Environment:** Docker Compose with Service Bus emulator
- **Environment Config:** PowerShell scripts + environment variables

---

## Implementation Sequence

```
Phase 1: Foundation (1-2 days)
├─ Project structure & .NET scaffolds
├─ Event contract JSON Schemas
└─ C# DTOs with validation

Phase 2: Core MVP (3-5 days)
├─ Docker Compose emulator topology
├─ Producer (publishes events)
├─ 4 Consumers (route by subscription)
├─ Carwash consumer + in-memory storage + HTTP API
└─ Scenario verifier (end-to-end testing)

Phase 3: Tests (2-3 days, parallel with Phase 2)
├─ Schema validation tests
├─ Filter routing tests (all 8 scenarios)
├─ Carwash integration tests
└─ Configuration tests

Phase 5.1: Local Scripts (1 day)
└─ run-local-poc.ps1 (orchestrates MVP)

[MVP Complete ✅]

Phase 4: Cloud IaC (2-3 days)
├─ Bicep modules (namespace, topic, subscriptions)
├─ RBAC role assignments
└─ deploy-azure.ps1

Phase 6: Documentation (1-2 days, parallel)
├─ Architecture guide
├─ Developer guide
├─ Runbook
└─ API reference
```

---

## Architecture Overview

```
Producers (CRM/MDM, Product Systems)
  ↓
Service Bus Topic: contact.events
  ↓
┌─────────────────────────────────────┐
│ Subscriptions (with SQL filters)   │
├─────────────────────────────────────┤
│ • digital-channels (no filter)     │
│ • insurance (hasInsurance=true)    │
│ • parks-resorts (hasParksResorts=true)│
│ • carwash (hasCarwashProduct=true) │
└─────────────────────────────────────┘
  ↓
Consumers
├─ DigitalChannels (all events, validation harness)
├─ Insurance (filtered events)
├─ ParksResorts (filtered events)
└─ Carwash Consumer
   ├─ Listens to carwash subscription
   ├─ Stores contacts in-memory
   └─ Exposes HTTP API for Pulse to call
      ↓
      Mock Pulse (MVP) / Real Pulse (Phase 5)
```

---

## Critical Constraints & Assumptions

### MVP Constraints
- ✗ No persistent storage for contacts (in-memory only)
- ✗ No real Pulse API calls (mock only)
- ✗ No cloud deployment (emulator only)
- ✗ No multi-region or HA (single emulator instance)
- ✓ Local Docker required for emulator
- ✓ All 8 filter routing scenarios validated

### Post-MVP Additions
- Phase 4: Real Azure deployment via Bicep
- Phase 5: Real Pulse API integration
- Phase 5+: Persistent storage (EF Core + database)
- Phase 5+: HA, monitoring, load testing

### Security & Compliance
- ✓ No secrets committed to repo
- ✓ All configuration via environment variables (MVP)
- ✓ Least-privilege RBAC (Phase 4 Bicep)
- ✓ Connection strings in Key Vault (Phase 4+)

---

## Risk Mitigation Strategies

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Emulator filter behavior differs from cloud | Medium | ADR-001: Test edge cases; Phase 4 validates on real Azure |
| Carwash storage not persistent | Low | ADR-002: Documented MVP limitation; Phase 5 adds database |
| Pulse integration untested | Low | ADR-003: Mock client validates API shape; Phase 5 integrates real API |
| Async code complexity | Low | ADR-004: Code templates + documentation; pair programming |
| Schema/DTO drift | Medium | ADR-005: Unit tests validate round-trip serialization |
| Secret leakage | High | ADR-006: Pre-commit hook + grep checks; CI validates |
| App orchestration complex | Medium | ADR-007: PowerShell script orchestrates; clear documentation |
| Bicep deployment fails | Low | ADR-008: CI validates with `az bicep build` |

---

## Validation Checklist for MVP Completion

### Code Quality
- [ ] `dotnet build` succeeds for all projects
- [ ] `dotnet test` passes all tests
- [ ] Test coverage ≥80% for business logic
- [ ] No compiler warnings (or justified with `#pragma`)
- [ ] No secrets in any committed code (verified by grep)
- [ ] All async code reviewed (no `.Result` or `.Wait()`)

### Functional Validation
- [ ] All 8 filter routing scenarios pass (DigitalChannels, Insurance, ParksResorts, Carwash)
- [ ] Producer successfully publishes events to topic
- [ ] 4 consumers receive messages from correct subscriptions
- [ ] Carwash stores contacts in-memory and serves via API
- [ ] Mock Pulse client makes valid HTTP calls to Carwash API

### Integration Validation
- [ ] `scripts/run-local-poc.ps1` completes end-to-end without errors
- [ ] Scenario verifier logs show all test scenarios passed
- [ ] Docker Compose spins up emulator and applies topology correctly
- [ ] Consumers start and process messages within 10 seconds
- [ ] No orphaned processes after script completes

### Documentation
- [ ] README enables new dev setup in <30 minutes
- [ ] DEVELOPER.md documents all configuration variables
- [ ] `.env.example` lists all required environment variables
- [ ] ADR-001 through ADR-008 complete and approved
- [ ] Architecture diagram in `docs/architecture/project-goal.md`

### Security
- [ ] No connection strings in code (all via environment variables)
- [ ] No API keys in compose.yaml (use secrets or env vars)
- [ ] `.gitignore` excludes config files and `.env`
- [ ] Pre-commit hook rejects common secret patterns (post-MVP: add to CI)

---

## Success Metrics

**MVP Approval Criteria:**
1. ✅ Technical decisions documented and approved
2. ✅ All code builds without warnings
3. ✅ All automated tests pass (≥80% coverage)
4. ✅ All 8 filter routing scenarios validated locally
5. ✅ Carwash integration produces valid API responses
6. ✅ Single command (`run-local-poc.ps1`) validates end-to-end
7. ✅ Zero secrets leaked to repository
8. ✅ New developer can onboard in <30 minutes
9. ✅ ADRs document all key decisions
10. ✅ Team ready for Phase 4 cloud deployment

---

## Next Steps

1. **Communicate Decisions** → Share this summary + 8 ADRs with team
2. **Approve Plan** → Product Owner (Remy) confirms implementation can begin
3. **Launch Phase 1** → Dev team (Nova+Sage+Milo) begins project structure + contracts
4. **Setup Parallel Work** → QA (Ivy, optional) defines filter routing test scenarios
5. **Daily Standups** → Track progress against Phase 1 → Phase 2 → Phase 3 timeline

---

## Document References

- [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md) — Full phase breakdown and timeline
- [PROJECT_BRIEF.md](../../PROJECT_BRIEF.md) — Business goals and scope
- ADR-001 through ADR-008 — Detailed justification for each decision

---

**Approval & Sign-Off:**
- Product Owner (Remy): ⏳ Pending
- Dev Lead: ⏳ Pending
- QA Lead (optional, Ivy): ⏳ Pending
