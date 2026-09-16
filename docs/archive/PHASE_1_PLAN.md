# Phase 1: Foundation & Setup - Plan

**Status:** Ready for Orchestration  
**Date:** 2026-09-15  
**Duration:** 1–2 days (dev time)

---

## Goal

Establish the project structure, define canonical event contracts in JSON Schema, and scaffold .NET 10 console applications ready for producer and consumer implementation in Phase 2.

**Observable outcome:** All project folders created, JSON Schema event contracts defined, C# DTOs generated/coded, and bare `.csproj` files building successfully.

---

## Context

- **PROJECT_BRIEF.md:** Enterprise Contact Events POC using Azure Service Bus
- **IMPLEMENTATION_PLAN.md:** Phase breakdown and timeline
- **ADRs (001–008):** Technical decisions approved (async-first, separate apps, JSON Schema + annotations, environment variables, etc.)
- **Repository instructions:** `.github/copilot-instructions.md` (to be created with stack guidance)
- **Stack:** .NET 10, C# (nullable), xUnit, Moq, Docker Compose, Bicep, PowerShell 7+

---

## In Scope

### 1. Project Structure
- Create solution file (`ServiceBusPoc.sln`)
- Create 7 projects:
  - `src/ServiceBusPoc.Core/` — Shared contracts, utilities, settings models
  - `src/ServiceBusPoc.Producer/` — Event producer console app
  - `src/ServiceBusPoc.DigitalChannels/` — Consumer console app
  - `src/ServiceBusPoc.Insurance/` — Consumer console app
  - `src/ServiceBusPoc.ParksResorts/` — Consumer console app
  - `src/ServiceBusPoc.Carwash/` — Consumer + API console app
  - `src/ServiceBusPoc.Verifier/` — Scenario verifier console app
- Create test project: `tests/ServiceBusPoc.Tests/` (shared test utilities)
- Create test projects: `tests/ServiceBusPoc.*.Tests/` (per-app unit tests, optional in Phase 1)

### 2. Event Contracts (JSON Schema)
Define canonical schemas in `contracts/` folder:
- `contact-updated-v1.schema.json` — Contact property changes (required fields: contactId, firstName, lastName; optional: email, phone, attributes)
- `product-holding-change-v1.schema.json` — Product/holding system events (placeholder for future)
- `envelope.schema.json` — Event envelope wrapper (id, type, timestamp, dataVersion, correlationId, source)
- `attributes.schema.json` — Contact attributes object (hasInsurance, hasParksResorts, hasCarwashProduct as booleans)

### 3. C# DTOs & Validation
In `src/ServiceBusPoc.Core/`:
- `Contracts/ContactUpdatedEvent.cs` — DTO matching contact-updated schema
- `Contracts/ContactData.cs` — Nested contact data with data annotations
- `Contracts/ContactAttributes.cs` — Attributes object with boolean properties
- `Contracts/EventEnvelope.cs` — Generic event wrapper
- `Configuration/ServiceBusSettings.cs` — IOptions class for Service Bus config
- `Configuration/CarwashSettings.cs` — IOptions class for Carwash API config
- `Utilities/JsonSerializerOptions.cs` — Shared JSON serialization configuration

### 4. Shared Infrastructure (Core Project)
- `Logging/LoggingExtensions.cs` — Structured logging setup
- `DependencyInjection/ServiceCollectionExtensions.cs` — Common DI registration helpers
- `Validation/EventValidator.cs` — Shared schema validation logic (using data annotations)

### 5. Console App Scaffolds
Each consumer and producer gets:
- `Program.cs` with DI setup, configuration loading, `async Task Main`
- `*Service.cs` or `*Consumer.cs` stub (actual logic in Phase 2)
- Reference to `ServiceBusPoc.Core`
- `.csproj` with dependencies: Azure.Messaging.ServiceBus, Microsoft.Extensions.Hosting, Microsoft.Extensions.Options

### 6. Documentation
- `README.md` — Project overview, prerequisites, quick start
- `docs/architecture/project-goal.md` — System diagram and architecture overview
- `.github/copilot-instructions.md` — Stack-specific guidance for this project (C# async, DI, testing, etc.)

---

## Out of Scope

- ❌ Producer/consumer business logic (Phase 2)
- ❌ Carwash HTTP API (Phase 2)
- ❌ Service Bus emulator Docker Compose (Phase 2)
- ❌ Unit or integration tests (Phase 3)
- ❌ Bicep infrastructure (Phase 4)
- ❌ Real Azure deployment (Phase 4+)

---

## Tasks

### Producer (Remy)
1. **Review & Approve Plan** — Confirm scope, timeline, acceptance criteria
2. **Coordinate Handoff** — Ensure Dev team ready; flag any blockers
3. **Track Progress** — Daily standups; update progress note if delays
4. **Prepare Merge** — Review PR; confirm contracts match PROJECT_BRIEF

### Dev Team (Nova+Sage+Milo)
1. **Create Solution & Projects**
   - `dotnet new sln -n ServiceBusPoc`
   - Create 7 projects with correct folder structure
   - Add project references (all reference Core)
   - Verify `dotnet build` succeeds

2. **Define Event Contracts**
   - Write JSON Schema files in `contracts/`
   - Review against PROJECT_BRIEF event requirements
   - Add JSON Schema validation in schemas (required fields, type constraints)

3. **Code C# DTOs**
   - Generate or hand-code DTOs from JSON Schemas
   - Add data annotations (`[Required]`, `[EmailAddress]`, `[Range]`, etc.)
   - Ensure DTOs are serializable/deserializable with `System.Text.Json`
   - Test round-trip serialization (manual or unit test)

4. **Scaffold Core Project**
   - Shared configuration classes (ServiceBusSettings, CarwashSettings)
   - Shared utilities (logging, DI extensions, validator)
   - Verify Core compiles and references resolve

5. **Scaffold Consumer/Producer Apps**
   - Each app: `Program.cs` with DI setup and `async Task Main`
   - Each app: Settings model stub, consumer/producer service stub
   - Each app: `.csproj` dependencies (Azure SDK, Extensions, logging)
   - Verify all 7 projects build

6. **Documentation**
   - Write README.md with prerequisites, build/run commands
   - Create `docs/architecture/project-goal.md` with system diagram
   - Write `.github/copilot-instructions.md` with C#/.NET guidance

### QA (Ivy, optional)
- Not needed for Phase 1 (structural/schema work, low behavioral risk)
- Only if Producer requests validation of contract schemas

---

## Acceptance Criteria

### Code Quality
- [ ] `dotnet build` succeeds for entire solution (all 7 projects)
- [ ] No compiler errors or warnings (or justified with `#pragma`)
- [ ] All projects reference correct dependencies
- [ ] C# target is .NET 10, nullable reference types enabled
- [ ] No secrets or hardcoded values in code

### Event Contracts
- [ ] 4 JSON Schema files exist: `contact-updated-v1`, `product-holding-change-v1`, `envelope`, `attributes`
- [ ] Schemas match PROJECT_BRIEF event requirements (contact fields, attributes, versioning)
- [ ] Schemas are valid JSON Schema draft-7 (validate with `az schema validate` or similar)
- [ ] C# DTOs have `[Required]`, `[EmailAddress]`, `[Range]` annotations matching schema constraints
- [ ] DTOs can round-trip serialize/deserialize with `System.Text.Json.JsonSerializer`

### Project Structure
- [ ] 7 projects in correct folders (`src/ServiceBusPoc.*`)
- [ ] Core project contains contracts, settings, utilities (no business logic)
- [ ] Each consumer/producer app has `Program.cs` with DI setup
- [ ] All apps compile independently: `dotnet build --project src/ServiceBusPoc.Producer`

### Documentation
- [ ] README.md describes project, prerequisites (.NET 10, Docker, PowerShell 7+), how to build
- [ ] `docs/architecture/project-goal.md` includes system diagram (producers → topic → subscriptions → consumers)
- [ ] `.github/copilot-instructions.md` documents stack conventions (async, DI, validation, testing)

### Verification Commands
- [ ] `dotnet build` passes (full solution)
- [ ] `dotnet build --project src/ServiceBusPoc.Core` passes
- [ ] Each project builds independently
- [ ] No uncommitted changes except expected files

---

## Verification

### Automated
```bash
# Build entire solution
dotnet build

# Build individual projects (spot checks)
dotnet build --project src/ServiceBusPoc.Core
dotnet build --project src/ServiceBusPoc.Producer
dotnet build --project src/ServiceBusPoc.Insurance
dotnet build --project src/ServiceBusPoc.Carwash

# Verify no secrets or hardcoded connection strings
grep -r "Endpoint=sb://" src/ tests/
grep -r "SharedAccessKey=" src/ tests/
grep -r "connection-string" src/ tests/
# Should return EMPTY (no secrets found)
```

### Manual
- Review JSON Schema files for correctness
- Review README.md for clarity and completeness
- Check `.github/copilot-instructions.md` for stack guidance
- Verify all 7 project names match the plan

### Independent Review
- **Not required** — Phase 1 is structural/setup work; low behavioral risk
- Producer (Remy) reviews PR for completeness and correctness
- Dev lead spot-checks contracts for alignment with PROJECT_BRIEF

### QA
- **Not required** — No test-worthy behavior yet

---

## PR Checklist

When Dev opens PR:

- [ ] Branch name: `feat/phase1-foundation` or similar
- [ ] PR title: "Phase 1: Foundation & Setup (project structure, event contracts, .NET scaffolds)"
- [ ] PR description includes:
  - Summary: "Establishes project structure, event contracts, and .NET console app scaffolds"
  - Acceptance criteria checklist (copy from above)
  - Verification commands (copy from above)
  - Changes: "✓ 7 projects created, ✓ 4 JSON Schemas, ✓ C# DTOs, ✓ Docs"
- [ ] Verification section: results of `dotnet build` and spot checks
- [ ] Reviewers: @ai-team-producer (Remy)
- [ ] Labels: `phase-1`, `scaffold`, `architecture`
- [ ] Linked to this plan document

---

## Risks and Decisions

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Event schema ambiguity | Medium | Review schemas against PROJECT_BRIEF; Producer approves before merge |
| .csproj dependency mismatches | Low | Use latest stable Azure SDK; document versions in README |
| Circular project references | Low | Core → everything; consumers don't reference each other |
| DTO serialization issues | Low | Manual round-trip test in Program.cs before Phase 2 |

**Material Decisions (documented in ADRs):**
- Separate console apps per role (ADR-007)
- JSON Schema + data annotations validation (ADR-005)
- Configuration via environment variables (ADR-006)

---

## QA Risk Matrix

| Impact | Uncertainty | Result |
|--------|-------------|--------|
| High | Low | ✅ Optional QA — could skip; structural work |
| High | High | ⏭️ Not applicable here |
| Low | Low | ✅ Skip QA — clear structural work |
| Low | High | ⏭️ Not applicable here |

**Conclusion:** QA not needed for Phase 1. Producer reviews for completeness.

---

## Next Action

**Immediate:**
1. Producer (Remy): Review and approve this plan (confirm scope, timeline, acceptance criteria)
2. Dev Lead: Confirm team capacity and start date
3. Dev Team: Begin with `dotnet new sln` and project creation

**Timeline:**
- **Day 1:** Projects created, initial build passes, JSON Schemas defined
- **Day 2 (optional):** DTOs coded, Core project scaffolded, PR ready for review
- **Merge:** Producer approves; Phase 1 complete → Phase 2 begins

---

## Sign-Off

| Role | Status | Date |
|------|--------|------|
| Producer (Remy) | ⏳ Pending approval | — |
| Dev Lead (Nova/Sage/Milo) | ⏳ Ready to start | — |
| QA (Ivy, optional) | ⏭️ Not required | — |

---

## Related Documents

- [IMPLEMENTATION_PLAN.md](../../IMPLEMENTATION_PLAN.md) — Full phase breakdown
- [PROJECT_BRIEF.md](../../PROJECT_BRIEF.md) — Business goals and scope
- [ADR-005 (Schema Validation)](../../docs/decisions/005-schema-validation.md) — Contract design details
- [ADR-007 (Separate Apps)](../../docs/decisions/007-separate-apps.md) — Project structure rationale
- [ADR-006 (Configuration)](../../docs/decisions/006-configuration-env-vars.md) — Settings and IOptions usage
