---
title: Order Items
slug: order_items
related_concepts: [open_orders, inventory, binracks]
related_workflows: [modify_open_orders_by_sku]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/OpenOrders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/ClassBase/OrderItem.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/openorders.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

Line items within an open order. Each item represents a product, composite bundle, or service
line on the order, carrying its channel-provided SKU, catalog SKU link, quantity, pricing, tax,
availability counters, and binrack storage information.

> [!IMPORTANT]
> Order lines are **not guaranteed** to be linked to a Linnworks inventory item. An order line
> may represent:
> 1. A physical product successfully linked to a Linnworks `StockItem` (`ItemId != Guid.Empty`).
> 2. An unmapped/unlinked channel item awaiting product mapping.
> 3. A non-stock or service line (`IsService == true`).

---

## Core Identifiers

| Identifier | Type | Description |
|---|---|---|
| `SKU` | `string` | Product SKU. When linked, represents the canonical Linnworks catalog SKU. |
| `ChannelSKU` / `ItemNumber` | `string` | The item number / SKU as transmitted by the originating sales channel. |
| `ItemId` / `StockItemId` | `Guid` (string) | System GUID of the linked inventory stock item. Populated when linked to catalog. |
| `RowId` | `Guid` (string) | Unique ID for this specific line on this order. Explicitly required by `Orders.RemoveOrderItem`. |
| `OrderId` | `Guid` (string) | The parent order's `pkOrderId`. |
| `StockItemIntId` | `int32` | Alternate integer stock item identifier exposed by some models/API families. |

### Identifier Convention for SKU Matching

- **Catalog Matching:** Compare against `item.SKU` / `item.ItemId` on lines confirmed to be linked to inventory.
- **Channel Correlation:** Use `item.ChannelSKU` / `item.ItemNumber` when correlating marketplace listing records or investigating unmapped lines.
- **Unmapped Lines:** If `item.ItemId == Guid.Empty`, do not assume the line has a valid Linnworks catalog entry. Use `Orders.UpdateLinkItem` if mapping is required.

---

## Important Models

| Model | Description |
|---|---|
| `OrderItem` | Core line model: `ItemId` (Guid), `SKU`, `ItemNumber`, `Title`, `Quantity`, `PricePerUnit`, `Tax`, `IsService`, `RowId`, `CompositeSubItems`. |
| `OrderItemOption` | Custom listing/buyer options attached to the line item (e.g. engraving text, gift wrap). |
| `OrderItemBinRack` | Storage binrack details associated with the line item at the order's fulfillment location. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics |
|---|---|---|
| **Broad open-order scan with standard item lines** | `OpenOrders.GetOpenOrders` | Paged retrieval filtered by ViewId and LocationId. |
| **Lightweight scan of SKUs, barcodes, and IDs** | `OpenOrders.GetOrdersLowFidelity` | Fast scannable data. LocationId defaults to Default location. |
| **Rich item detail for known open orders** | `OpenOrders.GetOpenOrdersDetails` | Leave `DetailLevel` null/empty for full details; or specify `BINRACKS`, `NOTES`. |
| **Load specific orders with full items** | `Orders.GetOrders` (with `loadItems = true`) | Controller-level query with optional item graph loading. |
| **Add a linked inventory item to an order** | `Orders.AddOrderItem` | Takes `orderId`, `itemId` (Guid), `channelSKU`, `fulfilmentCenter`, `quantity`. |
| **Modify an existing order item** | `Orders.UpdateOrderItem` | Passes parent `orderId` and the updated `OrderItem` object (preserving line identity). |
| **Remove a line item from an order** | `Orders.RemoveOrderItem` | Explicitly requires the line `rowid` (Guid) and `fulfilmentCenter`. |
| **Recalculate packaging after item edits** | `Orders.RecalculateSingleOrderPackaging` | Recalculates packaging weights, dimensions, and splits. |
| **Link an unmapped order line to catalog item** | `Orders.UpdateLinkItem` | Links channel item to a Linnworks catalog `StockItemId`. |

---

## Composite (Bundle) Item Handling

A line item where `item.CompositeSubItems != null && item.CompositeSubItems.Count > 0` represents
a composite product (bundle/kit). When filtering orders by SKU, macros should match both top-level
parent SKUs and recursively inspect composite child components:

```csharp
bool ItemContainsSku(OrderItem item, string targetSku)
{
    if (string.Equals(item.SKU, targetSku, StringComparison.OrdinalIgnoreCase))
        return true;

    if (item.CompositeSubItems == null || item.CompositeSubItems.Count == 0)
        return false;

    return item.CompositeSubItems.Any(sub => ItemContainsSku(sub, targetSku));
}
```

---

## Gotchas & Operational Rules

### Order lines are not guaranteed to be linked to inventory

An order line can be an unmapped channel listing or a service line (`item.IsService == true`). Before invoking inventory-specific operations (`Stock.GetStockItemsFull`, `Inventory.GetInventoryItemById`), verify that `item.ItemId != Guid.Empty`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Do not identify an existing order line by SKU alone

An order can contain multiple distinct line items sharing the exact same `SKU` (e.g. different buyer options or separate line entries):
- To remove an item, `Orders.RemoveOrderItem` explicitly requires the item's `RowId` (Guid).
- To update an item, `Orders.UpdateOrderItem` requires the `OrderItem` object with its existing line identity preserved.
- Never attempt to mutate lines based solely on SKU string without handling duplicate lines.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

### Re-evaluate packaging after material item changes

Adding, removing, or changing item quantities alters package weight, item dimensions, and packaging split calculations. If downstream carrier selection or shipping label generation depends on packaging data, invoke `Orders.RecalculateSingleOrderPackaging(orderId)` after completing item mutations.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Validate order mutability before mutating items

Item mutations should only be executed while an order is open and unlocked. Check `order.GeneralInfo.IsLocked == false` before making changes, and handle API rejection errors defensively.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Parent order containing the line items
- [`inventory`](inventory.md) — Underlying catalog stock item record referenced by `ItemId`
- [`binracks`](binracks.md) — Storage location of order line items

---

## Related Workflows

- [`modify_open_orders_by_sku`](../workflows/modify_open_orders_by_sku.md) — Filter open orders containing target SKU and apply mutations
