---
title: Extended Properties
slug: extended_properties
related_concepts: [open_orders, inventory, processed_orders]
related_workflows: [set_extended_property]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ProcessedOrders.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/processedorders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

Key-value metadata attached to Linnworks entities. Extended properties allow macros and
integrations to attach arbitrary named attributes (e.g. ERP synchronization status, fraud risk scores,
custom channel references, payment authorization codes, or warehouse routing decisions).

> [!NOTE]
> This concept page focuses on two commonly used extended-property families:
> 1. **Order Extended Properties** (`Orders` / `ProcessedOrders` controllers): Order-level metadata on open and processed orders.
> 2. **Inventory Item Extended Properties** (`Inventory` controller): Catalog product-level attributes (specifications, dimensions, custom item properties).
> 
> Linnworks also exposes extended-property concepts in other subsystems (e.g. Purchase Orders); those operate through their respective controllers.

---

## Core Identifiers and Property Fields

| Field | Type | Meaning & Constraints |
|---|---|---|
| `RowId` | `Guid` (string) | Unique ID of an individual property record. Present in `ExtendedProperty` when retrieved or updated. |
| `Name` | `string` | Logical property key name (e.g. `ERP_SyncStatus`). Treat as a logical key with consistent naming. |
| `Value` | `string` | Property value. Always represented as a string; parse/format in macro code if representing booleans or numbers. |
| `Type` | `string` | Property type/category label. Valid values are obtained from `Orders.GetExtendedPropertyTypes` or defined by workflow rules. |

---

## Important Models

| Model | Description |
|---|---|
| `ExtendedProperty` | Order property model returned by `GetExtendedProperties` and used by `SetExtendedProperties`: `RowId` (Guid), `Name` (string), `Value` (string), `Type` (string). |
| `BasicExtendedProperty` | Lightweight property model used by `AddExtendedProperties`: `Name` (string), `Value` (string), `Type` (string) — no `RowId`. |
| `AddExtendedPropertiesRequest` | Request payload for `Orders.AddExtendedProperties`: `OrderId` (Guid) and `ExtendedProperties` (`BasicExtendedProperty[]`). |
| `Orders_SetExtendedPropertiesRequest` | Request payload for `Orders.SetExtendedProperties`: `orderId` (Guid) and `ExtendedProperties` (`ExtendedProperty[]`). |
| `StockItemExtendedProperty` | Inventory item extended property model returned by `Inventory.GetInventoryItemExtendedProperties`. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Read open-order properties before mutation** | `Orders.GetExtendedProperties` | Returns existing `ExtendedProperty[]` for an order. Essential first step before updating. |
| **Append a new property to an open order** | `Orders.AddExtendedProperties` | Append-oriented; takes `BasicExtendedProperty[]` without `RowId`. Does NOT update existing keys. |
| **Replace/update open-order property collection** | `Orders.SetExtendedProperties` | Replacement-oriented; overwrites collection. Perform read-merge-write to preserve existing properties. |
| **Discover available order property names** | `Orders.GetExtendedPropertyNames` | Returns available order extended-property names configured in the account. |
| **Discover available order property types** | `Orders.GetExtendedPropertyTypes` | Returns available property type category strings (e.g. `Shipping`, `Custom`). |
| **Read processed-order extended properties** | `ProcessedOrders.GetProcessedOrderExtendedProperties` | Dedicated retrieval endpoint for historical/processed orders. |
| **Read catalog product extended properties** | `Inventory.GetInventoryItemExtendedProperties` | Retrieves product properties by `inventoryItemId` or `itemNumber`. |
| **Create catalog product extended properties** | `Inventory.CreateInventoryItemExtendedProperties` | Creates product-level attributes in inventory. |
| **Update catalog product extended properties** | `Inventory.UpdateInventoryItemExtendedProperties` | Updates existing product-level attributes in inventory. |
| **Delete catalog product extended properties** | `Inventory.DeleteInventoryItemExtendedProperties` | Removes product-level attributes from inventory. |

---

## Common Operations

- `Orders.GetExtendedProperties` — Retrieve all extended properties for an order by `orderId` (`pkOrderId`).
- `Orders.AddExtendedProperties` — Append new property entries to an order.
- `Orders.SetExtendedProperties` — Replace/update the order extended-property collection (requires read-merge-write).
- `ProcessedOrders.GetProcessedOrderExtendedProperties` — Read extended properties on processed orders.
- `Inventory.GetInventoryItemExtendedProperties` — Retrieve catalog product attributes.
- `Inventory.UpdateInventoryItemExtendedProperties` — Update catalog product attributes.

---

## Canonical Idempotency Pattern (Read-Merge-Write)

Because `AddExtendedProperties` does not update existing properties and `SetExtendedProperties` replaces
the entire collection, macros must follow the read-merge-write pattern:

```csharp
const string TargetKey = "ERP_SyncStatus";
const string TargetValue = "SYNCED";

// 1. Read existing properties
var existing = Api.Orders.GetExtendedProperties(orderId);

// 2. Find target property by canonical name (defensively case-insensitive)
var target = existing.FirstOrDefault(p => string.Equals(p.Name, TargetKey, StringComparison.OrdinalIgnoreCase));

if (target == null)
{
    // 3a. Property absent -> Append via AddExtendedProperties
    Api.Orders.AddExtendedProperties(new AddExtendedPropertiesRequest
    {
        OrderId = orderId,
        ExtendedProperties = new List<BasicExtendedProperty>
        {
            new BasicExtendedProperty { Name = TargetKey, Value = TargetValue, Type = "Custom" }
        }
    });
}
else if (!string.Equals(target.Value, TargetValue, StringComparison.Ordinal))
{
    // 3b. Property exists with different value -> Update in memory & write merged collection back
    target.Value = TargetValue;
    Api.Orders.SetExtendedProperties(new Orders_SetExtendedPropertiesRequest
    {
        OrderId = orderId,
        ExtendedProperties = existing // writes full merged list preserving RowIds and other keys
    });
}
// 3c. Already set -> Skip write (Idempotent No-op)
```

---

## Gotchas & Operational Rules

### `SetExtendedProperties` is replacement-oriented

`Orders.SetExtendedProperties` overwrites the order's entire extended-property collection with the supplied list. Submitting only a single property will erase all other existing extended properties on that order. Always retrieve current properties via `GetExtendedProperties`, update or add the target property in memory, and submit the complete merged collection.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Read-merge-write is not atomic (concurrency & race conditions)

`GetExtendedProperties` followed by `SetExtendedProperties` is a client-side read-modify-write sequence, not an atomic database upsert. If multiple macros or external integrations write extended properties on the same order concurrently, one writer can overwrite changes made after its read.
- Minimize execution time between read and write.
- Avoid deploying multiple competing macros that mutate the same order's extended properties simultaneously.
- Namespace macro-owned properties to avoid collisions.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### `AddExtendedProperties` is append-oriented, not an upsert

Linnworks explicitly documents that `AddExtendedProperties` will not update properties that match on property name and value. For idempotent macros where property names are expected to be unique, verify existence via `GetExtendedProperties` before invoking `AddExtendedProperties`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Preserve `RowId` when updating existing properties

When updating existing properties with `Orders.SetExtendedProperties`, preserve the `RowId` values returned by Linnworks. Never invent or synthesize artificial `RowId` GUIDs.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Normalize property names and namespace macro-owned keys

Treat property names as logical keys and use consistent canonical spelling and casing across all macros (e.g. `ERP_SyncStatus`). Do not intentionally create case variants (such as `sync_status` vs `Sync_Status`). Prefix macro-owned property names with an integration or functional namespace (e.g. `FRAUD_Score`, `3PL_Status`) to prevent collisions with user-entered UI fields or other channel integrations.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Scoping: Open vs Processed orders

The `Orders.SetExtendedProperties` and `Orders.AddExtendedProperties` endpoints are designed for open-order workflows. For historical/processed orders, use `ProcessedOrders.GetProcessedOrderExtendedProperties` for retrieval and verify post-processing mutation support before attempting writes.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/processedorders.json`

### Idempotency is mandatory for rule-triggered macros

Rule-triggered macros may execute repeatedly on the same order while matching conditions remain satisfied. Checking the current property value before writing prevents duplicate records and redundant API calls.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Order-level extended properties are attached to open orders
- [`processed_orders`](processed_orders.md) — Processed order extended properties are retrieved via `ProcessedOrders` APIs
- [`inventory`](inventory.md) — Catalog product-level extended properties are managed on stock items

---

## Related Workflows

- [`set_extended_property`](../workflows/set_extended_property.md) — Step-by-step check-then-set implementation workflow
