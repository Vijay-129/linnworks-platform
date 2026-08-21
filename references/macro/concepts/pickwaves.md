---
title: Picking Waves and Warehouse Picking
slug: pickwaves
related_concepts: [open_orders, locations, order_items, binracks]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Picking.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/ClassBase/PickingWaveGenerate.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/picking.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/wms.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The warehouse picking subsystem in Linnworks.

A picking wave (or pickwave) groups open orders and aggregates their line items to optimize physical
warehouse pick paths. Pickers can pick in single-order mode, batch mode, or tote-based multi-order pickwaves.

Macros use the picking endpoints to automate pickwave creation (e.g. generating waves for express
orders, auto-allocating orders that have reached complete inventory availability, or balancing workloads across pickers).

---

## Core Identifiers and Picking Fields

| Field | Type | Meaning & Constraints |
|---|---|---|
| `PickingWaveId` | `int32` | Primary unique numeric identifier of a picking wave in Linnworks. |
| `LocationId` | `Guid` (string) | Warehouse stock location UUID associated with the wave generation or query. |
| `UserId` | `int32` | Linnworks picker/user identifier assigned to the wave. (Note: passing `-1` in `UpdatePickingWaveHeader` deallocates the current user). |
| `State` | `string enum` | Current pickwave state: `Unallocated`, `Allocated`, `InProgress`, `Paused`, `Complete`, `Abandoned`, `Packing`, `Shipped`. |
| `SortingType` | `enum` / `string` | Pick path ordering: `BinPriority` (optimizes bin sequence) or `OrderView` (preserves order grouping). |
| `GroupType` | `enum` / `string` | Aggregation mode: `Items` (item-level batch pick) or `Orders` (order-by-order pick). |
| `ToteBarcode` | `string` | Operational tote identifier where the selected warehouse workflow utilizes totes (not a wave primary key). |

---

## Important Models

| Model | Description |
|---|---|
| `PickingWaveGenerate` | Generation payload: `LocationId` (Guid), `UserId` (int32?), `SortingType`, `GroupType`, `Orders` (`PickingWaveGenerateOrder[]`), `Pickwaves` (`PickingWaveGenerateMulti[]`). |
| `PickingWaveHeader` | Summary header model returned by `GetAllPickingWaveHeaders`: `PickingWaveId`, `LocationId`, `UserId`, `State`, `OrderCount`, `ItemCount`. |
| `PickingWaveItem` | Pickable line item in a wave: `OrderItemRowId`, `SKU`, `Quantity`, `BinRack`, `BatchNumber`, `PickedQuantity`. |
| `CheckAllocatableToPickwaveResponse` | Preflight validation result indicating which order IDs are eligible for wave generation. |
| `GeneratePickingWaveResponse` | Outcome of wave creation containing the generated `PickingWaveId` and allocation details. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Pre-validate candidate orders for picking** | `Picking.CheckAllocatableToPickwave` | Preflight check validating order eligibility before wave generation. | 150/min |
| **Generate a new picking wave** | `Picking.GeneratePickingWave` | Creates wave using location, user, sorting/grouping, item row IDs, and batch IDs. | 150/min |
| **List pickwave headers (by state/location)** | `Picking.GetAllPickingWaveHeaders` | Paged query filtering by `state` enum and `locationId`. | 150/min |
| **Retrieve complete single wave details** | `Picking.GetPickingWave` | Returns full pickwave graph for `pickingWaveId` (int32). | 150/min |
| **Update wave state or reassign picker** | `Picking.UpdatePickingWaveHeader` | Updates `State`, `UserId` (-1 to unassign), and timestamps. | 150/min |
| **Record picked item quantities** | `Picking.UpdatePickingWaveItem` | Updates picked status and counts for individual wave lines. | 150/min |
| **Reallocate wave line to another batch/bin** | `Picking.UpdatePickingWaveItemWithNewBinrack` | Changes batch/binrack for an allocated wave line with batch tracking. | 150/min |
| **Find alternative pick bins for an item** | `Picking.GetItemBinracks` | Returns alternative locations relative to the currently suggested binrack. | 150/min |
| **Remove orders from an active wave** | `Picking.DeleteOrdersFromPickingWaves` | Removes designated orders from an existing pickwave. | 150/min |

---

## Warehouse Picking Execution Flow

```
Candidate Open Orders (Filtered by View / Tags / Shipping)
        │
        ▼
Picking.CheckAllocatableToPickwave (Preflight Validation)
        │
        ▼
Build Allocation Payload (Order-Item RowIds + Batch IDs)
        │
        ▼
Picking.GeneratePickingWave (LocationId + UserId + SortingType)
        │
        ▼
Picking Wave Created (State = Unallocated / Allocated)
        │
        ▼
Picker Executes Wave (UpdatePickingWaveHeader / UpdatePickingWaveItem)
        │
        ▼
Wave Completed (State = Complete / Packing)
        │
        ▼
Downstream Pack Bench & Despatch Station Workflow
```

---

## Gotchas & Operational Rules

### `UserId` is an `int32` identifier, not a GUID

The Picking API identifies warehouse users using an integer `UserId` (`int32`), not a user GUID. Passing a GUID string will trigger validation errors.
- In `Picking.UpdatePickingWaveHeader`, setting `UserId = -1` unassigns/deallocates the current picker.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Picking.cs` | `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

### Pickwave generation is item-allocation aware

`Picking.GeneratePickingWave` is not merely a list of order IDs.
- The request requires individual order-item row identifiers (`RowId`), including composite child rows.
- If items use batch tracking, the applicable `BatchId` must also be supplied in the allocation request.
- Construct the generation payload using item allocation records returned by Linnworks rather than attempting to synthesize it from SKU and quantity alone.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/ClassBase/PickingWaveGenerate.cs`

### Pre-validate candidate orders with `CheckAllocatableToPickwave`

Always call `Picking.CheckAllocatableToPickwave(orderIds)` before attempting wave generation. Orders may be ineligible due to unallocated inventory, payment holds, or existing wave assignments. Inspect the returned response and filter out ineligible orders prior to building the generation request.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

### Generate waves within one location context

`GeneratePickingWave` accepts a single `LocationId` (Guid). Never attempt to combine orders from multiple fulfillment locations into a single wave generation request. Group candidate orders by their resolved `StockLocationId` and generate separate waves per warehouse.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

### `Picking.GetItemBinracks` is relative to the suggested bin

`Picking.GetItemBinracks` is designed for exception handling during picking. It returns alternative storage locations for a stock item relative to `currentBinRackSuggestion` within a specified `stockLocationId`. Setting `includeNonPickLocations = true` returns non-pickable storage areas.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

### Pickwave membership affects order operations

Orders allocated to active picking waves are locked into the warehouse picking process. Where a macro needs to cancel, split, or materially modify an order that has been added to a wave, call `Picking.DeleteOrdersFromPickingWaves` first to withdraw the order from picking before executing order-level modifications.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/picking.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders provide the source order lines allocated into pickwaves
- [`locations`](locations.md) — Waves and picking totes are scoped to a specific warehouse `StockLocationId`
- [`order_items`](order_items.md) — Item-level `RowId` and composite child rows are required for wave generation
- [`binracks`](binracks.md) — Pickwave path optimization evaluates binrack routing sequences and bin priorities

---

## Related Workflows

- (Used in automated pickwave generation, rush-order allocation, and warehouse workload balancing macros)
