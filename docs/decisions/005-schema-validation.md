# ADR 005: Schema Validation - JSON Schema + Data Annotations

**Status:** APPROVED (2026-09-15)  
**Date:** 2026-09-15  
**Decided By:** Product Owner  

---

## Context

Event contracts are defined in JSON Schema (canonical format for producer and consumer systems). The POC must validate that received events conform to schema and serialize/deserialize correctly.

## Problem Statement

How should we validate contact events against their JSON Schema definitions?

- Producers emit JSON events over Service Bus
- Consumers must validate incoming JSON against contract schema
- Errors on invalid data (missing fields, wrong types, out-of-range values)
- Balance between schema-as-code vs. schema-as-data

## Options Considered

### A. JSON Schema + Data Annotations (Selected)
- Define canonical schemas in JSON Schema files (e.g., `contact-updated-v1.schema.json`)
- Define C# DTOs with data annotations (`[Required]`, `[EmailAddress]`, etc.)
- Deserialize JSON → DTO; framework validates automatically
- DTOs serve as source of truth for C# code
- **Pros:** Explicit in C#; standard validation attributes; no extra dependencies; testable
- **Cons:** Manual DTO creation or schema-to-code generation step; schema duplication

### B. JSON Schema Only with Custom Validation
- Store schemas in JSON files
- Write custom validation logic in C# to check schema compliance
- Schemas are source of truth
- **Pros:** Single source of truth (JSON Schema)
- **Cons:** More code to maintain; no framework support; reinventing the wheel

### C. Fluent Validation
- Define DTOs in C# without annotations
- Use FluentValidation library for all validation rules
- More explicit and testable rules
- **Pros:** Powerful, flexible, highly testable
- **Cons:** Extra dependency; more boilerplate; overkill for simple contracts

## Decision

**Use JSON Schema + data annotations.**

- Canonical event schemas stored in `contracts/` as JSON Schema files
- C# DTOs hand-coded or generated with data annotations matching schema
- Use `System.ComponentModel.DataAnnotations` for validation
- Deserialize JSON → validate DTO → use in code
- Unit tests validate serialization/deserialization

## Consequences

### Positive
- Standard .NET validation approach
- No extra dependencies (data annotations built-in)
- Clear contract definition in both JSON and C#
- Framework handles common validations (`[Required]`, `[EmailAddress]`, `[Range]`, etc.)
- Easy to test validation rules
- IDE autocomplete and intellisense on DTOs

### Negative
- Schema duplication (JSON + C# code)
- Manual DTO creation (no automatic code generation in MVP)
- Drift risk: schema and DTO get out of sync
- Complex validations may require custom attributes

### Mitigation
- Document schema in JSON and C# side-by-side
- Add unit tests validating DTO can round-trip (serialize/deserialize)
- Future: Consider schema-to-code generation tool (post-MVP)
- Code review enforces DTO↔Schema consistency

## Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Schema and DTO drift out of sync | Medium | Unit tests for serialization; code review checklist |
| Complex validation rules hard to express with annotations | Medium | Use custom attributes or post-deserialization validation |
| No explicit schema versioning | Low | Include `dataVersion` field in envelope; document in schema file |

## Trade-Offs

- **Schema as Code vs. Schema as Data:** Use both; JSON is canonical for external systems
- **Automation vs. Simplicity:** Manual DTO creation simpler for MVP; generation tool future work

## Related Decisions

- **ADR 007:** Separate console apps (each loads its own event DTOs; ensures isolation)

## Implementation Notes

**JSON Schema File Example:**
```json
// contracts/contact-updated-v1.schema.json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Contact Updated Event",
  "type": "object",
  "properties": {
    "id": { "type": "string", "description": "Event ID (UUID)" },
    "type": { "const": "contact.updated", "description": "Event type" },
    "timestamp": { "type": "string", "format": "date-time" },
    "data": {
      "type": "object",
      "properties": {
        "contactId": { "type": "string", "minLength": 1 },
        "firstName": { "type": "string" },
        "lastName": { "type": "string" },
        "email": { "type": "string", "format": "email" },
        "phone": { "type": "string", "pattern": "^[0-9\\-\\+\\(\\)\\s]+$" },
        "attributes": {
          "type": "object",
          "properties": {
            "hasInsurance": { "type": "boolean" },
            "hasParksResorts": { "type": "boolean" },
            "hasCarwashProduct": { "type": "boolean" }
          }
        }
      },
      "required": ["contactId", "firstName", "lastName"]
    }
  },
  "required": ["id", "type", "timestamp", "data"]
}
```

**C# DTO:**
```csharp
using System.ComponentModel.DataAnnotations;

namespace ServiceBusPoc.Contracts;

public class ContactUpdatedEvent
{
    [Required]
    public string Id { get; set; } = null!;

    [Required]
    public string Type { get; set; } = null!;

    [Required]
    public DateTime Timestamp { get; set; }

    [Required]
    public ContactData Data { get; set; } = null!;
}

public class ContactData
{
    [Required]
    [StringLength(255, MinimumLength = 1)]
    public string ContactId { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = null!;

    [EmailAddress]
    public string? Email { get; set; }

    [Phone]
    public string? Phone { get; set; }

    public ContactAttributes? Attributes { get; set; }
}

public class ContactAttributes
{
    public bool HasInsurance { get; set; }
    public bool HasParksResorts { get; set; }
    public bool HasCarwashProduct { get; set; }
}
```

**Validation in consumer:**
```csharp
private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
{
    try
    {
        var json = args.Message.Body.ToString();
        var @event = JsonSerializer.Deserialize<ContactUpdatedEvent>(json);
        
        // Validate manually
        var context = new ValidationContext(@event);
        Validator.ValidateObject(@event, context, validateAllProperties: true);
        
        // Use validated event
        await HandleEventAsync(@event);
        await args.CompleteMessageAsync(args.CancellationToken);
    }
    catch (ValidationException ex)
    {
        _logger.LogError(ex, "Event validation failed: {Message}", ex.Message);
        // Dead-letter message
        await args.DeadLetterMessageAsync(args.Message, 
            deadLetterReason: "SchemaValidationFailed",
            deadLetterErrorDescription: ex.Message);
    }
}
```

**Unit Test Example:**
```csharp
[Fact]
public void ContactUpdatedEvent_ValidSerialization()
{
    var @event = new ContactUpdatedEvent
    {
        Id = "evt-001",
        Type = "contact.updated",
        Timestamp = DateTime.UtcNow,
        Data = new ContactData
        {
            ContactId = "c001",
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Attributes = new()
            {
                HasInsurance = true,
                HasCarwashProduct = true
            }
        }
    };

    // Serialize
    var json = JsonSerializer.Serialize(@event);
    
    // Deserialize
    var deserialized = JsonSerializer.Deserialize<ContactUpdatedEvent>(json);
    
    // Validate
    var context = new ValidationContext(deserialized);
    Validator.ValidateObject(deserialized, context, validateAllProperties: true);
    
    Assert.NotNull(deserialized);
    Assert.Equal("c001", deserialized.Data.ContactId);
}
```

## Post-MVP Notes

- Consider JSON Schema validation library (e.g., `NJsonSchema`) for strict schema compliance
- Implement schema-to-code generation tool (e.g., NSwag, AutoRest) if schema churn is high
- Add schema versioning support in envelope for forward/backward compatibility

## Sign-Off

- ✅ Product Owner (2026-09-15)
