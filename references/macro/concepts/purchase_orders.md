---
title: Purchase Orders and Inbound Procurement
slug: purchase_orders
related_concepts: [inventory, locations, extended_properties]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/PurchaseOrder.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/purchaseorder.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The procurement and inbound stock receipt subsystem in Linnworks. Purchase orders (POs) track
orders placed with external suppliers for stock replenishment.

When a PO transitions from `PENDING` to `OPEN`, items on the PO populate the `Due` (`OnOrder`) stock
counters in inventory. When items are delivered, physical on-hand stock is credited to the destination
warehouse location, `Delivered` quantities are updated, and outstanding `Due` balances are reduced.

---

## Core Identifiers and Header Fields

| Identifier | Type | Description |
|---|---|---|
| `pkPurchaseID` | `Guid` (string) | Primary system GUID of the purchase order. |
| `ExternalInvoiceNumber` | `string` | Purchase order reference number (e.g. `PO-10042`). |
| `SupplierReferenceNumber` | `string` | Supplier-side reference or order acknowledgement code. |
| `fkSupplierId` | `Guid` (string) | Supplier unique identifier in Linnworks. |
| `fkLocationId` | `Guid` (string) | Destination stock location UUID where goods will be received. |
| `Status` | `string enum` | Purchase order status: `PENDING`, `OPEN`, `PARTIAL`, `DELIVERED`. |

---

## Important Models

| Model | Description |
|---|---|
| `CommonPurchaseOrderHeader` | Header summary: `pkPurchaseID`, `ExternalInvoiceNumber`, `fkSupplierId`, `fkLocationId`, `Status`, `TotalCost`, `PostageTax`. |
| `CommonPurchaseOrderItem` | PO line item: `pkPurchaseItemId`, `fkStockItemId` (Guid), `SKU`, `ItemTitle`, `Quantity`, `Cost`, `Delivered`, `TaxRate`, `PackSize`. |
| `PurchaseOrderResponse` | Full PO object returned by `Get_PurchaseOrder`: contains header and line item list. |
| `Search_PurchaseOrders2_Response` | Paged search result containing matched PO headers. |
| `DeliverPurchaseOrderItemRequest` | Line delivery payload: `pkPurchaseId`, `pkPurchaseItemId`, `DeliveredQuantity`, `BatchNumber`, `BinRack`. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics |
|---|---|---|
| **Create new PO in PENDING status** | `PurchaseOrder.Create_PurchaseOrder_Initial` | Creates initial PO; items are added subsequently while in `PENDING`. |
| **Get complete PO (header + items)** | `PurchaseOrder.Get_PurchaseOrder` | Returns full PO graph for `pkPurchaseId` in a single call. |
| **Search/filter purchase orders** | `PurchaseOrder.Search_PurchaseOrders2` | **Preferred current search** supporting dates, supplier, location, status, and SKU. |
| **Legacy paged PO search** | `PurchaseOrder.Search_PurchaseOrders` | **Deprecated**. Use `Search_PurchaseOrders2` for new integrations. |
| **Add item line to PO** | `PurchaseOrder.Add_PurchaseOrderItem` | **PENDING status only.** Adds catalog item to purchase order. |
| **Update line quantity / cost / pack** | `PurchaseOrder.Update_PurchaseOrderItem` | **PENDING status only.** Modifies line item parameters and recalculates header totals. |
| **Delete item line from PO** | `PurchaseOrder.Delete_PurchaseOrderItem` | **PENDING status only.** Cannot delete lines where `Delivered > 0`. |
| **Bulk add / update / delete lines** | `PurchaseOrder.Modify_PurchaseOrderItems_Bulk` | **PENDING status only.** Batch item mutations with header recalculation. |
| **Transition PO status** | `PurchaseOrder.Change_PurchaseOrderStatus` | Supports `PENDING → OPEN`, `OPEN → DELIVERED`, `PARTIAL → DELIVERED`. |
| **Deliver specific PO line** | `PurchaseOrder.Deliver_PurchaseItem` | Receives quantity for a single line; credits on-hand stock and updates `Delivered`. |
| **Deliver selected item quantities** | `PurchaseOrder.Deliver_PurchaseItems_WithQuantity` | Batch receiving of specified quantities across lines. |
| **Deliver all items in full** | `PurchaseOrder.Deliver_PurchaseItemAll` | Completes delivery for all remaining outstanding lines on the PO. |
| **Deliver all non-batch items** | `PurchaseOrder.Deliver_PurchaseItemAll_ExceptBatchItems` | Delivers non-batch lines, leaving batch-tracked items for batch receipt. |
| **Audit goods receipt history** | `PurchaseOrder.Get_DeliveredRecords` | Retrieves historical delivery receipt entries for `pkPurchaseId`. |
| **Audit PO activity & changes** | `PurchaseOrder.Get_PurchaseOrderAudit` | Returns up to 1,000 audit log records documenting user/macro actions. |
| **Manage PO extended properties** | `PurchaseOrder.Add_PurchaseOrderExtendedProperty` | Unique property names required; max 50 properties per PO. |

---

## Purchase Order Lifecycle

```
PurchaseOrder.Create_PurchaseOrder_Initial
        │
        ▼
   PENDING Status
     • Add, update, and delete line items (Add_PurchaseOrderItem)
     • Configure costs, pack quantities, and supplier settings
        │
        │ PurchaseOrder.Change_PurchaseOrderStatus (PENDING → OPEN)
        ▼
     OPEN Status
     • Due / OnOrder counters populated in Inventory stock levels
     • Line item structure is locked
        │
        │ Inbound Delivery / Goods Receipt (Deliver_PurchaseItem*)
        ▼
    PARTIAL Status (if quantities remain outstanding)
     • Physical on-hand stock credited for delivered units
     • Delivered counter incremented on PO line
     • Outstanding Due balance reduced
        │
        │ Final Delivery Completed (or Change_PurchaseOrderStatus → DELIVERED)
        ▼
   DELIVERED Status
     • All line receipts completed
     • Due counters fully consolidated
```

---

## Gotchas & Operational Rules

### PO line structure is locked after opening

Adding, updating, and deleting individual item lines (`Add_PurchaseOrderItem`, `Update_PurchaseOrderItem`, `Delete_PurchaseOrderItem`, `Modify_PurchaseOrderItems_Bulk`) is strictly restricted to **`PENDING`** purchase orders.
- Complete all line item quantities, supplier costs, and pack sizes before transitioning the PO to `OPEN`.
- Header details (`Update_PurchaseOrderHeader`) can be updated while the PO is open, but line structures cannot be altered.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/purchaseorder.json`

### `PENDING → OPEN` populates `Due` (`OnOrder`) stock levels

Creating a PO in `PENDING` state does not alter inventory stock counters. It is the explicit transition from `PENDING` to `OPEN` via `PurchaseOrder.Change_PurchaseOrderStatus` that updates the `Due` (`OnOrder`) level in catalog inventory.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/purchaseorder.json`

### Goods receipt credits physical on-hand stock

Executing delivery endpoints (`Deliver_PurchaseItem`, `Deliver_PurchaseItemAll`) increases physical on-hand stock at the destination `fkLocationId` and increments `Delivered` on the line item.
- Do not equate goods receipt directly to free-to-sell `Available` stock, as open orders may immediately allocate arriving units (`InOrderBook`).
- Lines where `Delivered > 0` cannot be deleted from a PO.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/purchaseorder.json`

### Batch-tracked PO lines require batch-aware receiving

Do not use `Deliver_PurchaseItemAll` indiscriminately if items utilize batch tracking.
- Linnworks provides `PurchaseOrder.Deliver_PurchaseItemAll_ExceptBatchItems` to separate standard items from batch-tracked lines.
- Batch-tracked items must be received with explicit `BatchNumber`, expiry dates, and binrack assignments via `Deliver_PurchaseItem`.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/PurchaseOrder.cs`

### PO extended property constraints (Max 50 & Unique Names)

When adding metadata via `PurchaseOrder.Add_PurchaseOrderExtendedProperty`:
- Property names must be **unique** within the purchase order; adding a duplicate name throws an API error.
- A maximum of **50 extended properties** is allowed per purchase order; exceeding 50 throws an error.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/purchaseorder.json`

---

## Related Concepts

- [`inventory`](inventory.md) — POs populate `Due` stock counters and credit physical stock upon receipt
- [`locations`](locations.md) — PO deliveries are scoped to a destination warehouse `fkLocationId`
- [`extended_properties`](extended_properties.md) — PO-level metadata operates under dedicated PO extended property endpoints

---

## Related Workflows

- (Used in automated PO creation, supplier replenishment, goods receipt, and stock intake macros)
