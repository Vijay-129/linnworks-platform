---
title: Returns, Refunds, and RMA Post-Sale
slug: returns_and_refunds
related_concepts: [processed_orders, inventory, locations]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ReturnsRefunds.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ProcessedOrders.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/returnsrefunds.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/processedorders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The processed-order returns, exchanges, resends, and refunds subsystem in Linnworks.

Manages customer returns, replacement item exchanges, resends for lost/damaged goods, scrap bookings,
and channel-synchronized financial refunds.

> [!IMPORTANT]
> **RMA vs Refund Separation:** An RMA and a refund are not the same transaction in Linnworks.
> - An **RMA** manages physical item returns, exchanges, resends, scrap tracking, and warehouse stock receipts.
> - A **Refund** manages monetary amounts returned to the customer or submitted to the sales channel.
>
> While an RMA item can include an associated `RefundAmount`, integrations must model and action RMA
> physical handling and financial refund submission through their respective separate workflows.

---

## Core Identifiers and Location Fields

| Field | Type | Description |
|---|---|---|
| `OrderId` | `Guid` (string) | System UUID of the original processed order. |
| `RMAHeaderId` | `int32` | Unique integer identifier of the RMA booking header. |
| `RefundHeaderId` | `int32` | Unique integer identifier of the refund booking header. |
| `ReturnLocation` | `Guid` (string) | Destination warehouse stock location where returned items will be received. |
| `DespatchLocationId` | `Guid` (string) | Warehouse stock location from which exchange or resend items will be despatched. |

---

## Important Models

| Model | Description |
|---|---|
| `OrderRMAHeader` | Top-level RMA record: `RMAHeaderId` (int32), `OrderId` (Guid), `NumOrderId`, `Status`, `CreatedDate`, `Actioned`, `RMALines`. |
| `CreateRMABookingRequest` | RMA booking payload: `OrderId`, `ReturnItems` (`ReturnItem[]`), `ExchangeItems` (`ExchangeItem[]`), `ResendItems` (`ResendItem[]`), `ChannelInitiated`, `Reference`. |
| `CreateRMABookingResponse` | Response returned by `CreateRMABooking` containing the created `RMAHeaderId`. |
| `ReturnItem` | RMA return line: `OrderItemRowId`, `ReturnQuantity`, `RefundAmount`, `ScrapQuantity`, `ReturnLocation`, `BatchInventoryId`, `ReasonTag`, `SubReasonTag`, `BinrackOverride`. |
| `ExchangeItem` | RMA exchange line: `OrderItemRowId`, `Quantity`, `SKU`, `DespatchLocationId`, `ReasonTag`. |
| `CreateRefundRequest` | Financial refund payload: `OrderId`, `RefundItems`, `RefundShipping`, `RefundServices`, `Reason`, `ReasonTag`. |
| `CreateRefundResponse` | Outcome containing the created `RefundHeaderId` awaiting approval. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Retrieve channel RMA capabilities & reasons** | `ReturnsRefunds.GetReturnOptions` | Returns supported return types and predefined reason tags for the channel. | 150/min |
| **Book return, exchange, or resend (RMA)** | `ReturnsRefunds.CreateRMABooking` | Creates RMA booking; does **not** adjust physical inventory until actioned. | 150/min |
| **Update an existing RMA booking** | `ReturnsRefunds.UpdateRMABooking` | Updates lines or details on an un-actioned RMA booking. | 150/min |
| **Look up RMA headers for an order** | `ReturnsRefunds.GetRMAHeadersByOrderId` | Returns all RMA bookings associated with `OrderId`. | 150/min |
| **List pending RMA bookings awaiting action** | `ReturnsRefunds.GetActionableRMAHeaders` | Paged query of pending RMAs ready to be processed in the warehouse. | 150/min |
| **Action booked RMA (receive stock / generate orders)** | `ReturnsRefunds.ActionRMABooking` | Accepts RMA: credits stock at `ReturnLocation` and generates exchange orders. | 150/min |
| **Delete an un-actioned RMA booking** | `ReturnsRefunds.DeleteRMA` | Removes un-actioned lines and deletes header if no lines remain. | 150/min |
| **Retrieve channel refund options** | `ReturnsRefunds.GetRefundOptions` | Returns channel-specific monetary refund capabilities and reasons. | 150/min |
| **Pre-validate proposed refund** | `ProcessedOrders.IsRefundValid` | Validates whether a proposed refund is permissible on the channel. | 150/min |
| **Create financial refund for approval** | `ReturnsRefunds.CreateRefund` | Creates refund record in system awaiting action/approval. | 150/min |
| **Update an un-actioned refund** | `ReturnsRefunds.UpdateRefund` | Modifies refund lines prior to channel transmission. | 150/min |
| **Look up refund headers for an order** | `ReturnsRefunds.GetRefundHeadersByOrderId` | Returns all refund records associated with `OrderId`. | 150/min |
| **List pending refunds awaiting transmission** | `ReturnsRefunds.GetActionableRefundHeaders` | Paged query of approved refunds ready to transmit to the channel. | 150/min |
| **Transmit approved refund to sales channel** | `ReturnsRefunds.ActionRefund` | Submits refund to marketplace API (e.g. Amazon / eBay / Shopify). | 150/min |
| **Delete an un-actioned refund** | `ReturnsRefunds.DeleteRefund` | Removes pending refund header/lines. | 150/min |
| **Search return and refund history** | `ReturnsRefunds.SearchReturnsRefundsPaged` | Paged search over historical returns/refunds (max 3-month date range). | 150/min |

---

## Post-Sale Lifecycles: RMA and Refund Workflows

```
                          Processed Order (pkOrderId)
                                      │
        ┌─────────────────────────────┴─────────────────────────────┐
        ▼                                                           ▼
   RMA Workflow                                              Refund Workflow
(Returns / Exchanges / Resends)                           (Monetary Adjustments)
        │                                                           │
ReturnsRefunds.GetReturnOptions                           ReturnsRefunds.GetRefundOptions
        │                                                 ProcessedOrders.IsRefundValid
        ▼                                                           │
ReturnsRefunds.CreateRMABooking                                     ▼
  • Return items recorded as booked                       ReturnsRefunds.CreateRefund
  • ScrapQuantity marked for disposal                       • Creates pending refund record
  • No physical stock changes yet                           • No money transferred yet
        │                                                           │
        ▼                                                           ▼
Actionable RMA State                                      Actionable Refund State
(ReturnsRefunds.GetActionableRMAHeaders)                  (ReturnsRefunds.GetActionableRefundHeaders)
        │                                                           │
ReturnsRefunds.ActionRMABooking                           ReturnsRefunds.ActionRefund
  • Physical stock credited at ReturnLocation               • Transmits refund to sales channel
  • Scrap items written off                                 • Records final financial completion
  • Exchange / Resend orders created
```

---

## Gotchas & Operational Rules

### RMA creation does NOT alter inventory until actioned

Calling `ReturnsRefunds.CreateRMABooking` records the return/exchange booking but does **not** adjust physical inventory or available stock.
- Physical returned stock is only credited to `ReturnLocation` when `ReturnsRefunds.ActionRMABooking` or `ReturnsRefunds.ActionBookedOrder` is executed upon receiving the goods.
- `ScrapQuantity` identifies units that should be written off / disposed of rather than booked back into inventory when the RMA is actioned.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/returnsrefunds.json`

### Channel RMA and refund reasons must use predefined tags

When creating RMAs or refunds for channel orders:
- Consult `ReturnsRefunds.GetReturnOptions` (for RMAs) and `ReturnsRefunds.GetRefundOptions` (for refunds) to obtain valid channel reason codes.
- Supply `ReasonTag` and, where applicable, `SubReasonTag` on return lines. Supplying unmapped or arbitrary strings can cause channel submission rejections during `ActionRefund`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/returnsrefunds.json`

### Batched returns require an explicit batch inventory selection

For batched inventory items (`isBatchedStockItem == true`):
- The return request must specify the target batch inventory record via `BatchInventoryId`.
- Do not assume Linnworks can automatically determine which batch should receive the returned quantity.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/ReturnsRefunds.cs`

### Date range limitation on historical search

When searching historical returns or refunds via `ReturnsRefunds.SearchReturnsRefundsPaged`:
- The maximum date range (`to - from`) is **3 months** per query.
- For longer audit periods, split queries into sequential quarterly spans.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/ReturnsRefunds.cs`

---

## Related Concepts

- [`processed_orders`](processed_orders.md) — RMAs and refunds are booked against processed historical orders
- [`inventory`](inventory.md) — Physical stock is credited to inventory only when an RMA is actioned
- [`locations`](locations.md) — Returned goods require a target `ReturnLocation`, and exchanges require a `DespatchLocationId`

---

## Related Workflows

- (Used in automated customer service returns processing, RMA intake, and channel refund synchronization macros)
