---
title: Locations and Warehouses
slug: locations
related_concepts: [inventory, open_orders, binracks, pickwaves]
related_workflows: [inventory_adjustment]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Locations.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Wms.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/locations.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/wms.json
  - type: migration_finding
    ref: migration/STATUS.md
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The warehouse and fulfillment location structure in Linnworks.

Every stock level, stock adjustment, open order allocation, and purchase order delivery is
explicitly associated with a `StockLocation`. Locations can represent physical warehouses,
retail branches, dropshippers, or external fulfillment centers.

---

## Architectural Separation: Classic Locations vs WMS Structures

```
Linnworks Account
  └── Stock Location (StockLocationId / StockLocationIntId)
        │
        ├── 1. Classic Item-Location Configuration
        │      └── Item-Location BinRack text strings (e.g. "SHELF-1")
        │
        └── 2. WMS Structures (Where Warehouse Management is configured)
              ├── Warehouse Zones (ZoneId int32, Parent Zones, Hierarchy)
              ├── Physical BinRacks (BinRackId int32, Capacities, Restrictions)
              └── Warehouse Totes (ToteBarcode, TotId)
```

> [!NOTE]
> Not all stock locations contain WMS structures. Simple stock locations only maintain classic
> item-location bin/rack strings. WMS zones, physical binracks, and totes apply exclusively to
> WMS-enabled warehouse locations.

---

## Core Identifiers

| Identifier | Type | Description |
|---|---|---|
| `StockLocationId` / `pkStockLocationId` | `Guid` (string) | Standard location UUID used by Inventory, Locations, Orders, and classic APIs. |
| `StockLocationIntId` | `int32` | Integer location identifier required by multiple WMS APIs (e.g. `Wms.GetWarehouseZonesByLocation`). |
| `LocationName` | `string` | Human-readable name of the location (e.g. `Main Warehouse`, `FBA UK`, `Default`). Used in UI and some order-creation endpoints. |
| `IsFulfillmentCenter` | `boolean` | Indicates that the location is configured for fulfillment-center behavior. |

---

## Important Models

| Model | Description |
|---|---|
| `StockLocation` | Core location model: `StockLocationId` (Guid), `LocationName` (string), `IsFulfillmentCenter` (bool), `Address`. |
| `WarehouseZone` | WMS zone definition: `ZoneId` (int32), `Name`, `StockLocationIntId` (int32), `ParentZoneId`. |
| `WarehouseTote` | Warehouse tote model: `ToteBarcode` (string), `TotId` (int32), `LocationId` (Guid). |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Identifier Semantics | Rate Limit |
|---|---|---|---|
| **List configured stock locations** | `Inventory.GetStockLocations` | Returns active `StockLocation[]` across the account. | 150/min |
| **Get details for a single location** | `Locations.GetLocation` | Takes `pkStockLocationId` (Guid). | 150/min |
| **Create a new stock location** | `Locations.AddLocation` | Location configuration payload. | 150/min |
| **Update location details/fulfillment status** | `Locations.UpdateLocation` | Updates location name and fulfillment configuration. | 150/min |
| **Move open orders to a target location** | `Orders.MoveToLocation` | Takes `orderIds` (`Guid[]`) + `pkStockLocationId` (`Guid`). | 250/min |
| **Create new orders at a location** | `Orders.CreateOrders` | Accepts `location` name string. | 150/min |
| **Read configured item-location bin/racks** | `Inventory.GetInventoryItemLocations` | Scoped by `inventoryItemId` (Guid) + `StockLocationId`. | 150/min |
| **Read WMS zones for a location** | `Wms.GetWarehouseZonesByLocation` | Requires `stockLocationIntId` (**int32**). | 150/min |
| **Read warehouse totes for a location** | `Locations.GetWarehouseTOTEs` | Takes `LocationId` (**Guid**), optional barcode/ID. | 150/min |

---

## Common Operations

- `Inventory.GetStockLocations` — Retrieve all active warehouse locations.
- `Locations.GetLocation` — Retrieve details for a specific `pkStockLocationId` UUID.
- `Locations.AddLocation` — Create a new warehouse location record.
- `Locations.UpdateLocation` — Update location settings or fulfillment-center parameters.
- `Orders.MoveToLocation` — Transfer open orders from their current fulfillment location to a target location UUID.
- `Wms.GetWarehouseZonesByLocation` — Retrieve WMS zone hierarchy for a `stockLocationIntId`.
- `Locations.GetWarehouseTOTEs` — Retrieve active picking totes for a warehouse location UUID.

---

## Gotchas & Operational Rules

### Location identifier requirements are endpoint-dependent

Do not assume all location endpoints take a GUID:
- Classic `Inventory`, `Locations`, and `Orders` endpoints commonly use **`StockLocationId` (Guid)**.
- WMS zone endpoints (`Wms.GetWarehouseZonesByLocation`, `Wms.GetBinrackZonesByZoneIdOrName`) require **`StockLocationIntId` (int32)**.
- Order ingestion (`Orders.CreateOrders`) can accept a **`LocationName` (string)**.
- Always verify the expected identifier type in the target endpoint's request model.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/wms.json` | `vendor/PublicApiSpecs/1.0/locations.json`

### Moving an order changes its fulfillment location context

`Orders.MoveToLocation` transfers open orders to a target `pkStockLocationId` and can optionally apply a fulfillment status (`Unassigned`, `Assigned`, `Submitted`, `Accepted`).
- Moving an order alters the stock location from which line items will be allocated.
- Do not assume Linnworks will automatically preserve the same allocation or fulfillment state if stock levels differ at the destination.
- If downstream processing requires available inventory, validate stock availability explicitly at the target location after moving.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Do not treat `Guid.Empty` as an "all locations" wildcard

Migration testing on the target account demonstrated that passing `Guid.Empty` (`00000000-0000-0000-0000-000000000000`) resolved to the location named "Default" rather than aggregating across all stock locations.
- This is a migration/runtime finding and should not be generalized to every endpoint without verification.
- In multi-location accounts, always query `Inventory.GetStockLocations()` to retrieve valid location IDs.

**Source:** `migration_finding` — `migration/STATUS.md`

### Resolve configured locations once per macro execution

Do not hardcode environment-specific `StockLocationId` GUIDs into macro code.
- Retrieve locations at macro initialization via `Inventory.GetStockLocations()` and resolve the target location by `LocationName` or configuration key.
- If matching by `LocationName`, fail explicitly if no match or multiple ambiguous matches are returned.
- Cache the resolved location in memory for the remainder of the macro execution rather than calling `GetStockLocations` inside item loops.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`inventory`](inventory.md) — Stock levels and catalog availability are tracked per stock location
- [`open_orders`](open_orders.md) — Open orders are assigned to a fulfillment location
- [`binracks`](binracks.md) — Physical storage coordinates and WMS binracks within a location
- [`pickwaves`](pickwaves.md) — Picking waves and totes are scoped to a warehouse location

---

## Related Workflows

- [`inventory_adjustment`](../workflows/inventory_adjustment.md) — Resolve location, find item by SKU, and adjust stock levels
