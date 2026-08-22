---
title: Warehouse Stock Transfers
slug: warehouse_transfers
related_concepts: [inventory, locations, binracks]
related_workflows: [inventory_adjustment]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/WarehouseTransfer.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Stock.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/ClassBase/TransferStatus.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/warehousetransfer.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/warehousetransfer-new.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The inter-warehouse stock movement subsystem in Linnworks.

Warehouse Transfers manage the movement of physical inventory between distinct Linnworks locations
(`fromLocationId` to `toLocationId`), tracking requested, sent, and received quantities across transfer bins
and discrepancies through the transfer lifecycle.

> [!NOTE]
> **Inter-Location Transfers vs. Intra-Warehouse Moves:**
> - **Warehouse Transfers (`WarehouseTransfer`):** Moves stock between distinct physical Linnworks warehouse locations.
> - **Warehouse Moves (`Stock.CreateWarehouseMove`):** Moves stock between binracks/zones within the *same* warehouse location.
> - **FBA Inbound (`WarehouseTransfer v2`):** A dedicated multi-stage Amazon FBA inbound shipment workflow.

---

## Core Identifiers and Version Context

| API Generation | Identifier | Type | Description |
|---|---|---|---|
| **Legacy (`/api/WarehouseTransfer/*`)** | `pkTransferId` | `Guid` (string) | Primary UUID of the transfer. |
| **Legacy (`/api/WarehouseTransfer/*`)** | `fromLocationId` / `toLocationId` | `Guid` (string) | Origin and destination warehouse location UUIDs. |
| **Legacy (`/api/WarehouseTransfer/*`)** | `ReferenceNumber` | `string` | User-defined or system transfer reference code. |
| **Legacy (`/api/WarehouseTransfer/*`)** | `Status` | `string enum` | Lifecycle status (`TransferStatus`). |
| **REST v1 / v2 (`/v1/*`, `/v2/*`)** | `transferId` | `int32` | Integer transfer identifier used by v1/v2 REST endpoints. |

---

## Important Models

| Model | Description |
|---|---|
| `WarehouseTransfer` | Top-level transfer header: `pkTransferId`, `fromLocationId`, `toLocationId`, `Status`, `ReferenceNumber`, `TransferDate`, `ItemCount`. |
| `WarehouseTransferItem` | Transfer item line: `pkTransferItemId`, `pkStockItemId` (Guid), `SKU`, `ItemTitle`, `RequestedQuantity`, `SentQuantity`, `ReceivedQuantity`. |
| `WarehouseTransferBin` | Container/bin tracking grouping items and quantities inside a transfer shipment. |
| `TransferStatus` | Status enum: `Draft`, `Request`, `Accepted`, `Packing`, `InTransit`, `CheckingIn`, `Delivered`. |
| `CreateWarehouseMoveRequest` | Payload for **`Stock.CreateWarehouseMove`**: moves stock between binracks within a single warehouse. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics |
|---|---|---|
| **Create new transfer request (Legacy)** | `WarehouseTransfer.CreateTransferRequestWithReturn` | Creates transfer between `fromLocationId` and `toLocationId`. |
| **Create new transfer (REST v1)** | `POST /v1/warehousetransfer/transfers/CreateTransfer` | Creates transfer using integer `transferId`. |
| **Add items to transfer** | `WarehouseTransfer.AddItemsToTransfer` | **Draft / Request status only.** Adds batch of stock items. |
| **Update requested quantity** | `WarehouseTransfer.ChangeTransferItemRequestQuantity` | **Draft / Request status only.** Modifies requested line quantity. |
| **Update sent quantity (pack shipment)** | `WarehouseTransfer.ChangeTransferItemSentQuantity` | Records dispatched count; adjusts origin physical inventory. |
| **Update received quantity (receive shipment)** | `WarehouseTransfer.ChangeTransferItemReceivedQuantity` | Records received count; credits destination physical inventory. |
| **Advance transfer lifecycle status** | `WarehouseTransfer.ChangeTransferStatus` | Updates status: `Draft`, `Request`, `Accepted`, `Packing`, `InTransit`, `CheckingIn`, `Delivered`. |
| **Reallocate stock between transfer bins** | `WarehouseTransfer.AllocateItemToBin` | Moves item quantity between bins belonging to an existing transfer. |
| **Query active transfers for a location** | `WarehouseTransfer.GetActiveTransfersForLocation` | Returns active transfers scoped to `locationId` (Guid). |
| **Query active transfers across all locations** | `WarehouseTransfer.GetActiveTransfersAllLocations` | Returns all active transfers across the account. |
| **Search transfers by location / criteria** | `WarehouseTransfer.SearchTransfersByLocation` | Paged search filtering by location, status, or date. |
| **Inspect transfer discrepancies** | `WarehouseTransfer.GetDiscrepancyItems` | Identifies quantity mismatches (`Sent < Requested` or `Received < Sent`). |
| **Create transfer from discrepancies** | `WarehouseTransfer.CreateTransferFromDescrepancies` | Generates a new transfer covering remaining undelivered quantities. |
| **Intra-warehouse bin-to-bin stock move** | `Stock.CreateWarehouseMove` | Moves stock between binracks in the same location (`InTransit` or `Open`). |

---

## Transfer Lifecycle Flow

```
WarehouseTransfer.CreateTransferRequestWithReturn (fromLocationId + toLocationId)
        │
        ▼
   Draft Status
        │  • Add items and set requested quantities (AddItemsToTransfer)
        ▼
  Request Status
        │  • Destination requests stock; items remain editable
        ▼
  Accepted Status
        │  • Origin accepts the transfer request
        ▼
  Packing Status
        │  • Items packed into transfer bins
        │  • Sent quantities recorded (ChangeTransferItemSentQuantity)
        │  • Origin physical stock decremented
        ▼
 InTransit Status
        │  • Transferred stock is physically in transit (unavailable at origin)
        ▼
CheckingIn Status
        │  • Goods arrive at destination
        │  • Received quantities verified (ChangeTransferItemReceivedQuantity)
        │  • Destination physical stock credited
        ▼
Delivered Status
        │  • Transfer completed and archived
        │  • Inspect discrepancies if Received < Sent (GetDiscrepancyItems)
```

---

## FBA Inbound Transfers (v2 REST API)

Inbound shipments to Amazon FBA utilize the dedicated **`WarehouseTransfer v2`** REST API family rather than the standard warehouse transfer workflow:
- **Shipping Plans & Shipments:** Manages FBA inbound plans, Amazon destination fulfillment centers, and shipment IDs.
- **Packing Groups & Cartons:** Organizes items into box configurations, carton weight limits, and pallet manifests.
- **Transport & Labels:** Coordinates carrier transport options, box barcode labels, and pallet thermal labels.
- Do not mix standard internal warehouse transfer endpoints with the multi-stage Amazon FBA inbound pipeline.

---

## Gotchas & Operational Rules

### Items and requested quantities can only be edited in `Draft` or `Request`

Calling `WarehouseTransfer.AddItemsToTransfer`, `AddItemToTransfer`, or `ChangeTransferItemRequestQuantity` is strictly permitted only while the transfer is in **`Draft`** or **`Request`** status. Once the transfer transitions to `Accepted` or `Packing`, the requested line item structure is locked.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/WarehouseTransfer.cs`

### Stock adjustments occur via quantity updates, not status changes alone

Do not assume that changing the transfer status enum alone performs inventory accounting:
- Updating sent quantity (**`ChangeTransferItemSentQuantity`**) decrements physical inventory at the origin warehouse.
- Updating received quantity (**`ChangeTransferItemReceivedQuantity`**) increments physical inventory at the destination warehouse.
- Maintain sent and received quantities explicitly during the packing and checking-in stages.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/warehousetransfer.json`

### Sent and in-transit quantities are unavailable for order fulfillment

Quantities that have been marked as sent and moved to `InTransit` are physically deducted from origin stock and cannot fulfill open orders at the origin. They are not credited to the destination warehouse until received quantities are recorded.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Intra-warehouse stock moves use `Stock.CreateWarehouseMove`

To physically move stock between binracks or zones within the same warehouse, use **`Stock.CreateWarehouseMove`** (under the `Stock` controller), not `WarehouseTransfer`.
- `WarehouseTransfer.AllocateItemToBin` is specifically for allocating items between transfer shipping bins, not for general warehouse bin-to-bin movements.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Stock.cs`

---

## Related Concepts

- [`inventory`](inventory.md) — Transfer quantity updates adjust physical stock levels at origin and destination
- [`locations`](locations.md) — Transfers require valid origin `fromLocationId` and destination `toLocationId`
- [`binracks`](binracks.md) — Intra-warehouse moves operate on WMS binracks via `Stock.CreateWarehouseMove`

---

## Related Workflows

- [`inventory_adjustment`](../workflows/inventory_adjustment.md) — Adjusting catalog stock levels and recording warehouse movements
