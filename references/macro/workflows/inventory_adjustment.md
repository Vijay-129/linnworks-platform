---
title: Inventory Adjustment
slug: inventory_adjustment
intent: "Use when a macro must adjust the stock level of one or more items at a specific location."
related_concepts: [inventory]
related_workflows: []
ambiguities:
  - type: ambiguous_adjustment_type
    blocking: false
    question: "Should the adjustment set an absolute level or apply a relative delta?"
    reason: "Stock adjustments can be absolute (set to N) or relative (+N / -N). The endpoint and request shape differ."
    possible_intents:
      - set_absolute_level
      - apply_relative_delta
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: migration_finding
    ref: migration/STATUS.md
---

## Intent

Use when a macro must set or adjust the stock level of one or more Linnworks stock items at
one or more warehouse locations — for example, recording a stock receipt, correcting a
discrepancy, or zeroing stock after a write-off.

The core challenge is location resolution: every stock adjustment is scoped to a specific
`StockLocationId`. Guid.Empty is not a valid substitute for "all locations".

## Preconditions

- The SKU(s) of the items to adjust are known
- The location name or ID is known (or will be resolved at runtime)
- The desired new level or delta is known

## Inputs

| Input | Type | Notes |
|---|---|---|
| SKU(s) | `string[]` | Linnworks catalog SKUs (ItemNumber) |
| Location | `string` or `Guid` | Location name (resolved to ID at runtime) or a known location ID |
| Adjustment | `integer` | New absolute level or relative delta (clarify before coding) |

## Workflow steps

1. **Resolve LocationId**
   `Inventory.GetStockLocations` — retrieve all warehouse locations. Match by name to get
   the target `StockLocationId`. Never use `Guid.Empty`.

2. **Resolve StockItemId from SKU**
   `Inventory.GetStockItemIdsBySKU` — submit the target SKUs and receive back a list of
   `{ ItemNumber, StockItemId }` mappings. Use the returned GUID for all subsequent calls.

3. **Decision: single item or batch?**
   See Decision Points below.

4. **Check current level (optional but recommended)**
   `Inventory.GetStockLevel` — verify the current level before adjustment. Log it for
   audit purposes. Skip if the adjustment is unconditional (e.g. always set to 0).

5. **Apply adjustment**
   `Stock.SetStockLevel` (for absolute level set) or the appropriate delta endpoint.
   Always pass the resolved `StockLocationId` and `StockItemId`.

6. **Log outcome**
   Log the SKU, location name, previous level, new level, and timestamp.

## Decision points

### Single item or batch?

- **Single item** → call `Stock.SetStockLevel` once with the resolved IDs
- **Multiple items** → use `Stock.SetStockLevelBulk` if available for the operation,
  or loop over items calling `SetStockLevel` per item. Check rate limits (typically
  150/min) and add delays if adjusting a large batch.

### Absolute level or relative delta?

- **Absolute** (set level to N) → `Stock.SetStockLevel` with the target quantity
- **Relative** (+N or -N delta) → check whether a delta endpoint exists; if not, read
  the current level first (step 4), compute the new target, then call `SetStockLevel`
  with the computed absolute value.

## Relevant operations

- `Inventory.GetStockLocations` — Resolve location name to StockLocationId — always required
- `Inventory.GetStockItemIdsBySKU` — Resolve SKU to StockItemId — always required before stock calls
- `Inventory.GetInventoryItemByID` — Optional: verify item exists and retrieve full item detail
- `Inventory.GetStockLevel` — Read current level at a location before adjusting (audit trail)
- `Stock.SetStockLevel` — Write new stock level for an item at a location

## Gotchas

### Guid.Empty is not "all locations" — it means Default only

Every stock call must use a real location GUID. On a multi-location account, `Guid.Empty`
targets the Default location only and silently excludes all others. Always resolve via
`GetStockLocations`.

**Source:** `migration_finding` — `migration/STATUS.md`

### SKU-to-StockItemId resolution is mandatory

Stock level endpoints require a `StockItemId` GUID — there is no endpoint that accepts a
SKU directly for the adjustment. Use `GetStockItemIdsBySKU` before every adjustment batch.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Inventory.cs`

### Location IDs must be resolved fresh per run

Locations can be added or renamed by administrators. Do not hardcode a `StockLocationId`.
Resolve by name via `GetStockLocations` on every macro execution.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Rate limit on stock write endpoints

Stock mutation endpoints are typically rate-limited at 150/min. When adjusting a large
number of items, pace the calls — check the rate limit header and add delays if needed.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/inventory.json`

## Do not use when

- You need to adjust stock in a **Purchase Order** context — use the `PurchaseOrder` controller
- You need to move stock between locations — that is a warehouse transfer, not a stock adjustment
- You need to update a stock item's catalog properties (description, images) — use `Inventory.UpdateInventoryItem`

## Related concepts

- `inventory` — Full coverage of stock items, identifiers, and location scoping

## Related workflows

- (none currently — `modify_open_orders_by_sku` and `set_extended_property` are in the order domain)
