---
title: Modify Open Orders By SKU
slug: modify_open_orders_by_sku
intent: "Use when a macro must find open orders that contain a specific SKU and apply a change to those orders or their items."
related_concepts: [open_orders, order_items]
related_workflows: [set_extended_property]
ambiguities:
  - type: ambiguous_mutation
    blocking: true
    question: "What should be changed on the matching orders or items?"
    reason: "'Modify' is underspecified. The write operation depends on the mutation type — each requires a different API endpoint and different inputs."
    possible_intents:
      - move_to_folder
      - set_extended_property
      - assign_shipping_service
      - change_order_status
      - update_order_item_quantity
      - remove_order_item
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/OpenOrders.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/openorders.json
  - type: migration_finding
    ref: migration/STATUS.md
---

## Intent

Use when a macro must identify open orders containing a specific SKU (or SKUs) and then
apply a mutation to those orders — for example, moving them to a folder, setting a property,
changing shipping, or modifying an item quantity.

Because "modify" is ambiguous, the write step must be clarified before code is generated.
The read step (retrieving + filtering orders) is well-defined and the same in all cases.

## Preconditions

- A valid `ViewId` for the target order view is known or can be retrieved via `OpenOrders.GetViewStats`
- The SKU(s) to match are known
- The target mutation has been clarified (see Ambiguities in frontmatter)
- If mutation is location-scoped, a real `LocationId` has been resolved via `Inventory.GetStockLocations`

## Inputs

| Input | Type | Notes |
|---|---|---|
| Target SKU(s) | `string` or `string[]` | The Linnworks catalog SKU (item.SKU), not ChannelSKU |
| View ID | `Guid` | Required by GetOpenOrders. `0` is not valid. |
| Location ID | `Guid` | Required by GetOpenOrders. Guid.Empty is NOT "all locations". |
| Mutation-specific inputs | varies | Depends on the write operation chosen |

## Workflow steps

1. **Resolve ViewId**
   `OpenOrders.GetViewStats` — verify the view exists and get its ID. If a fixed ViewId is
   configured by the user, validate it is non-zero before use.

2. **Resolve LocationId**
   `Inventory.GetStockLocations` — retrieve real location IDs. Never use `Guid.Empty` as a
   substitute for "all locations".

3. **Page through open orders**
   `OpenOrders.GetOpenOrders` — retrieve one page at a time (recommended: 200 per page).
   Loop until `PageNumber >= TotalPages`. Each `OpenOrder` in `Data` contains an `Items`
   array.

4. **Filter: does this order contain the target SKU?**
   For each `OpenOrder`, iterate `order.Items`. Compare `item.SKU` against the target SKU.
   Do NOT compare `item.ChannelSKU` unless the requirement is explicitly channel-side.
   If composites are in scope, also traverse `item.CompositeSubItems`.

5. **Decision: is the item detail from GetOpenOrders sufficient?**
   See Decision Points below.

6. **Apply mutation**
   The specific endpoint depends on the mutation type (see Ambiguities). All mutation
   endpoints require `pkOrderId`, not `NumOrderId`, as the order identifier.

7. **Log outcome**
   Always log `NumOrderId` (the human-readable order number) in every log line, not the
   GUID `pkOrderId`.

## Decision points

### Is the item detail from GetOpenOrders sufficient?

- **YES** (you only need SKU, Quantity, basic fields) → proceed directly to step 6
- **NO** (you need RowId for item-level mutations, BinRack data, or full pricing) →
  call `OpenOrders.GetOpenOrdersDetails` with the matched order IDs before step 6.
  GetOpenOrdersDetails accepts a list of IDs and is faster than calling GetOrderById
  per order.

### Does the mutation require the order to not be locked?

Some mutations (AssignToFolder, SetExtendedProperties, ChangeShippingMethod) fail on locked
or parked orders. Check `order.GeneralInfo.IsLocked` before attempting mutations if the order
population may include locked orders.

### Are composites in scope?

- **NO** (plain SKU match only) → compare `item.SKU` only
- **YES** (user may have composite/bundle SKUs) → also traverse `item.CompositeSubItems[]`
  and compare each sub-item's `SKU`

## Relevant operations

- `OpenOrders.GetViewStats` — Validate that the ViewId exists before using it
- `OpenOrders.GetOpenOrders` — Primary paged retrieval of open orders
- `OpenOrders.GetOpenOrdersDetails` — Retrieve full item detail by order ID list when needed
- `Inventory.GetStockLocations` — Resolve real LocationIds — never skip this step
- `Orders.AssignToFolder` — Mutation: move matched orders to a folder (requires folder ID)
- `Orders.GetAvailableFolders` — Required before AssignToFolder to resolve folder ID by name
- `Orders.SetExtendedProperties` — Mutation: set extended property (see set_extended_property workflow)
- `Orders.ChangeShippingMethod` — Mutation: change shipping service on matched orders
- `Orders.UpdateOrderItem` — Mutation: modify a specific item's fields (requires RowId)

## Gotchas

### ViewId = 0 is not valid

`GetOpenOrders` requires a real view ID. `ViewId = 0` is not a sentinel for "any view" and
will produce a server-side error. Retrieve valid IDs via `GetViewStats`.

**Source:** `migration_finding` — `migration/STATUS.md`

### LocationId = Guid.Empty is NOT "all locations"

Using `Guid.Empty` as `LocationId` targets the "Default" location only. On a multi-location
account this silently excludes most orders. Always resolve real IDs via `GetStockLocations`.

**Source:** `migration_finding` — `migration/STATUS.md`

### Pagination is not optional

`GetOpenOrders` is paginated. Passing a very large `EntriesPerPage` value is not a safe
substitute for real pagination — it will fail on large datasets or exceed macro time budgets.
Page with a fixed size (e.g. 200) and loop.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### ChannelSKU is not the same as SKU

Match against `item.SKU` unless the requirement explicitly concerns the channel's product
identifier. These values can differ significantly.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/ClassBase/OrderItem.cs`

### Mutations on locked orders will fail

`AssignToFolder`, `ChangeShippingMethod`, and similar write operations cannot be executed on
locked or parked orders. Check `GeneralInfo.IsLocked` and handle the skip gracefully.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

## Do not use when

- You need to act on **all** open orders (no SKU filter) — use GetOpenOrders directly without item-level filtering
- You need to act on **processed** (despatched) orders — use the ProcessedOrders controller
- You need to look up a stock item's catalog properties (levels, descriptions) — that is an Inventory workflow

## Related concepts

- `open_orders` — The parent order object
- `order_items` — The Items array and item-level identifiers

## Related workflows

- `set_extended_property` — When the mutation is setting a named property on matched orders
