---
title: Set Extended Property on an Order
slug: set_extended_property
intent: "Use when a macro must write a named key-value property to an order, ensuring idempotency (no duplicates, correct update vs create)."
related_concepts: [extended_properties, open_orders]
related_workflows: [modify_open_orders_by_sku]
ambiguities: []
sources:
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/ClassBase/ExtendedProperty.cs
---

## Intent

Use when a macro needs to attach or update a named metadata value on a Linnworks order — for
example, a processing flag, a routing decision, a third-party reference, or an audit marker.

The core challenge is idempotency: `AddExtendedProperties` creates a new record and will
NOT update an existing one with the same name; `SetExtendedProperties` updates but requires
an existing `RowId`. This workflow enforces the check-then-set pattern required to prevent
duplicate records.

## Preconditions

- The `pkOrderId` of the target order is known
- The property `Name` (key) to set is known
- The desired `Value` is known
- (Optional) The property `Type` is known

## Inputs

| Input | Type | Notes |
|---|---|---|
| Order ID | `string` (Guid) | The `pkOrderId` of the order |
| Property Name | `string` | Case-sensitive. Must be consistent across macro runs. |
| Property Value | `string` | All values are stored as strings |
| Property Type | `string` | Optional category label. Omit if not needed. |

## Workflow steps

1. **Retrieve existing properties**
   `Orders.GetExtendedProperties` — pass `orderId` (the `pkOrderId`) as a query parameter.
   Returns a list of `ExtendedProperty` objects currently on this order.

2. **Check existence by Name**
   Search the returned list for a property whose `Name` matches the target property name.
   Use exact (case-sensitive) string comparison.

3. **Decision: does the property exist?**
   See Decision Points below.

4. **Decision: should we skip if value already matches?**
   See Decision Points below.

5. **Write the property**
   Either call `AddExtendedProperties` (new) or `SetExtendedProperties` (existing), based on
   the decision in step 3.

6. **Log outcome**
   Log the `NumOrderId` (not the GUID), the property name, the operation performed
   (created/updated/skipped), and the final value.

## Decision points

### Does the property already exist on this order?

- **YES** (found a property with the matching Name) →
  - Read its current `Value` and `RowId`
  - Go to "Does the value already match?"
- **NO** (no property with this Name found) →
  - Call `Orders.AddExtendedProperties` with `Name`, `Value`, `Type` (no RowId)
  - Done

### Does the value already match?

- **YES** (current value == desired value) →
  - Skip the write — no-op, log "already set"
  - Done
- **NO** (current value differs) →
  - Call `Orders.SetExtendedProperties` with the existing `RowId`, `Name`, new `Value`, `Type`
  - Done

## Relevant operations

- `Orders.GetExtendedProperties` — Retrieve all existing extended properties for this order
- `Orders.GetExtendedPropertyNames` — Optional: discover existing property name conventions in the account
- `Orders.AddExtendedProperties` — Create a new property when it does not exist
- `Orders.SetExtendedProperties` — Update an existing property (requires RowId)

## Gotchas

### AddExtendedProperties will NOT update an existing property

The API spec states: "This will NOT update properties that match on property name / value."
Calling Add when the Name already exists creates a duplicate. Always check first.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### SetExtendedProperties requires a valid RowId from GetExtendedProperties

The `RowId` must come from a prior `GetExtendedProperties` call. You cannot construct or
derive it from the Name. There is no upsert endpoint — the check-then-set pattern in this
workflow is the canonical approach.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/ClassBase/ExtendedProperty.cs`

### Property Names are case-sensitive

Linnworks stores and matches property names exactly as provided. `ProcessingFlag` and
`processingflag` are treated as different properties. Be consistent in naming conventions
and use the same case in every macro run.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Rule macros may fire multiple times on the same order

A rule macro runs whenever its condition is true. If the triggering condition persists across
multiple Linnworks polling cycles, the macro will be called again for the same order. The
value-check in step 4 prevents redundant writes, but the full check (steps 1–4) must run
on every invocation — do not assume the property is absent just because the macro runs once.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

## Do not use when

- You need to set a property on a **processed** order — use `ProcessedOrders` controller equivalents
- You need to set a **stock item** extended property — that is `Inventory.SetStockItemExtendedProperties`, which is a different endpoint
- You need to remove a property — there is no delete endpoint for order extended properties; design the value to represent absence (e.g. empty string or a sentinel value)

## Related concepts

- `extended_properties` — What extended properties are and the full model description
- `open_orders` — The order context this workflow operates on

## Related workflows

- `modify_open_orders_by_sku` — When you need to find orders by SKU first, then set a property on them
