---
title: BinRacks and Warehouse Storage
slug: binracks
related_concepts: [inventory, locations, pickwaves]
related_workflows: [inventory_adjustment]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Stock.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Picking.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Wms.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/stock.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/picking.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/wms.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

Binracks describe where inventory is stored, moved, or picked within a Linnworks stock location.

Linnworks exposes two distinct binrack paradigms that must not be confused:
1. **Classic Item-Location Bin/Rack:** Simple `BinRack` text strings configured per SKU per stock location (e.g. `"A-01"`).
2. **WMS Physical BinRacks:** Rich warehouse management entities identified by integer `BinRackId`, participating in binrack types, zone relationships, item/group placement restrictions, warehouse stock-flow configuration, physical batch inventory, picking, and warehouse moves.

---

## Two Distinct Binrack Models

```
CLASSIC INVENTORY LOCATION
──────────────────────────
StockItemId (Guid)
        +
StockLocationId (Guid)
        │
        └── BinRack (string)
            e.g. "A-01"

Used for:
- item-location configuration
- simple bin/rack display
- Inventory.Get/Add/Update/DeleteItemLocations
- Stock.GetStockItemsLocation


WMS PHYSICAL INVENTORY
──────────────────────
Warehouse / WMS location
        │
        ├── BinRackId (int32)
        │     ├── binrack type (Stock.GetBinrackTypes)
        │     ├── zone relationships (Wms.GetWarehouseZonesByLocation)
        │     └── placement/flow restrictions (Stock.SearchBinracks)
        │
        └── BatchInventoryId (int32)
              │
              ├── physical quantity
              ├── current physical bin
              └── warehouse move / picking operations (Stock.CreateWarehouseMove)
```

> [!IMPORTANT]
> Do not treat classic inventory `BinRack` strings and WMS `BinRackId` entities as interchangeable. Modifying a text `BinRack` string on an item does not create or move physical WMS stock.

---

## Core Identifiers

| Identifier | Type | Meaning |
|---|---|---|
| `BinRack` | `string` | Human-readable bin/rack name or code (e.g. `A-03-02`). |
| `BinRackId` | `int32` | Internal unique WMS binrack identifier. |
| `StockLocationId` | `Guid` (string) | Standard Linnworks stock-location UUID. |
| `StockLocationIntId` | `int32` | Integer stock-location identifier used by WMS and Zone APIs. |
| `StockItemId` | `Guid` (string) | Linnworks inventory product identifier UUID. |
| `ZoneId` | `int32` | WMS warehouse zone identifier. |
| `ZoneName` / `Name` | `string` | Warehouse zone human-readable name. |
| `BinRackTypeId` | `int32` | Identifier of a WMS binrack type; retrieve configured types via `Stock.GetBinrackTypes` rather than hard-coding IDs. |
| `BatchInventoryId` | `int32` | Identifier of the physical inventory/batch record used by WMS-aware stock operations. Note: physical batch-inventory records exist in WMS locations even for products not configured as customer-visible "batched" items. |

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Read simple SKU → stock-location/binrack string** | `Stock.GetStockItemsLocation` | Returns location name and `BinRack` string for a batch of `StockItemId` + `StockLocationId` pairs. |
| **Read inventory item's configured location records** | `Inventory.GetInventoryItemLocations` | Retrieves item-location configuration records for a stock item. |
| **Add configured item-location bin/rack** | `Inventory.AddItemLocations` | Adds a new item-location bin/rack mapping. |
| **Update configured item-location bin/rack string** | `Inventory.UpdateItemLocations` | Updates the configured bin/rack text string for a stock item at a location. |
| **Delete configured item-location bin/rack** | `Inventory.DeleteItemLocations` | Deletes item-location bin/rack mapping records for an inventory item. |
| **Update targeted item-location field** | `Inventory.UpdateInventoryItemLocationField` | Targeted modification of a single location-level field on a stock item. |
| **Discover configured WMS binrack types** | `Stock.GetBinrackTypes` | Retrieves available binrack type IDs for a location before calling `SearchBinracks`. |
| **Find suitable WMS destination bins for an item** | `Stock.SearchBinracks` | Evaluates item/group restrictions and warehouse flow logic, returning candidate bins in preferred placement order. |
| **Read SKUs physically stored in a WMS bin** | `Stock.GetBinrackSkus` | Returns SKUs and batch details currently located in a specific `BinRackId`. |
| **Read WMS bin details by IDs** | `Stock.GetBinRacksById` | Retrieves full WMS binrack metadata for a list of `BinRackId` integers. |
| **Initialize physical WMS stock move** | `Stock.CreateWarehouseMove` | Creates a move in `InTransit` (marks stock unavailable) or `Open` state for a `BatchInventoryId`. |
| **Update / complete physical WMS stock move** | `Stock.UpdateWarehouseMove` / `CompleteWarehouseMove` | Updates or completes in-progress warehouse moves. |
| **Audit moves for a binrack** | `Stock.GetWarehouseMove` / `GetWarehouseMovesByBinrack` | Queries active or historical incoming and outgoing stock moves for a bin. |
| **Read warehouse zone hierarchy** | `Wms.GetWarehouseZonesByLocation` | Retrieves warehouse zone structures using `StockLocationIntId` (`int32`). |
| **Find binracks belonging to specified zones** | `Wms.GetBinrackZonesByZoneIdOrName` | Queries binracks mapped to specific `ZoneIds` (int32) or `ZoneNames` within a `StockLocationIntId`. |
| **Map binrack to a warehouse zone** | `Wms.UpdateWarehouseBinrackBinrackToZone` | Links or updates zone assignment for a binrack (passing `BinRackId = 0` removes the bin from the zone). |
| **Find alternative physical locations while picking** | `Picking.GetItemBinracks` | Scoped to a `StockLocationId`, finds alternative bin locations relative to `currentBinRackSuggestion` (`includeNonPickLocations` can return storage bins). |
| **Change allocated pickwave item bin/batch** | `Picking.UpdatePickingWaveItemWithNewBinrack` | Reallocates a pickwave line to an alternative physical bin/batch (only applicable where batch information exists). |
| **Update picked batch/bin delta** | `Picking.UpdatePickedItemDelta` | Updates batch/binrack delta for allocated pickwave items (only applicable where batch information exists). |

---

## Multi-Bin Storage and Picking

In WMS-enabled warehouses, a stock item can have physical inventory distributed across multiple distinct `BinRackId` locations:
- **Picking Suggestion:** `Picking.GetItemBinracks` takes `stockItemId`, `stockLocationId`, and `currentBinRackSuggestion` to discover secondary physical pick locations if the primary location is depleted or blocked. Supplying `includeNonPickLocations = true` allows discovering storage binracks not normally routed for picking.
- **WMS Candidate Search:** `Stock.SearchBinracks` evaluates warehouse stock flow configuration, bin types (retrieved via `Stock.GetBinrackTypes`), and group restrictions to suggest optimal putaway or replenishment bins.
- **Wave Reallocation:** If a picker cannot pick from the suggested bin, `Picking.UpdatePickingWaveItemWithNewBinrack` or `Picking.UpdatePickedItemDelta` reallocates the line to another batch/bin (for items with batch information).

> [!NOTE]
> Do not assume that the classic catalog item's `BinRack` text string is automatically the WMS bin that will be selected during pickwave generation. WMS picking logic dynamically evaluates physical batch inventory availability, bin priorities, and warehouse stock-flow configuration.

---

## Gotchas & Operational Rules

### Binrack identity depends on API family

- **Classic Inventory APIs:** Represent the bin/rack as a text string associated with a `StockLocationId` (Guid).
- **WMS APIs:** Represent physical binracks as entity records with integer `BinRackId`, zone assignments, and binrack types.
- Do not use text binrack names as global unique identifiers; they must always be evaluated in their specific warehouse location context.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/stock.json` | `vendor/PublicApiSpecs/1.0/inventory.json`

### Updating an item-location BinRack is not a WMS stock move

`Inventory.UpdateItemLocations` or `Inventory.UpdateInventoryItemLocationField` updates the item's configured catalog location/bin-rack text label. It does **not** physically transfer WMS inventory quantities between physical binracks.
- Physical WMS stock movement requires the warehouse-move workflow (**`Stock.CreateWarehouseMove`**, **`Stock.UpdateWarehouseMove`**, and **`Stock.CompleteWarehouseMove`**) operating against `BatchInventoryId` and `BinrackIdDestination`.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Stock.cs` | `vendor/LinnworksNetSDK/Controllers/Inventory.cs`

### WMS Zone endpoints use `StockLocationIntId` (int32), not `StockLocationId` (Guid)

WMS zone endpoints such as `Wms.GetWarehouseZonesByLocation` and `Wms.GetBinrackZonesByZoneIdOrName` require the integer location identifier **`StockLocationIntId`** (`int32`), rather than the standard stock-location UUID.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/wms.json`

### `Picking.GetItemBinracks` is location-scoped and relative

`Picking.GetItemBinracks` does not perform a global catalog search across all warehouses. It is scoped to a specific `stockLocationId` and returns alternative bins relative to the `currentBinRackSuggestion`. Setting `includeNonPickLocations = true` returns storage locations that are normally non-pickable.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

### Pickwave item reallocations require batch information

Endpoints such as `Picking.UpdatePickingWaveItemWithNewBinrack` and `Picking.UpdatePickedItemDelta` are strictly applicable only to pickwave items carrying batch information.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

---

## Related Concepts

- [`inventory`](inventory.md) — Inventory item-location records can carry a configured `BinRack` string for a stock item at a specific stock location
- [`locations`](locations.md) — Warehouse fulfillment locations hosting binracks and zones
- [`pickwaves`](pickwaves.md) — Pick-wave allocation can reference physical batch/binrack inventory and alternative picking locations

---

## Related Workflows

- [`inventory_adjustment`](../workflows/inventory_adjustment.md) — Adjusting stock at specific warehouse locations
