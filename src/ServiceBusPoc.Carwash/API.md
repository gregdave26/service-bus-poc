# Carwash Verification API

## Overview

The Carwash service exposes a member verification HTTP API endpoint that validates whether a RAC member ID is associated with an active carwash product subscription. Pulse or mock Pulse calls this endpoint to verify membership eligibility before contact sync operations.

## Endpoint Specification

### POST `/carwash/v1/verify`

Verifies whether a RAC member ID has an active carwash product subscription.

#### Request

**URI:** `POST http://localhost:5000/carwash/v1/verify`

**Content-Type:** `application/json`

**Body:**
```json
{
  "RacId": "12345678"
}
```

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `RacId` | string | Yes | RAC member ID to verify |

#### Response

**Success (200 OK):**
```json
{
  "ValidMember": true
}
```

| Status | Condition | Response |
|--------|-----------|----------|
| **200** | Valid or invalid RAC ID | `{"ValidMember": true/false}` |

**Validation Error (400 Bad Request):**
```json
{
  "Errors": [
    "'Rac Id' must not be empty."
  ]
}
```

| Status | Condition | Response |
|--------|-----------|----------|
| **400** | RacId is empty or missing | `{"Errors": ["'Rac Id' must not be empty."]}` |

#### Example Usage

**Curl - Valid member:**
```bash
curl -X POST http://localhost:5000/carwash/v1/verify \
  -H "Content-Type: application/json" \
  -d '{"RacId": "VALID-123456"}'
```

**Response:**
```json
{
  "ValidMember": true
}
```

**Curl - Invalid member:**
```bash
curl -X POST http://localhost:5000/carwash/v1/verify \
  -H "Content-Type: application/json" \
  -d '{"RacId": "INVALID-999999"}'
```

**Response:**
```json
{
  "ValidMember": false
}
```

**Curl - Missing RacId:**
```bash
curl -X POST http://localhost:5000/carwash/v1/verify \
  -H "Content-Type: application/json" \
  -d '{}'
```

**Response (400):**
```json
{
  "Errors": [
    "'Rac Id' must not be empty."
  ]
}
```

## Implementation Details

- **Server:** Runs on `http://localhost:5000` alongside the Service Bus consumer
- **Mock Validation Logic:** RAC IDs starting with "VALID" (case-insensitive) are considered valid members; all others are invalid
- **Error Handling:**
  - Empty or missing `RacId`: Returns 400 Bad Request
  - Invalid JSON: Returns 400 Bad Request
  - Unhandled exceptions: Returns 500 Internal Server Error
- **Lifecycle:** API server is started as a background task in Carwash Program.cs and stopped when the application shuts down

## Integration Points

1. **Pulse or mock Pulse:** Calls this endpoint before syncing contacts with carwash product records.
2. **Service Bus consumer:** Independently processes contact events filtered by `hasCarwashProduct=true`.

The API and consumer are separate integration points: the consumer does not call this API, and this API does not consume Service Bus messages or call Pulse.

## Future Enhancements

- Replace mock validation with actual member database lookup
- Add authentication/authorization header validation
- Implement caching for repeated lookups
- Add metrics and monitoring (latency, error rates)
- Support batch verification for multiple RAC IDs
