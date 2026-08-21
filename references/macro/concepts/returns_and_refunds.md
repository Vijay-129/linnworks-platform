---
title: Returns, Refunds, and RMA Post-Sale
slug: returns_and_refunds
related_concepts: [processed_orders, open_orders, inventory]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ReturnsRefunds.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/PostSale.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/returnsrefunds.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/postsale.json
---

## Purpose

The post-sale customer service and RMA (Return Merchandise Authorization) subsystem in Linnworks.
Manages customer returns, item exchanges, full/partial order refunds, scrap bookings, and channel-notified
refund actions.

Macros use this subsystem to automate returns processing (e.g. auto-booking an exchange when a customer
support ticket is received, or approving refunds matching predefined business criteria).

## Core identifiers

| Identifier | Type | Description |
|---|---|---|
| `pkOrderId` | `Guid` (string) | System ID of the original processed order. |
| `pkRMAHeaderId` | `integer` | Unique ID of the RMA booking header. |
| `pkRefundHeaderId` | `integer` | Unique ID of the refund booking header. |
| `fkReturnLocationId` | `Guid` (string) | Stock location where returned items should be booked back into inventory. |
| `fkDespatchLocationId` | `Guid` (string) | Stock location where replacement exchange items will be despatched from. |

## Important models

| Model | Description |
|---|---|
| `OrderRMAHeader` | Top-level RMA record containing return/exchange lines. |
| `OrderRefundHeader` | Top-level refund summary record for an order. |
| `CreateRMABookingRequest` | Request model to book return lines, scrap items, and exchange items. |
| `CreateRefundRequest` | Request model to calculate and initiate order/shipping refunds. |
| `BookedReturnsExchangeItem` | Individual return/exchange item line in an RMA. |
| `ValidationResult` | Return validation output checking if an automated refund is permissible on the channel. |

Use `get_model` to see full field lists.

## Common operations

- `ReturnsRefunds.CreateRMABooking` — Book a customer return, exchange, or scrap request against a processed order.
- `ReturnsRefunds.CreateRefund` — Create an automated or manual refund against order items or shipping cost.
- `ReturnsRefunds.ActionRMABooking` — Accept and action a pending RMA booking in the system.
- `ReturnsRefunds.ActionRefund` — Transmit the approved refund amount to the sales channel (e.g. eBay / Amazon API).
- `ReturnsRefunds.GetRMAHeadersByOrderId` / `GetRefundHeadersByOrderId` — Look up active RMA and refund records for an order.
- `PostSale.GetPostSaleStatus` — Query the overarching post-sale status of an order.

## Returns / RMA Lifecycle

```
Customer Request (Return / Refund / Exchange)
       ↓
Pre-Validation: ProcessedOrders.IsRefundValid / ReturnsRefunds.GetReturnOptions
       ↓
Create RMA Booking (CreateRMABooking / CreateRefund)
       │  • Return items: stock placed in pending return
       │  • Exchange items: exchange order generated
       │  • Scrap items: marked as damaged/scrapped
       ▼
Action Booking: ReturnsRefunds.ActionRMABooking
       │  • Books returned stock back into Inventory (fkReturnLocationId)
       ▼
Action Refund: ReturnsRefunds.ActionRefund (Sends refund notification to sales channel)
```

## Gotchas

### Returned items do not increment stock until Actioned

Creating an RMA booking (`CreateRMABooking`) creates the return record, but does NOT immediately return
stock to available inventory. Stock is only returned to inventory once `ActionRMABooking` or `ActionBookedOrder`
is executed.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/returnsrefunds.json`

### Channel refund reasons must match channel-specific enums

When creating automated channel refunds, `ChannelReason` and `ChannelSubReason` must match the specific
values required by the marketplace (e.g. Amazon / eBay). Passing an invalid string will fail channel refund submission.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/ReturnsRefunds.cs`

### Batched items must have batch numbers specified on return

If returning a product that is marked as a batched inventory item (`isBatchedStockItem == true`), the
return booking must specify the batch number into which the item is being returned.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/ReturnsRefunds.cs`

## Related concepts

- `processed_orders` — Returns and refunds operate exclusively on processed orders
- `inventory` — Actioned returns book stock back into inventory locations

## Related workflows

- (Used in automated customer returns processing and marketplace refund sync macros)
