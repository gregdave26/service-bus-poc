# ADR 001: Emulator-First Validation Strategy

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

The POC must validate the entire Service Bus contact events topology before committing to cloud deployment. We need to balance speed of iteration, cost, and reproducibility.

## Problem Statement

- Development needs fast feedback loops to catch routing and integration errors early
- Cloud provisioning introduces delay and cost
- Cloud dependencies (credentials, subscriptions) create friction for team members
- Tests must be deterministic and reproducible without external dependencies

## Options Considered

### A. Emulator-First (Selected)
- Use Docker Compose with Azure Service Bus emulator for local development
- Validate all filter routing, consumer logic, and Carwash integration locally
- Deploy to real Azure only after emulator validation passes
- **Pros:** Fast iteration, zero cost, deterministic, no cloud credentials
- **Cons:** Single-instance, TCP-only, no persistence; minor behavioral differences from cloud

### B. Cloud-First
- Provision real Service Bus namespace on Azure immediately
- **Pros:** Real environment; catches cloud-specific issues early
- **Cons:** Slower, costly, requires credentials, non-deterministic

### C. Hybrid (Parallel)
- Develop locally with emulator and validate on real Azure before handoff
- **Pros:** Comprehensive validation
- **Cons:** Doubles effort; complexity

## Decision

**Use emulator-first validation for MVP (Phase 1–3).**

- Development and testing with Azure Service Bus emulator via Docker Compose
- All filter routing scenarios validated locally before cloud
- Phase 4 adds real Azure deployment via Bicep
- Pre-commit and CI tests run against emulator

## Consequences

### Positive
- Developers iterate 10x faster without cloud setup friction
- Zero cloud costs during MVP development
- Fully reproducible testing (no flaky external dependencies)
- Tests run offline; CI/CD works without cloud credentials

### Negative
- Emulator has known limitations (single instance, TCP only)
- Filter behavior may differ slightly from cloud
- Post-MVP, must validate against real Azure
- Filter syntax needs manual cloud compatibility check

### Mitigation
- Document emulator limitations upfront
- Write explicit filter tests comparing emulator with Azure docs
- Cloud validation in Phase 4 includes full regression testing
- Maintain feature parity between compose.yaml and Bicep

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Emulator filters don't match cloud filters | Medium | Test edge cases; verify syntax against Azure docs |
| Builds for emulator fail in cloud | Medium | Phase 4 regression testing on real Azure |
| Emulator instability | Low | Compose health checks; built-in restart |

## Trade-Offs

- **Speed vs. Fidelity:** Prioritizes development velocity over absolute cloud fidelity
- **Dev Experience vs. Realism:** Simpler setup traded for slightly less realistic environment

## Implementation Notes

- Emulator topology: `infra/servicebus/compose.yaml`
- Configuration: `infra/servicebus/config.json`
- Local run script: `scripts/run-local-poc.ps1`

## Sign-Off

- ✅ Product Owner (2026-09-15)
