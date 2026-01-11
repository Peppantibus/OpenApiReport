# OpenAPI Change Report

## Summary
- Breaking: 1
- Risky: 0
- Additive: 1
- Cosmetic: 0
- Total: 2

## Top changes
- **Operation removed** (Breaking) — `GET /orders` (Risk 10)
- **Operation added** (Additive) — `POST /orders` (Risk 2)

## Breaking
### Tag: orders
#### GET /orders
- **Operation removed**
  - Pointer: `paths./orders.get`
  - Before: `present` → After: `missing`
  - Meaning: Removed op
  - SuggestedAction: Re-add
  - RiskScore: 10

## Additive
### Tag: orders
#### POST /orders
- **Operation added**
  - Pointer: `paths./orders.post`
  - Before: `missing` → After: `present`
  - Meaning: Added op
  - SuggestedAction: Update
  - RiskScore: 2

