---
title: Inventory and Stock Management
slug: inventory
related_concepts: [order_items, open_orders, locations, binracks]
related_workflows: [inventory_adjustment]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Stock.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Locations.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/stock.json
  - type: migration_finding
    ref: migration/STATUS.md
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The Linnworks product catalog and stock level management subsystem.

Linnworks divides stock management across two primary API families:
1. **`Inventory` Controller:** Manages the product catalog, product profiles, extended properties, suppliers, and item-location configuration.
2. **`Stock` Controller:** Manages operational stock tracking, catalog search, paged inventory queries, and stock quantity adjustments (both absolute counts and relative delta changes).

---

## Core Identifiers

| Identifier | Type | Description |
|---|---|---|
| `StockItemId` | `Guid` (string) | Primary system GUID for a stock item. Required by catalog configuration and certain detail endpoints. |
| `ItemNumber` / `SKU` | `string` | The product SKU — the primary user-facing catalog identifier. Accepted directly by many operational `Stock` endpoints. |
| `StockLocationId` | `Guid` (string) | ID of a warehouse location. Stock levels are maintained per `StockLocationId`. |

**Terminology Note:** `ItemNumber` in catalog APIs corresponds directly to `SKU` in order and operational stock endpoints.

---

## Important Models

| Model | Description |
|---|---|
| `StockItem` | Core catalog header: `StockItemId` (Guid), `ItemNumber` (SKU), `ItemTitle`, `CategoryId`, `Quantity`. |
| `StockItemFull` | Complete product profile: descriptions, weight, dimensions, images, suppliers, extended properties. |
| `StockItemLevel` | Location-specific inventory breakdown: `StockLevel` (Physical on-hand), `Available`, `InOrderBook` (Allocated to open orders), `Due` (On order from POs). |
| `StockLocation` | Warehouse location record: `StockLocationId`, `LocationName`, `IsFulfillmentCenter`. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Resolve SKU strings to StockItemId GUIDs** | `Inventory.GetStockItemIdsBySKU` | Batch lookup mapping SKUs (`ItemNumber[]`) to `StockItemId` GUIDs when an endpoint requires a GUID. | 150/min |
| **Retrieve one inventory item by SKU or GUID** | `Inventory.GetInventoryItem` | Accepts either `stockItemId` or `SKU` (prioritizes GUID if both provided). | 150/min |
| **Retrieve full product details by known GUID** | `Inventory.GetInventoryItemById` | Returns full product object (dimensions, weights, prices) for `id` (Guid). | 150/min |
| **Paged inventory search (SKU, Title, Barcode)** | `Stock.GetStockItemsFull` | Paged search (max 200/page) supporting keyword searches, variation parents, and optional `StockLevels`. | 150/min |
| **Retrieve all configured stock locations** | `Inventory.GetStockLocations` | Returns active `StockLocation` definitions across the account. | 150/min |
| **Set absolute stock level for SKU(s)** | `Stock.SetStockLevel` | Sets the physical stock level for a list of items identified directly by `SKU`. | 250/min |
| **Apply relative delta stock changes (+N / -N)** | `Stock.UpdateStockLevelsBySKU` | Modifies stock levels relatively (+5, -2) for items identified directly by `SKU`. | 150/min |
| **Flexible bulk stock change (SKU/ID + Name/ID)** | `Stock.UpdateStockLevelsBulk` | Allows changing stock levels using either SKU or StockItemId and location name or ID. | 150/min |
| **Update a location-level field for an item** | `Inventory.UpdateInventoryItemLevels` | Updates a specific location-level inventory field for a known `inventoryItemId`. | 250/min |
| **Batch / WMS-specific stock delta** | `Stock.BatchStockLevelDelta` | For batch/WMS scenarios involving `BatchNumber`, `pkBatchInventoryId`, and `BinRack`. | 150/min |

---

## Stock-Level Semantics

Stock quantities in Linnworks are fundamentally **location-aware**. Common stock-level models expose:
- **`StockLevel` (Physical / On Hand):** Total physical units present at the specific warehouse location.
- **`Available` (Free to Sell):** Unallocated stock available for new orders at that location.
- **`InOrderBook` (Allocated):** Stock allocated to unpaid or open processing orders.
- **`Due` / `OnOrder`:** Stock pending delivery from open Purchase Orders.

> [!WARNING]
> Do not attempt to manually recompute `Available` with client-side formulas unless the exact account model semantics are known. Prefer the `Available` value returned directly by Linnworks. Additionally, distinguish internal available stock from marketplace quantities sent to sales channels (which may be further modified by Max Listed Quantity caps, percentage rules, and End When thresholds).

---

## Gotchas & Operational Rules

### SKU-to-StockItemId resolution is endpoint-dependent

Do not assume every stock API requires a `StockItemId` GUID.
- Several operational endpoints accept `SKU` directly, including **`Stock.SetStockLevel`**, **`Stock.UpdateStockLevelsBySKU`**, and **`Inventory.GetInventoryItem`**.
- Other catalog configuration endpoints require a `StockItemId` GUID (e.g. `Inventory.GetInventoryItemById`, `Inventory.UpdateInventoryItemLevels`).
- Only perform a SKU → GUID resolution via `Inventory.GetStockItemIdsBySKU` when the target endpoint explicitly requires GUID parameters.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Stock.cs` | `vendor/LinnworksNetSDK/Controllers/Inventory.cs`

### Rate limits vary by stock endpoint

Do not apply a single generic rate limit to all stock write operations:
- `Stock.SetStockLevel` — **250 requests/minute**
- `Stock.UpdateStockLevelsBySKU` — **150 requests/minute**
- `Stock.UpdateStockLevelsBulk` — **150 requests/minute**
- `Inventory.UpdateInventoryItemLevels` — **250 requests/minute**

Implement proper backoff handling (HTTP 429) where high-frequency catalog mutations occur.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/stock.json` | `vendor/PublicApiSpecs/1.0/inventory.json`

### Do not treat `Guid.Empty` as "all locations"

Migration testing on the target account demonstrated that passing `Guid.Empty` (`00000000-0000-0000-0000-000000000000`) resolved to the location named "Default" rather than aggregating across all stock locations.
- For multi-location workflows, explicitly retrieve all active locations via `Inventory.GetStockLocations()` and query/update real location IDs.

**Source:** `migration_finding` — `migration/STATUS.md`

### Resolve locations once at macro startup

Warehouse locations can be configured or renamed by account administrators. Resolve the active location list once near macro initialization (`Inventory.GetStockLocations()`) and reuse the result in memory throughout the execution run. Avoid calling `GetStockLocations` repeatedly inside item loops.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`order_items`](order_items.md) — Order items reference inventory catalog items via `ItemId`
- [`locations`](locations.md) — Warehouse fulfillment locations hosting inventory
- [`binracks`](binracks.md) — Shelf and aisle storage coordinates for inventory

---

## Related Workflows

- [`inventory_adjustment`](../workflows/inventory_adjustment.md) — Resolve location, find item by SKU, and adjust stock levels
