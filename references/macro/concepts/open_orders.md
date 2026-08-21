---
title: Open Orders
slug: open_orders
related_concepts: [order_items, extended_properties, inventory, folders, shipping]
related_workflows: [modify_open_orders_by_sku, set_extended_property]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/OpenOrders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/openorders.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: migration_finding
    ref: migration/STATUS.md
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

Orders that Linnworks has received from a sales channel or created manually, but that have not yet
been fully processed (despatched). They live in the Open Orders section of the Linnworks UI
and are the primary working set for order-management macros.

Once processed, an order is no longer returned by normal open-order retrieval endpoints such as
`OpenOrders/GetOpenOrders` or `GetOpenOrdersDetails`. Processed-order data is primarily accessed
through the `ProcessedOrders` APIs, although `OpenOrders/SearchOrders` can optionally include
processed orders when `IncludeProcessed = true`.

---

## API Naming Warning: Two Distinct `GetOpenOrders` Endpoints

Linnworks exposes more than one endpoint named `GetOpenOrders` across different controllers:

- **`OpenOrders/GetOpenOrders`** (`POST /api/OpenOrders/GetOpenOrders`):
  View-based retrieval using `ViewId` (int32), `LocationId` (Guid), `EntriesPerPage`, `PageNumber`, and optional `OrderIds`.
- **`Orders/GetOpenOrders`** (`POST /api/Orders/GetOpenOrders`):
  Filter-based retrieval using `filters`, `sorting`, `fulfilmentCenter`, `additionalFilter`, and pagination.

> [!IMPORTANT]
> Always include the controller name (`OpenOrders` vs `Orders`) when documenting, referencing, or generating API calls to avoid calling the wrong endpoint with an incompatible request model.

---

## Core Identifiers

| Identifier | Type | Description |
|---|---|---|
| `pkOrderId` | `Guid` (string) | Internal primary key. Use as API parameter where required. |
| `NumOrderId` | `integer` | Human-readable Linnworks order number used by operators and displayed prominently in the UI. |
| `GeneralInfo.ReferenceNum` | `string` | Channel reference number (e.g. Amazon / eBay / Shopify order ID). |

### Identifier Convention for Macros

- **Operator Logs & Diagnostics:** Prefer `NumOrderId` in all log lines and error messages — it is the primary order number operators use to locate orders in the Linnworks UI.
- **Channel-Side Correlation:** Include `GeneralInfo.ReferenceNum` when correlating against channel order IDs.
- **API Payloads:** Use `pkOrderId` (Guid) only where an API method specifically requires the internal UUID.
- **Logging Best Practice:** Avoid logging raw `pkOrderId` GUIDs alone without `NumOrderId`, unless required for low-level SDK tracing. *(Source: `macro_convention`)*

---

## Important Models

| Model | Description |
|---|---|
| `OpenOrder` | Top-level order object returned by `OpenOrders/GetOpenOrders`. Contains `GeneralInfo`, `ShippingInfo`, `CustomerInfo`, `TotalsInfo`, `Items`. |
| `OrderGeneralInfo` | General order fields: status, channel `Source`/`SubSource`, received dates, reference numbers, `IsLocked`, `HoldOrCancel`. |
| `OrderItem` | Line item within the order. Contains `SKU`, `Quantity`, `PricePerUnit`, `TaxCostInclusive`. (See `order_items` concept). |
| `GetOpenOrdersRequest` | Request model for `OpenOrders/GetOpenOrders`. Key fields: `ViewId` (**int32**), `LocationId` (Guid), `EntriesPerPage` (int32), `PageNumber` (int32), `OrderIds` (Guid[] optional). |
| `PostFilterPagedResponse_OpenOrder` | Paged response from `OpenOrders/GetOpenOrders`. Fields: `Data` (`OpenOrder[]`), `TotalEntries`, `TotalPages`, `PageNumber`. |
| `GetOpenOrdersDetailsRequest` | Request for `OpenOrders/GetOpenOrdersDetails`. Fields: `OrderIds` (`Guid[]`) and optional `DetailLevel` (`FOLDER`, `NOTES`, `IDENTIFIERS`, `EXTENDEDPROPERTIES`, `BINRACKS`). An empty/null `DetailLevel` returns full details. |

Use `get_model` to see the complete field schemas for any of these.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Paginate orders from a Linnworks UI view** | `OpenOrders/GetOpenOrders` | Fast, view-filtered paged retrieval using `ViewId` (int32) and `LocationId` (Guid). |
| **Fast scan of SKUs, barcodes, and IDs** | `OpenOrders/GetOrdersLowFidelity` | Lightweight scan without loading heavy order graphs. |
| **Full details for a known batch of open orders** | `OpenOrders/GetOpenOrdersDetails` | Optimized specifically for open orders; faster than generic order APIs. |
| **Search orders by search term (open/processed)** | `OpenOrders/SearchOrders` | Indexed search across open orders, supporting optional inclusion of processed orders (`IncludeProcessed`). |
| **Filter and sort without a predefined view** | `Orders/GetOpenOrders` | Flexible controller-level filter/sort parameters. |
| **Assign orders to a folder** | `Orders/AssignToFolder` | Batch folder assignment by folder name (unlocked/unparked orders only). |
| **Add new order extended properties** | `Orders/AddExtendedProperties` | Append-oriented; accepts `BasicExtendedProperty[]` without `RowId`. |
| **Update extended properties preserving others** | `Orders.GetExtendedProperties` → merge → `Orders.SetExtendedProperties` | Replacement-oriented; perform read-merge-write to preserve existing metadata. |

---

## Common Operations

- `OpenOrders.GetOpenOrders` — Retrieve a paged list of open orders filtered by view and location.
- `OpenOrders.GetOpenOrdersDetails` — Retrieve full order detail for a batch of `OrderIds` with optional `DetailLevel`.
- `OpenOrders.GetOrdersLowFidelity` — Fast scan returning IDs, SKUs, and barcodes without full detail overhead.
- `OpenOrders.SearchOrders` — Search by term across open orders (and optionally processed orders).
- `OpenOrders.GetViewStats` — Retrieve statistics and order counts for a known `ViewId` (int32).
- `Orders.GetAvailableFolders` — Retrieve folder list to verify folder existence prior to assignment.
- `Orders.AssignToFolder` — Assign orders to a folder by name. **Cannot be executed on locked or parked orders.**
- `Orders.SetExtendedProperties` — Set extended properties on an order. **Warning: replaces existing property collection.**

---

## Lifecycle

```
Channel / Manual Order Received
       │
       ▼
   Open Order (Accessible via OpenOrders / Orders controllers)
       │
       │ Process / Despatch
       ▼
 Processed Order (Primarily accessed via ProcessedOrders; only limited post-processing operations remain available)
       │
       ├──── Return / RMA (optional post-sale path)
       │
       └──── Refund / Exchange (optional post-sale path)
```

Once an order moves to Processed, it is no longer returned by the normal `OpenOrders` retrieval
endpoints. `OpenOrders/SearchOrders` is a documented exception when `IncludeProcessed = true`.
Macros targeting open orders must act before despatch.

---

## Gotchas & Operational Rules

### `ViewId` (int32) must identify a real order view

`OpenOrders/GetOpenOrders` requires a valid integer `ViewId` (`int32`). Do not assume `ViewId = 0` means "all views" or "no view" — the server will reject it.
- Use a `ViewId` configured for the Linnworks account.
- Note that `Orders/GetOrderViews` is marked as deprecated in public API specifications.
- `OpenOrders/GetViewStats` is for retrieving statistics and cache info for a **known** `ViewId`, not for discovering available view IDs.

**Source:** `migration_finding` — `migration/STATUS.md` | `public_api_spec` — `vendor/PublicApiSpecs/1.0/openorders.json`

### Do not assume `Guid.Empty` means "all locations"

During migration validation, passing `Guid.Empty` as `LocationId` resolved to the account's Default location rather than all locations. On a multi-location account, this made 92% of open orders invisible (1,871 returned vs 23,520 actual).
- `GetOrdersLowFidelity` likewise documents an omitted/default `LocationId` as targeting the Default location.
- If a macro must cover all fulfillment locations, call `Inventory.GetStockLocations()` and loop over the real location IDs explicitly.

**Source:** `migration_finding` — `migration/STATUS.md` | `sdk_source` — `vendor/LinnworksNetSDK/Controllers/OpenOrders.cs`

### `SetExtendedProperties` is replacement-oriented

Do not treat `Orders.SetExtendedProperties` as a single-property upsert. The operation replaces the order's existing extended-property collection.

To safely update a property without destroying other metadata:
1. Retrieve current properties via `Orders.GetExtendedProperties`.
2. Modify or add the target property in memory.
3. Write the merged collection back with `Orders.SetExtendedProperties`, or use `Orders.AddExtendedProperties` if creating entirely new keys.

*(See workflow: [`set_extended_property`](../workflows/set_extended_property.md))*

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Prefer `GetOpenOrdersDetails` for detailed open-order retrieval

When you already have open-order `pkOrderId` values, prefer `OpenOrders/GetOpenOrdersDetails` over generic order APIs (`Orders/GetOrderById`).
- The SDK docs explicitly recommend `GetOpenOrdersDetails` as optimized and faster for open orders.
- Pass `DetailLevel` when full ancillary data (e.g. notes or binracks) is unnecessary.
- **Macro best practice:** Use bounded batches (e.g. 50–100 IDs per call) to keep payload size, memory, and timeout risks bounded.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs` | `macro_convention` — `references/standards/macro_conventions.md`

### `AssignToFolder` cannot run on locked or parked orders

`Orders.AssignToFolder` rejects locked or parked orders.
- Use `GeneralInfo.IsLocked` where available to identify locked orders.
- For parked-state detection, use the exact tag/status representation exposed by the target SDK/API model (such as tag 7 via `Orders.ChangeOrderTag`); do not infer parked state from `HoldOrCancel`.
- Always handle the API rejection defensively because order state can change between the read and assignment calls.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Pagination is mandatory

Never use `int.MaxValue` as `EntriesPerPage`. A growing dataset will eventually exceed the ~5-minute macro execution budget. Page with a fixed size (e.g. 100–200 entries) and loop until `PageNumber >= TotalPages`.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`order_items`](order_items.md) — Line items within an open order
- [`extended_properties`](extended_properties.md) — Key-value metadata on an order
- [`folders`](folders.md) — Organizing and staging open orders in folders
- [`shipping`](shipping.md) — Postal service allocation and shipping info
- [`inventory`](inventory.md) — Stock items referenced by open order items

---

## Related Workflows

- [`modify_open_orders_by_sku`](../workflows/modify_open_orders_by_sku.md) — Retrieve open orders and act on those containing a target SKU
- [`set_extended_property`](../workflows/set_extended_property.md) — Safe read-merge-write pattern for extended properties
