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
---

## Purpose

Binracks describe where inventory is stored, moved, or picked within a Linnworks stock location.

Linnworks exposes two distinct binrack paradigms that must not be confused:
1. **Classic Item-Location Bin/Rack:** Simple `BinRack` strings configured per SKU per stock location (e.g. `A-01`).
2. **WMS Physical BinRacks:** Rich warehouse management entities identified by `BinRackId` (integer), carrying type, dimensions, capacity percentages, item/group restrictions, routing sequences, zone assignments, and physical batch inventory records.

---

## Two Distinct Binrack Models

```
┌────────────────────────────────────────────────────────┐
│ 1. Classic Inventory Model                             │
│    SKU (ItemNumber) + StockLocationId (Guid)           │
│    └── "BinRack" (String label, e.g. "SHELF-1")        │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│ 2. WMS Physical Warehouse Model                        │
│    BinRackId (int32)                                   │
│    ├── BinRack (String name)                           │
│    ├── LocationId (Guid) / StockLocationIntId (int32) │
│    ├── ZoneId (int32) & ZoneType                       │
│    ├── RoutingSequence & Capacity Limits               │
│    └── BatchInventoryId (int32 physical stock records) │
└────────────────────────────────────────────────────────┘
```

> [!IMPORTANT]
> Do not treat classic inventory BinRack strings and WMS `BinRackId` entities as interchangeable. Modifying a text BinRack string on an item does not create or move physical WMS stock.

---

## Core Identifiers

| Identifier | Type | Meaning |
|---|---|---|
| `BinRack` | `string` | Human-readable bin/rack name or code (e.g. `A-03-02`). |
| `BinRackId` | `int32` | Internal unique WMS binrack identifier. |
| `StockLocationId` | `Guid` (string) | Standard Linnworks stock-location UUID. |
| `StockLocationIntId` | `int32` | Integer stock-location identifier used by WMS and Zone APIs. |
| `StockItemId` | `Guid` (string) | Linnworks inventory product identifier. |
| `StockItemIntId` | `int32` | Integer stock-item identifier used in some stock and location APIs. |
| `ZoneId` | `int32` | WMS warehouse zone identifier. |
| `ZoneName` / `Name` | `string` | Warehouse zone's human-readable name. |
| `BinRackTypeId` | `int32` | WMS binrack type identifier (e.g. Pallet, Shelf, Bulk). |
| `BatchInventoryId` | `int32` | Physical batch/inventory record used in WMS stock movement and picking. |

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Read simple SKU → stock-location/binrack string** | `Stock.GetStockItemsLocation` | Returns location name and `BinRack` string for a batch of `StockItemId` + `StockLocationId` pairs. |
| **Read inventory item's configured location records** | `Inventory.GetInventoryItemLocations` | Retrieves item-location configuration records for a stock item. |
| **Add configured item-location bin/rack** | `Inventory.AddItemLocations` | Adds a new item-location bin/rack mapping. |
| **Update configured item-location bin/rack string** | `Inventory.UpdateItemLocations` | Updates the configured bin/rack text string for a stock item at a location. |
| **Find alternative physical locations while picking** | `Picking.GetItemBinracks` | Scoped to a `StockLocationId`, finds alternative bin locations where the item can be picked relative to the current suggestion. |
| **Find suitable WMS destination bins for an item** | `Stock.SearchBinracks` | Evaluates item/group restrictions and warehouse flow logic, returning candidate bins in preferred placement order. |
| **Read SKUs physically stored in a WMS bin** | `Stock.GetBinrackSkus` | Returns SKUs and batch details currently located in a specific `BinRackId`. |
| **Read WMS bin details by IDs** | `Stock.GetBinRacksById` | Retrieves full WMS binrack metadata for a list of `BinRackId` integers. |
| **Read warehouse zone hierarchy** | `Wms.GetWarehouseZonesByLocation` | Retrieves warehouse zone structures and parent/child hierarchies for a location. |
| **Find binracks belonging to specified zones** | `Wms.GetBinrackZonesByZoneIdOrName` | Queries binracks mapped to specific `ZoneIds` (int32) or `ZoneNames` within a `StockLocationIntId`. |
| **Map binrack to a warehouse zone** | `Wms.UpdateWarehouseBinrackBinrackToZone` | Links or updates the zone assignment for a WMS binrack. |
| **Physically move WMS inventory between bins** | `Stock.CreateWarehouseMove` | Moves physical stock using `BatchInventoryId`, quantity, and optional `BinrackIdDestination`. |
| **Change allocated pickwave item bin/batch** | `Picking.UpdatePickingWaveItemWithNewBinrack` | Reallocates a pickwave line to an alternative physical bin/batch. |

---

## Multi-Bin Storage and Picking

In WMS-enabled warehouses, a stock item can have physical inventory distributed across multiple distinct `BinRackId` locations:
- **Picking Suggestion:** `Picking.GetItemBinracks` takes `stockItemId`, `stockLocationId`, and `currentBinRackSuggestion` to discover secondary physical pick locations if the primary location is depleted or blocked.
- **WMS Candidate Search:** `Stock.SearchBinracks` evaluates warehouse stock flow configuration, bin types, and group restrictions to suggest optimal putaway or replenishment bins.
- **Wave Reallocation:** If a picker cannot pick from the suggested bin, `Picking.UpdatePickingWaveItemWithNewBinrack` reallocates the line to another batch/bin.

> [!NOTE]
> Do not assume that the classic catalog item's `BinRack` text string is automatically the WMS bin that will be selected during pickwave generation. WMS picking logic dynamically evaluates batch availability, bin priorities, and pickwave sorting rules (`BinPriority` vs `OrderView`).

---

## Gotchas & Operational Rules

### Binrack identity depends on API family

- **Classic Inventory APIs:** Represent the bin/rack as a text string associated with a `StockLocationId` (Guid).
- **WMS APIs:** Represent physical binracks as entity records with integer `BinRackId`, routing sequences, and zone assignments.
- Do not use text binrack names as global unique identifiers; they must always be evaluated in their specific warehouse location context.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/stock.json` | `vendor/PublicApiSpecs/1.0/inventory.json`

### Updating an item-location BinRack is not a WMS stock move

`Inventory.UpdateItemLocations` updates the item's configured catalog location/bin-rack text label. It does NOT physically transfer WMS inventory quantities between physical binracks. Physical WMS stock movement requires warehouse move operations (`Stock.CreateWarehouseMove` / `Stock.UpdateWarehouseMove`) operating against `BatchInventoryId` and `BinrackIdDestination`.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Stock.cs` | `vendor/LinnworksNetSDK/Controllers/Inventory.cs`

### WMS Zone APIs require `StockLocationIntId` (int32), not `StockLocationId` (Guid)

Zone endpoints on the `Wms` controller (such as `Wms.GetBinrackZonesByZoneIdOrName` and `Wms.GetWarehouseZonesByLocation`) require the integer location identifier `StockLocationIntId`, not the standard location GUID.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/wms.json`

### `Picking.GetItemBinracks` is location-scoped and relative

`Picking.GetItemBinracks` does not perform a global catalog search across all warehouses. It is scoped to a specific `stockLocationId` and returns alternative bins relative to the `currentBinRackSuggestion`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

---

## Related Concepts

- [`inventory`](inventory.md) — Products hold classic default binrack text strings
- [`locations`](locations.md) — Warehouse fulfillment locations hosting binracks and zones
- [`pickwaves`](pickwaves.md) — Pick waves route pickers through binracks based on routing sequence

---

## Related Workflows

- [`inventory_adjustment`](../workflows/inventory_adjustment.md) — Adjusting stock at specific warehouse locations
