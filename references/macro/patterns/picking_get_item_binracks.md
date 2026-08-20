---
title: Picking.GetItemBinracks - which fields are real, live-confirmed
added_by: platform
date: 2026-08-20
---

## Problem

`get_model` couldn't be trusted for `BinRackStockItem`/`StockItemBatch` (a real
generator gap, now fixed - see the `sync_api_spec.py` commit from this date), and
even with the model shape known, it wasn't clear which of its declared fields the
live API actually populates versus which are always `null` from this specific
endpoint. A macro working around this uncertainty resorted to reflection and
several candidate property-name spellings. This doc replaces that guesswork with
a live-confirmed answer, so future macros can trust the typed model directly.

## Which API / helper

`Api.Picking.GetItemBinracks(GetItemBinracksRequest { StockItemId,
StockLocationId, CurrentBinRackSuggestion, IncludeNonPickLocations })` ->
`GetItemBinracksResponse { AlternateLocations, PickableBins, NonPickableBins }`.
See `get_model("GetItemBinracksResponse")` / `get_model("BinRackStockItem")` /
`get_model("StockItemBatch")` for the full declared shape - this doc only adds
what's confirmed live, it doesn't replace those.

## Live-confirmed 2026-08-20 (real EU account, real batch-tracked item)

Called both the typed SDK method and the raw, unparsed JSON response
(`Factory.GetResponse`) for the same request and diffed them. Every field below
was present with a real, non-null, non-default value on the wire - trust these
without a null/reflection guard:

**`PickableBins[]` (`BinRackStockItem`)**: `BatchId`, `BatchInventoryId`,
`PrioritySequence`, `BatchStatus`, `BinRack` (the bin's name, e.g.
`"RISVHI-BIN"`), `CurrentFullPercentage`, `Quantity`, `PickedQuantity`,
`InventoryTrackingType`, `StockItemId`, `BatchNumber`, `LocationId`.

**Declared on `BinRackStockItem` but NOT populated by this endpoint** (came back
`null` despite the field existing and the surrounding object being fully
populated) - don't rely on these from `GetItemBinracks` specifically, they may
only be populated by a different endpoint (e.g. `Stock.GetBinRacksById`):
`BinRackId`, `StandardType`, `InTransit`, `ExpiresOn`, `SellBy`,
`BinrackTypeName`.

**`AlternateLocations[]` (`StockItemBatch`)**: `BatchId`, `SKU`,
`InventoryTrackingType`, `StockItemId`, `BatchNumber`, `ExpiresOn`, `SellBy`,
`Inventory` (nested list, see below), `IsDeleted`.

**Declared but NOT populated**: `Item` - this duplicates `Inventory` on the same
class (`StockItemBatch.Item : IEnumerable<StockItemBatchInventory>`) and was
`null` while `Inventory` carried the real data. Use `Inventory`, not `Item`.

**`AlternateLocations[].Inventory[]` (`StockItemBatchInventory`)**:
`BatchInventoryId`, `BatchId`, `StockLocationId`, `BinRack`, `PrioritySequence`,
`Quantity`, `StockValue`, `StartQuantity`, `PickedQuantity`, `BatchStatus`,
`IsDeleted`.

**Declared but NOT populated**: `WarehouseBinrackStandardType`,
`WarehouseBinrackTypeName`, `InTransfer`, `BinRackId`, `WarehouseBinrackTypeId`.

**`NonPickableBins[]`**: not independently observed with real data in this test
(came back empty even with `IncludeNonPickLocations = true`) - same
`BinRackStockItem` type as `PickableBins`, but treat its "not populated" list as
unconfirmed rather than assuming it matches `PickableBins` exactly.

## Two things this test surfaced beyond the field shapes

- **A second live confirmation of section 0.1's `Guid.Empty` finding, in a
  different account/context than the original.** The bin data for this item was
  found at the location literally named `"Default"`, whose `StockLocationId` is
  `00000000-0000-0000-0000-000000000000` - i.e. `Guid.Empty`. Same conclusion as
  before, independently reproduced: `Guid.Empty` is a real location's ID, not a
  wildcard.
- **`IsWarehouseManaged` does not reliably predict where binrack data lives.**
  This item's bins were at the non-WMS `"Default"` location - none of this
  account's 6 `IsWarehouseManaged = true` locations had this item's bin data at
  all. Don't assume "only check WMS-flagged locations" when hunting for binrack
  data for a specific item/location pair you don't already know - check the
  location the item is actually allocated at, however that location is flagged.

## Method

Authenticated against a real account (`AuthController.AuthorizeByApplication`),
called `Factory.GetResponse("Picking/GetItemBinracks", ...)` directly for the raw
JSON alongside `Api.Picking.GetItemBinracks(...)` for the typed result, and
diffed the two. Finding a real candidate required scanning `Stock.GetStockItemsFull`
for `isBatchedStockType` items with `Available > 0` (very few existed - most of
this account's items aren't batch-tracked at all) before a specific
already-known StockItemId was supplied to test directly. No credentials were
persisted; the throwaway console project used for this was deleted after the
test.
