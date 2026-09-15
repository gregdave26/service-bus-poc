# ADR 008: Infrastructure as Code - Bicep Templates

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

Phase 4 requires deploying Service Bus infrastructure to real Azure (post-MVP). The infrastructure includes:
- Service Bus namespace
- `contact.events` topic
- 4 subscriptions with filters
- RBAC for least-privilege access
- Key Vault for secrets management

The IaC must be deployable via Azure CLI and maintainable by the team.

## Problem Statement

Which Infrastructure as Code language should we use for Azure Service Bus infrastructure?

- Must be Azure-native and easy to maintain
- Must support filtered subscriptions and RBAC
- Must be deployable via CLI (no manual steps)
- Must support environment-specific parameters (dev, staging, prod)

## Options Considered

### A. Bicep Templates (Selected)
- Modern, concise, Azure-native language
- Transpiles to ARM JSON automatically
- Native Azure tooling support
- Aligns with Microsoft CAF (Cloud Adoption Framework)
- **Pros:** Clean syntax, powerful, purpose-built for Azure, excellent tooling, future-proof
- **Cons:** Bicep-specific (not portable to other clouds)

### B. Terraform
- Cloud-agnostic, supports multi-cloud deployments
- Large ecosystem, many modules available
- **Pros:** Portable, powerful, widely adopted
- **Cons:** Overkill for single-cloud POC; more verbose for Azure; separate state management

### C. ARM Templates (JSON)
- Native Azure format, fully supported
- Verbose and harder to maintain
- **Pros:** Fully native to Azure; no transpilation
- **Cons:** JSON boilerplate; hard to read; harder to maintain

## Decision

**Use Bicep templates for Phase 4+ cloud deployment.**

- Bicep is the primary IaC language
- Modular Bicep files in `infra/modules/`
- Main orchestration template in `infra/main.bicep`
- Parameter files for environment-specific values
- Deployable via `az deployment group create --template-file infra/main.bicep`

## Consequences

### Positive
- Modern, clean syntax (much less boilerplate than ARM JSON)
- Purpose-built for Azure; native tooling integration
- Bicep best-practice alignment with Microsoft CAF
- Parameter files enable environment-specific deployments (dev/staging/prod)
- Automatic ARM JSON generation (no manual transpilation)
- Excellent VS Code extension support
- Easy to add to existing Azure subscriptions
- Modular approach enables reusable components

### Negative
- Bicep-specific (can't deploy to AWS/GCP)
- Smaller community than Terraform
- Less reusable modules in public registries (vs. Terraform)
- Requires Azure CLI for deployment (no cloud-agnostic tooling)

### Mitigation
- Document POC is Azure-only; multi-cloud not a requirement
- Use published Azure Verified Modules (AVM) for best-practices
- Store Bicep in version control; CI/CD validates with `az bicep build`

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Team unfamiliar with Bicep | Low | Provide templates and documentation; use AVM patterns |
| Deployment fails due to ARM/Bicep syntax | Low | Validate with `az bicep build` in CI; test on dev subscription first |
| RBAC too permissive or restrictive | Medium | Review with security team; follow CAF least-privilege pattern |

## Trade-Offs

- **Portability vs. Maintainability:** Single-cloud Bicep simpler than multi-cloud Terraform
- **Ecosystem vs. Purpose-Built:** Purpose-built Bicep over mature Terraform ecosystem

## Related Decisions

- **ADR 001:** Emulator-first (Bicep deployed in Phase 4, after MVP validation)
- **ADR 006:** Configuration (Bicep outputs can feed environment variables for apps)

## Implementation Notes

**Directory structure:**
```
infra/
├── main.bicep                          # Main orchestration
├── main.bicepparam                     # Default parameters
├── prod.bicepparam                     # Production overrides (optional)
├── modules/
│   ├── servicebus-namespace.bicep      # Service Bus namespace + RBAC
│   ├── topic.bicep                     # Topic + subscriptions
│   ├── role-assignments.bicep          # Least-privilege RBAC
│   └── keyvault.bicep                  # Optional: Key Vault for secrets
└── deploy.bicep                        # Helper template (optional)
```

**Main template structure (main.bicep):**
```bicep
metadata description = 'Service Bus contact events topology'

@minLength(1)
@maxLength(11)
param environment string

param location string = resourceGroup().location

@metadata({ description: 'Service Bus namespace name (must be globally unique)' })
param namespaceName string = 'sb-contact-${environment}-${uniqueString(resourceGroup().id)}'

module sbNamespace 'modules/servicebus-namespace.bicep' = {
  name: 'sbNamespace'
  params: {
    namespaceName: namespaceName
    location: location
    environment: environment
  }
}

module contactEventsTopic 'modules/topic.bicep' = {
  name: 'contactEventsTopic'
  params: {
    namespaceName: sbNamespace.outputs.namespaceName
    topicName: 'contact.events'
    subscriptions: [
      {
        name: 'digital-channels'
        sqlFilter: '1=1' // All events
      }
      {
        name: 'insurance'
        sqlFilter: 'attributes.hasInsurance = true'
      }
      {
        name: 'parks-resorts'
        sqlFilter: 'attributes.hasParksResorts = true'
      }
      {
        name: 'carwash'
        sqlFilter: 'attributes.hasCarwashProduct = true'
      }
    ]
  }
}

output namespaceName string = sbNamespace.outputs.namespaceName
output topicName string = contactEventsTopic.outputs.topicName
output subscriptions array = contactEventsTopic.outputs.subscriptions
```

**Parameter file (main.bicepparam):**
```bicep
using './main.bicep'

param environment = 'dev'
param location = 'australiaeast'
param namespaceName = 'sb-contact-dev-unique'
```

**Deployment command (PowerShell):**
```powershell
# Validate
az bicep build --file infra/main.bicep

# Deploy to resource group
az deployment group create `
  --resource-group rg-service-bus-poc `
  --template-file infra/main.bicep `
  --parameters infra/main.bicepparam
```

**Namespace module (modules/servicebus-namespace.bicep):**
```bicep
param namespaceName string
param location string
param environment string

resource sbNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    tags: {
      environment: environment
      createdBy: 'bicep'
    }
  }
}

output namespaceName string = sbNamespace.name
output connectionString string = listKeys(
  '${sbNamespace.id}/AuthorizationRules/RootManageSharedAccessKey',
  sbNamespace.apiVersion
).primaryConnectionString
```

**Topic module (modules/topic.bicep):**
```bicep
param namespaceName string

@metadata({ description: 'Topic name' })
param topicName string

@metadata({ description: 'Array of subscriptions with optional filters' })
param subscriptions array

resource sbNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' existing = {
  name: namespaceName
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2021-11-01' = {
  parent: sbNamespace
  name: topicName
  properties: {
    enablePartitioning: false
  }
}

resource subscriptionsRes 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2021-11-01' = [
  for sub in subscriptions: {
    parent: topic
    name: sub.name
    properties: {
      lockDuration: 'PT5M'
      requiresSession: false
      defaultMessageTimeToLive: 'P14D'
      deadLetteringOnFilterEvaluationExceptions: true
      deadLetteringOnMessageExpiration: true
    }
  }
]

resource filterRules 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2021-11-01' = [
  for (sub, i) in subscriptions: if (sub.sqlFilter != null) {
    parent: subscriptionsRes[i]
    name: 'filter-${i}'
    properties: {
      filterType: 'SqlFilter'
      sqlFilter: {
        sqlExpression: sub.sqlFilter
      }
    }
  }
]

output topicName string = topic.name
output subscriptions array = [
  for (sub, i) in subscriptions: {
    name: sub.name
    id: subscriptionsRes[i].id
  }
]
```

**CI/CD validation (.github/workflows/bicep-validate.yml):**
```yaml
name: Validate Bicep

on: [pull_request, push]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Validate Bicep
        run: |
          az bicep build --file infra/main.bicep
          az bicep build --file infra/modules/servicebus-namespace.bicep
          az bicep build --file infra/modules/topic.bicep
```

## Post-MVP Roadmap

Phase 4:
- Implement full RBAC module (least-privilege access for apps)
- Add Key Vault integration for connection strings
- Create environment parameter files (dev, staging, prod)
- Test deployment on real Azure subscription

Phase 5+:
- Add monitoring (Application Insights, log analytics)
- Implement cost optimization (reserved capacity, scaling policies)
- Consider multi-region deployment (if needed)

## Sign-Off

- ✅ Product Owner (2026-09-15)
