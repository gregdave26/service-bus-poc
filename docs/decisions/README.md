# Architecture Decision Records (ADRs)

This folder contains all architecture and technical decisions for the Service Bus POC project.

---

## 📋 Quick Reference

| ADR | Title | Status | Impact |
|-----|-------|--------|--------|
| [SUMMARY](DECISIONS_SUMMARY.md) | Technical Decisions Summary | ✅ Approved | Read this first for overview |
| [001](001-emulator-first-validation.md) | Emulator-First Validation Strategy | ✅ Approved | MVP approach: local emulator before cloud |
| [002](002-carwash-in-memory-storage.md) | Carwash Contact Storage | Superseded | Historical proposal replaced by ADR-009 |
| [003](003-carwash-api-mock-pulse.md) | Carwash API Testing | Superseded | Generic endpoints replaced by ADR-009 |
| [004](004-async-first.md) | Async-First Programming Model | ✅ Approved | All I/O operations use async/await |
| [005](005-schema-validation.md) | Schema Validation | ✅ Approved | JSON Schema + data annotations |
| [006](006-configuration-env-vars.md) | Configuration & Secrets | ✅ Approved | Environment variables only (MVP) |
| [007](007-separate-apps.md) | Application Architecture | ✅ Approved | Separate console app per role |
| [008](008-bicep-iac.md) | Infrastructure as Code | ✅ Approved | Bicep templates for Azure deployment |
| [009](009-carwash-integration-boundary.md) | Carwash Integration Boundary | ✅ Approved | Independent consumer and Pulse-called verification API |
| [010](010-browser-dashboard.md) | Browser-Based Live Dashboard | ✅ Approved | Real-time service status and event publishing UI |

---

## 🎯 Reading Guide

### For First-Time Readers
1. Start with [DECISIONS_SUMMARY.md](DECISIONS_SUMMARY.md) for complete overview
2. Read each current ADR for detailed rationale and implementation notes; ADR-002 and ADR-003 are historical
3. Reference as you implement each component

### For Developers
- [ADR-004](004-async-first.md) — How to write async code
- [ADR-005](005-schema-validation.md) — Event schema and validation examples
- [ADR-006](006-configuration-env-vars.md) — Environment variable setup
- [ADR-007](007-separate-apps.md) — Project structure and separate console apps
- [ADR-009](009-carwash-integration-boundary.md) — Carwash consumer/API dependency direction

### For DevOps/Infrastructure
- [ADR-008](008-bicep-iac.md) — Cloud infrastructure templates
- [ADR-001](001-emulator-first-validation.md) — Local emulator setup (Phase 1–3)
- [ADR-006](006-configuration-env-vars.md) — Configuration management

### For QA/Testing
- [ADR-001](001-emulator-first-validation.md) — Test environment and reproducibility
- [ADR-009](009-carwash-integration-boundary.md) — Independent Carwash consumer and API validation
- [ADR-005](005-schema-validation.md) — Schema and validation testing

---

## 📊 Decision Summary by Phase

### MVP (Phases 1–3)
| Decision | MVP Approach |
|----------|--------------|
| Validation | ✅ Local emulator (ADR-001) |
| Storage | No Service Bus-backed verification store (ADR-009) |
| Pulse API | Mock Pulse calls `POST /carwash/v1/verify` (ADR-009) |
| Code Model | ✅ Async-first (ADR-004) |
| Validation | ✅ JSON Schema + annotations (ADR-005) |
| Configuration | ✅ Environment variables (ADR-006) |
| Architecture | ✅ Separate console apps (ADR-007) |
| Infrastructure | ✅ Docker Compose emulator |

### Phase 4+ (Post-MVP)
| Area | Post-MVP Enhancement |
|------|---------------------|
| Validation | Real Azure Service Bus |
| Storage | Add persistent verification data only if required |
| Pulse API | Live Pulse calls the Carwash verification API |
| Infrastructure | Bicep templates on real Azure (ADR-008) |
| Configuration | Key Vault integration (ADR-006 evolution) |

---

## 🔄 Relationships Between Decisions

```
ADR-001 (Emulator-First)
  ├─ Requires ADR-007 (Separate Apps)
  └─ Validated by ADR-005 (Schema Tests)

ADR-006 (Configuration)
  ├─ Used by all apps (ADR-007)
  ├─ Supports ADR-001 (emulator config)
  └─ Upgrades to Key Vault (Phase 4)

ADR-004 (Async-First)
  ├─ Required by ADR-007 (concurrent apps)
  └─ Supports ADR-008 (production-grade design)

ADR-009 (Carwash Integration Boundary)
  ├─ Supersedes ADR-002 where it couples storage to the API
  └─ Supersedes ADR-003's generic contact endpoints

ADR-008 (Bicep IaC)
  └─ Validates ADR-001 (emulator topology parity)
```

---

## 🚀 Implementation Checklist

- [ ] Read DECISIONS_SUMMARY.md (all team members)
- [ ] Read relevant ADRs for your role
- [ ] Confirm all approvals (Product Owner, Dev Lead, QA)
- [ ] Use ADRs as reference during implementation
- [ ] Add a new ADR when a material decision changes
- [ ] Link to relevant ADR in code comments/commit messages

---

## ✅ Approval Status

**Decision Authority:** Product Owner (Remy)

| Role | Decision | Date | Sign-Off |
|------|----------|------|----------|
| Product Owner | ADR-001–ADR-008 | 2026-09-15 | ✅ Approved |
| Product Owner | ADR-009 | 2026-09-16 | ✅ Approved |
| Dev Lead | TBD | TBD | ⏳ Pending |
| QA Lead (optional) | TBD | TBD | ⏳ Pending |

---

## 📝 Creating Future ADRs

If new architectural decisions arise during implementation:

1. Copy template from any existing ADR
2. Use next available number (e.g., ADR-009)
3. Include: Context, Problem, Options, Decision, Consequences, Risks, Trade-Offs
4. Add to this README
5. Link from DECISIONS_SUMMARY.md
6. Get Product Owner approval before implementation

**ADR Template Sections:**
- Status (PROPOSED, APPROVED, DEPRECATED)
- Date and decision authority
- Context and problem statement
- Options considered (with pros/cons)
- Selected decision and rationale
- Consequences (positive, negative, mitigations)
- Risks and trade-offs
- Implementation notes
- Related decisions
- Sign-off

---

## 🔗 Related Documents

- [IMPLEMENTATION_PLAN.md](../IMPLEMENTATION_PLAN.md) — Full phase breakdown, timeline, roles
- [PROJECT_BRIEF.md](../../PROJECT_BRIEF.md) — Business goals and scope
- DEVELOPER.md (forthcoming) — Code setup and patterns
- README.md (forthcoming) — Project overview and quick start

---

**Last Updated:** 2026-09-15  
**Maintained By:** Architecture Team
