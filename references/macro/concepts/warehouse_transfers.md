---
title: Warehouse Stock Transfers
slug: warehouse_transfers
related_concepts: [inventory, locations, binracks]
related_workflows: [inventory_adjustment]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/WarehouseTransfer.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/warehousetransfer.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/2.0/warehousetransfer.json
---

## Purpose

The inter-warehouse and inter-bin stock movement subsystem in Linnworks. Warehouse transfers
manage the transit of stock items from an origin `FromLocationId` to a destination `ToLocationId`,
or between physical storage zones/binracks within a single warehouse.

Also manages FBA (Fulfillment by Amazon) inbound transfer shipments (via v2 WarehouseTransfer endpoints),
tracking carton configurations, pallet manifests, and transit statuses.

## Core identifiers

| Identifier | Type | Description |
|---|---|---|
| `pkTransferId` | `Guid` (string) | Unique identifier of the warehouse transfer. |
| `FromLocationId` | `Guid` (string) | Origin warehouse location GUID where stock is deducted. |
| `ToLocationId` | `Guid` (string) | Destination warehouse location GUID where stock is received. |
| `Status` | `enum` / `integer` | Transfer state: `Draft`, `Request`, `Sent` (In-Transit), `Received`, `Delivered`. |
| `TransferReference` | `string` | Human-readable transfer manifest reference code. |

## Important models

| Model | Description |
|---|---|
| `WarehouseTransfer` | Top-level transfer model: origin/destination locations, dates, status, item counts. |
| `WarehouseTransferItem` | Item line in a transfer: `pkStockItemId`, `Quantity`, `SentQuantity`, `ReceivedQuantity`. |
| `WarehouseTransferBin` | Container/bin tracking grouping items inside a transfer shipment. |
| `CreateWarehouseMoveRequest` | Request to move stock between binracks within the same location. |

Use `get_model` to see full field lists.

## Common operations

- `WarehouseTransfer.CreateTransfer` — Initialize a new inter-location stock transfer in `Draft` state.
- `WarehouseTransfer.AddItemsToTransfer` — Add a batch of stock items and quantities to a draft transfer.
- `WarehouseTransfer.ChangeTransferStatus` — Advance transfer state (e.g. `Draft` → `Sent`, `Sent` → `Received`).
- `WarehouseTransfer.GetActiveTransfers` / `GetTransfers` — Query transfers by origin, destination, or status.
- `WarehouseTransfer.CreateWarehouseMove` — Execute an immediate internal bin-to-bin stock transfer.

## Transfer Lifecycle

```
Draft / Request
       │  • Add items and expected quantities
       ▼
Sent (In Transit)
       │  • Stock is deducted from origin (FromLocationId)
       │  • Stock enters 'In Transit' state
       ▼
Received / Delivered
       │  • Stock is booked into destination (ToLocationId)
       │  • Any discrepancies are recorded as scrap or missing
```

## Gotchas

### Items can only be added to transfers in Draft/Request states

Calling `WarehouseTransfer.AddItemsToTransfer` on a transfer that has already transitioned to `Sent`
or `Delivered` will fail. All line items must be attached before marking as `Sent`.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/WarehouseTransfer.cs`

### Stock in transit is not available for order allocation

When a transfer is marked as `Sent`, the transferred stock is immediately removed from the origin location's
`Available` stock level and is held in transit. Neither the origin nor the destination can fulfill orders
from that quantity until the transfer is booked in (`Received`).

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/warehousetransfer.json`

### Internal bin moves vs Inter-location transfers

Moving an item from binrack `A-01` to `B-04` within the *same* warehouse location should use
`WarehouseTransfer.CreateWarehouseMove` or `Stock.UpdateItemBinrack`, not a full multi-stage inter-location transfer.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/warehousetransfer.json`

## Related concepts

- `inventory` — Transfers move stock inventory balances between locations
- `locations` — Origin and destination are defined by StockLocationId
- `binracks` — Transfers can specify source and destination binracks

## Related workflows

- `inventory_adjustment` — When adjusting levels locally rather than moving between warehouses
