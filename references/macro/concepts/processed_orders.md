---
title: Processed Orders
slug: processed_orders
related_concepts: [open_orders, shipping, customers, extended_properties]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ProcessedOrders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/processedorders.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: migration_finding
    ref: migration/STATUS.md
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

Orders that have been fully processed (despatched) in Linnworks.

Processing finalizes the order for fulfillment. For normal linked and stock-tracked items, processing
deducts physical inventory from the order's fulfillment location and queues the order for channel
despatch synchronization. Once processed, an order leaves the open-order working set and is no
longer accessible via standard `OpenOrders` retrieval APIs.

Processed orders are primarily read-only for standard order attributes, but support a dedicated set
of post-sale operations, including order notes, returns, refunds, resends, exchanges, and audit tracking.

---

## Core Identifiers

| Identifier | Type | Description |
|---|---|---|
| `pkOrderId` | `Guid` (string) | Primary internal system GUID of the order. |
| `nOrderId` / `NumOrderId` | `integer` | Human-readable Linnworks order number used by operators and displayed in the UI. |
| `ReferenceNum` | `string` | Channel reference number (e.g. Amazon / eBay / Shopify order ID). |
| `dProcessedOn` | `DateTime` | Timestamp when the order was marked as processed/despatched. |
| `TrackingNumber` | `string` | Final carrier tracking reference (when supplied prior to processing). |

---

## Important Models

| Model | Description |
|---|---|
| `SearchProcessedOrdersRequest` | Search payload for `SearchProcessedOrders`: date ranges, field filters, sorting, sources, sub-sources, pagination. |
| `SearchProcessedOrdersResponse` | Response model returning matched `ProcessedOrderWeb` records and total count. |
| `ProcessedOrderWeb` | Summary model representing a processed order record. |
| `AuditEntry` | Historical log record returned by `GetProcessedAuditTrail` documenting operator/macro actions and timestamps. |
| `ReturnInfo` / `RefundInfo` | Post-sale transaction models representing return items, refund amounts, and reasons. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Search processed order history** | `ProcessedOrders.SearchProcessedOrders` | **Preferred current API** for searching historical processed orders. | 150/min |
| **Legacy paged processed search** | `ProcessedOrders.SearchProcessedOrdersPaged` | **Deprecated**. Maximum 3-month date range per request. | 150/min |
| **Export processed orders to CSV** | `ProcessedOrders.DownloadOrdersToCSV` | Direct CSV export of processed order datasets. | 150/min |
| **Retrieve direct carrier tracking URLs** | `ProcessedOrders.GetOrderTrackingURLs` | Resolves carrier tracking URLs using tracking numbers and vendor info. | 150/min |
| **Retrieve processed order audit trail** | `ProcessedOrders.GetProcessedAuditTrail` | Retrieves full timestamped action log for `pkOrderId`. | 150/min |
| **Read processed-order extended properties** | `ProcessedOrders.GetProcessedOrderExtendedProperties` | Dedicated retrieval endpoint for post-despatch metadata. | 250/min |
| **Add a post-processing note** | `ProcessedOrders.AddOrderNote` | Appends an internal or external note to a processed order. | 150/min |
| **Inspect return / exchange history** | `ProcessedOrders.GetReturnsExchanges` | Returns RMA, return, and exchange records for an order. | 150/min |
| **Inspect returnable item quantities** | `ProcessedOrders.GetReturnItemsInfo` | Validates quantities and items eligible for return. | 150/min |
| **Create a resend order** | `ProcessedOrders.CreateResend` / `CreateFullResend` | Generates a replacement order for lost or damaged goods. | 150/min |
| **Book an item exchange** | `ProcessedOrders.CreateExchange` | Generates an exchange order with `despatchLocation` and `returnLocation`. | 150/min |
| **Process financial refunds** | `ProcessedOrders.RefundFreeText` / `RefundShipping` | Applies financial refund lines against an order. | 150/min |

---

## Lifecycle & Post-Processing Flow

```
Open Order (OpenOrders / Orders Controllers)
    │
    │ Orders.ProcessOrder / Orders.ProcessOrdersInBatch
    ▼
Processed Order
    │
    ├── 1. Audit & Reporting (GetProcessedAuditTrail, DownloadOrdersToCSV)
    ├── 2. Order Notes (AddOrderNote)
    ├── 3. Tracking Lookup (GetOrderTrackingURLs)
    ├── 4. Extended Properties (GetProcessedOrderExtendedProperties)
    ├── 5. Returns & RMAs (GetReturnsExchanges, GetReturnItemsInfo)
    ├── 6. Financial Refunds (RefundFreeText, RefundShipping, RefundServices)
    ├── 7. Resends (CreateResend, CreateFullResend)
    └── 8. Exchanges (CreateExchange)
```

---

## Gotchas & Operational Rules

### Prefer `SearchProcessedOrders` over deprecated `SearchProcessedOrdersPaged`

`ProcessedOrders.SearchProcessedOrdersPaged` is marked as deprecated in public API specifications.
- Build new integrations and macros against **`ProcessedOrders.SearchProcessedOrders`**.
- When maintaining legacy workflows using `SearchProcessedOrdersPaged`, respect the maximum 3-month date range limit (`to - from <= 90 days`).

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/processedorders.json`

### Processed orders have limited post-processing mutability

Standard open-order mutation endpoints (`Orders.AssignToFolder`, `Orders.ChangeShippingMethod`, `Orders.UpdateOrderItem`, `Orders.SetExtendedProperties`) cannot be executed against processed orders. Post-despatch modifications are strictly limited to:
- Adding order notes (`ProcessedOrders.AddOrderNote`)
- Document reprinting (`PrintService.*`)
- Post-sale returns, refunds, resends, and exchanges (`ProcessedOrders.CreateReturn`, `CreateResend`, `CreateExchange`)

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/processedorders.json`

### Financial refund does not necessarily imply inventory return

Do not assume that executing a financial refund (`RefundFreeText`, `RefundShipping`) automatically increases available stock in inventory.
- Financial refunds and physical inventory returns are distinct operations.
- Physical stock re-entry requires booking a return/exchange workflow with an explicit `returnLocation`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/processedorders.json`

### Stock deductions apply to linked and tracked inventory

For normal linked and stock-tracked items, order processing deducts physical stock from the order's fulfillment location.
- Unlinked channel lines (`item.ItemId == Guid.Empty`) do not deduct catalog inventory.
- Untracked inventory items do not generate standard stock history decrements.
- Composite bundle parent items behave differently from their child stock components.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Shipping and tracking info must be set prior to processing

Shipping methods, postal services, and tracking numbers should be assigned while the order is in the open state. Processing finalizes this information for channel despatch notifications. Once processed, general shipping method assignments cannot be modified.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Customer PII may be redacted on historical orders

Depending on sales channel settings and `PIIRedactionDays` configuration, customer personal data (names, addresses, phone numbers) may be scrubbed from historical processed orders after a retention window. Macros reading historical orders must handle missing or redacted customer fields gracefully.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — The pre-despatch working state of an order
- [`shipping`](shipping.md) — Carrier consignments and tracking information are finalized upon processing
- [`customers`](customers.md) — Customer address data on processed orders is subject to PII redaction
- [`extended_properties`](extended_properties.md) — Read-only access to metadata via `GetProcessedOrderExtendedProperties`

---

## Related Workflows

- (Used in despatch audit, customer service lookup, tracking verification, and post-sale reporting macros)
