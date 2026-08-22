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
| `RowId` | `Guid` (string) | Unique record ID of an individual order extended-property record. |
| `Name` | `string` | Logical property key name (e.g. `ERP_SyncStatus`). |
| `Value` | `string` | Property value string. |
| `Type` | `string` | Property type category string (e.g. `Attribute`, `Shipping`, `Info`). Obtain valid values from `Orders.GetExtendedPropertyTypes`. |
| `CreatedDate` / `LastUpdatedDate` | `DateTime` | System timestamps populated on `ExtendedProperty` records. |

---

## Important Models

| Model | Description |
|---|---|
| `ExtendedProperty` | Order property model returned by `GetExtendedProperties` and accepted by `SetExtendedProperties`: `RowId` (`Guid`), `CreatedDate` (`DateTime`), `LastUpdatedDate` (`DateTime`), `Name` (`string`), `Value` (`string`), `Type` (`string`). |
| `BasicExtendedProperty` | Lightweight property model used by `AddExtendedProperties`: `Name` (`string`), `Value` (`string`), `Type` (`string`) — no `RowId`. |
| `AddExtendedPropertiesRequest` | Request payload for `Orders.AddExtendedProperties`: `OrderId` (`Guid`) and `ExtendedProperties` (`BasicExtendedProperty[]`). |
| `StockItemExtendedProperty` | Product property model returned by `Inventory.GetInventoryItemExtendedProperties`: `pkRowId` (`Guid`), `fkStockItemId` (`Guid`), `ProperyName` (`string` — exact SDK spelling), `PropertyValue` (`string`), `PropertyType` (`string`). |
| `StockItemExtendedPropertyUpsertItem` | Input model for `Inventory.CreateInventoryItemExtendedProperties`: `fkStockItemId` (`Guid?`), `SKU` (`string`), `ProperyName` (`string`), `PropertyValue` (`string`), `PropertyType` (`string`). |
| `StockItemExtendedPropertyWithSku` | Input model for `Inventory.UpdateInventoryItemExtendedProperties`: `ItemNumber` (`string`), `pkRowId` (`Guid`), `fkStockItemId` (`Guid`), `ProperyName` (`string`), `PropertyValue` (`string`), `PropertyType` (`string`). |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Read open-order properties before mutation** | `Orders.GetExtendedProperties` | Returns existing `List<ExtendedProperty>` for an order. Essential first step before updating. |
| **Append a new property to an open order** | `Orders.AddExtendedProperties` | Takes `AddExtendedPropertiesRequest` with `BasicExtendedProperty[]`. Does not update properties matching on name/value. |
| **Replace/update open-order property collection** | `Orders.SetExtendedProperties` | Overwrites collection. Takes `(Guid orderId, ExtendedProperty[] extendedProperties)`. Perform read-merge-write to preserve existing properties. |
| **Discover available order property names** | `Orders.GetExtendedPropertyNames` | Returns available order extended-property names configured in the account. |
| **Discover available order property types** | `Orders.GetExtendedPropertyTypes` | Returns available order property type category strings (e.g. `Attribute`, `Shipping`, `Info`). |
| **Read processed-order extended properties** | `ProcessedOrders.GetProcessedOrderExtendedProperties` | Dedicated retrieval endpoint for historical/processed orders (`pkOrderId` Guid). |
| **Read catalog product extended properties** | `Inventory.GetInventoryItemExtendedProperties` | Retrieves product properties by `inventoryItemId` (Guid) or `itemNumber` (SKU). |
| **Create catalog product extended properties** | `Inventory.CreateInventoryItemExtendedProperties` | Takes `List<StockItemExtendedPropertyUpsertItem>` to create product attributes. |
| **Update catalog product extended properties** | `Inventory.UpdateInventoryItemExtendedProperties` | Takes `List<StockItemExtendedPropertyWithSku>` to update product attributes. |
| **Delete catalog product extended properties** | `Inventory.DeleteInventoryItemExtendedProperties` | Deletes product extended properties by `inventoryItemId` and property names. |

---

## Common Operations

- `Orders.GetExtendedProperties` — Retrieve all extended properties for an order by `orderId` (`pkOrderId`).
- `Orders.AddExtendedProperties` — Append new property entries to an order without constructing `RowId`s.
- `Orders.SetExtendedProperties` — Replace/update the order extended-property collection (requires read-merge-write).
- `ProcessedOrders.GetProcessedOrderExtendedProperties` — Read extended properties on processed orders.
- `Inventory.GetInventoryItemExtendedProperties` — Retrieve catalog product attributes by item ID or SKU.
- `Inventory.CreateInventoryItemExtendedProperties` / `UpdateInventoryItemExtendedProperties` — Manage catalog attributes.

---

## Canonical Idempotency Pattern (Read-Merge-Write)

Because `AddExtendedProperties` does not update existing properties matching on name/value and `SetExtendedProperties` replaces the entire collection, macros must follow the read-merge-write pattern:

```csharp
const string TargetKey = "ERP_SyncStatus";
const string TargetValue = "SYNCED";

// Select a property type valid for the target workflow/account (e.g. "Attribute", "Info")
const string PropertyType = "Attribute";

// 1. Read existing properties
var existing = Api.Orders.GetExtendedProperties(orderId);

// 2. Apply integration canonical name policy (handling potential duplicate names)
var matches = existing
    .Where(p => string.Equals(p.Name, TargetKey, StringComparison.OrdinalIgnoreCase))
    .ToList();

if (matches.Count == 0)
{
    // 3a. Property absent -> Append via AddExtendedProperties without constructing a RowId
    Api.Orders.AddExtendedProperties(new AddExtendedPropertiesRequest
    {
        OrderId = orderId,
        ExtendedProperties = new[]
        {
            new BasicExtendedProperty
            {
                Name = TargetKey,
                Value = TargetValue,
                Type = PropertyType
            }
        }
    });
}
else
{
    // 3b. Linnworks can contain duplicate property names; workflow treats first match as canonical
    var target = matches[0];

    if (!string.Equals(target.Value, TargetValue, StringComparison.Ordinal))
    {
        target.Value = TargetValue;

        // 3c. Submit merged collection back (SDK signature: Guid orderId, ExtendedProperty[] extendedProperties)
        Api.Orders.SetExtendedProperties(
            orderId,
            existing.ToArray()
        );
    }
    // 3d. Already set with matching value -> Skip write (Idempotent No-op)
}
```

---

## Gotchas & Operational Rules

### `SetExtendedProperties` is replacement-oriented

`Orders.SetExtendedProperties` overwrites the order's entire extended-property collection with the supplied array. Submitting only a single property will erase all other existing extended properties on that order. Always retrieve current properties via `GetExtendedProperties`, update or add the target property in memory, and submit the complete merged collection.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Read-merge-write is not atomic (concurrency & race conditions)

`GetExtendedProperties` followed by `SetExtendedProperties` is a client-side read-modify-write sequence, not an atomic database upsert. If multiple macros or external integrations write extended properties on the same order concurrently, one writer can overwrite changes made after its read.
- Minimize execution time between read and write.
- Avoid deploying multiple competing macros that mutate the same order's extended properties simultaneously.
- Namespace macro-owned properties to avoid collisions.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### `AddExtendedProperties` is append-oriented, not an upsert

Linnworks explicitly documents that `AddExtendedProperties` will **not** update properties that match on property name and value. Do not treat this endpoint as a name-based upsert, and do not assume property names are uniquely enforced by Linnworks.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Preserve `RowId` when updating existing properties

When modifying an existing property with `Orders.SetExtendedProperties`, retain the `RowId` Linnworks returned rather than replacing it with an arbitrary client-generated identifier. Allow Linnworks to assign and return IDs for newly created property records.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Normalize property names and namespace macro-owned keys

For macro-owned properties, enforce one logical property per canonical property name at the application level. Prefix macro-owned property names with an integration or functional namespace (e.g. `ERP_SyncStatus`, `FRAUD_Score`, `3PL_Status`) to prevent collisions with user-entered UI fields or channel integrations.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Scoping: Open vs Processed orders

The `Orders.SetExtendedProperties` and `Orders.AddExtendedProperties` endpoints are designed for open-order workflows. For historical/processed orders, use `ProcessedOrders.GetProcessedOrderExtendedProperties` for retrieval and verify post-processing mutation support before attempting writes.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/processedorders.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Order-level extended properties are attached to open orders
- [`processed_orders`](processed_orders.md) — Processed order extended properties are retrieved via `ProcessedOrders` APIs
- [`inventory`](inventory.md) — Catalog product-level extended properties are managed on stock items

---

## Related Workflows

- [`set_extended_property`](../workflows/set_extended_property.md) — Step-by-step check-then-set implementation workflow
