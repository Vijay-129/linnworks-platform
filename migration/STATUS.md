# Migration Status

Source of truth for "is this controller rewritten and current." Update this file, not
your memory, whenever a controller is touched. `scripts/sync_api_spec.py` reads the
`Spec Version` column to know what's already been synced.

## Core fix, 2026-08-13

Live-testing Orders surfaced a real bug in `LinnworksAPI/Core/Factory.cs` (present
since the original legacy SDK, ported unchanged): `HttpWebRequest.GetResponse()`
throws `WebException` for any non-2xx response, so the status-code check that parsed
the real Linnworks error message was dead code - every v1 API error surfaced as a
generic ".NET WebException" message instead of the actual server error text. Fixed
by catching the `WebException` and reading the real error body from `ex.Response`.
This affects error messages across **all 27 v1 controllers** (they all share
`Factory.GetResponse`), not just Orders. No behavior change on success paths.

## Full read-only sweep, 2026-08-13

Ran every method across all 27 v1 controllers whose name matches a read-only allow-list
(Get/List/Search/Find/View/Check/Validate/Count/Filter/Is) and doesn't contain a
mutation keyword (Add/Create/Update/Delete/Set/Process/Assign/...) against a real
account, via reflection with default/empty arguments. **411 mutating methods were
never invoked** (by design - this only tests reads). Of the 253 read-only calls made:
- 165 succeeded outright
- 88 were rejected by the server with a real validation message (e.g. "Order not
  found", "must be supplied") - expected, since most were called with empty/default
  IDs rather than real business data; not a defect
- 0 client-side bugs remained after fixing the 2 that were found (below)

Two real client-side bugs found and fixed, both pre-existing in the legacy SDK and
invisible until an actual live response hit them:

1. **`ConfigItem<T>`, `ConfigProperty<T>`, `ConfigPropertySelectionList<T1,T2>`**
   (`Inventory/Models/`) had their generic parameters named after real types
   (`ConfigItem<String>`, `ConfigProperty<Boolean>`) - this shadows the real
   `System.String`/`System.Boolean` inside the class body, so fields that must
   always be a real string/bool regardless of `T` (`PropertyType`, `Loaded`,
   `IsChanged`) silently resolved to `T` instead. Crashed on
   `Inventory.GetChannels()` for any `T` other than the shadowed type. Renamed the
   generic parameters and fixed the field types.
2. **`AnyConfig.Rules` / `HeaderConfig.Rules`** were typed `ConfigRuleCollection` (a
   hand-authored `{Item, Items}` wrapper not in the spec); the real API sends a
   plain JSON array. Changed both to `List<ConfigRule>` and deleted the now-unused
   `ConfigRuleCollection.cs`.
3. **`ActionType` and `DisplayType` enums** (used by `RulesEngine`) were missing
   values the live API actually returns - neither enum is even defined in
   `vendor/PublicApiSpecs/1.0/rulesengine.json`, so there was no spec to catch this
   against. Found the complete real value sets by pulling raw JSON from a live
   `GetActionTypes` call: added `AddItemToOrder`, `AddNoteToOrder`,
   `AddServiceToOrder`, `SetDispatchDate` to `ActionType`, and `Currency`,
   `NumberOfDays`, `Paragraph`, `Percentage`, `Time`, `Timezone`, `Toggle` to
   `DisplayType`. **Neither enum has a spec to verify completeness against - both
   should be re-checked against a live account periodically, not assumed final.**

| Controller | Spec Version | API Version | Last Synced | Status | Notes |
|---|---|---|---|---|---|
| Auth | auth | v1 | 2026-08-20 | done | promoted to LinnworksAPI/V1/Controllers/Auth; builds clean; 2/2 spec endpoints verified. LIVE-TESTED 2026-08-13: AuthorizeByApplication against a real account succeeded (EU locality, session + Status/StateType enum deserialization confirmed working). GetServerUTCTime kept from old SDK but is NOT in the current public spec - confirm with Linnworks before relying on it long-term |
| Customer | customer (reverse-documented) | v1 | 2026-08-13 | done | no PublicApiSpecs file exists; reverse-documented via scripts/reverse_document_controller.py into references/api/v1/Customer.md, then promoted via scripts/port_controller.py; builds clean; 1/1 method verified |
| Dashboards | dashboards | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 7/7 spec endpoints verified, plus 3 legacy extras (ExecuteCustomPagedScript, ExecuteCustomPagedScript_Customer, ExecuteCustomScriptQuery) not in current spec - confirm before relying on long-term |
| Email | email | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 4/4 spec endpoints verified |
| GenericListings | genericlistings | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 12/12 spec endpoints verified |
| ImportExport | importexport | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 11/11 spec endpoints verified |
| Inventory | inventory | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 139/139 spec endpoints verified. LIVE-TESTED 2026-08-13 (full read-only sweep, see above): GetChannels failed initially on a real generic-type-shadowing bug in ConfigItem/ConfigProperty/ConfigPropertySelectionList plus a wrong-type Rules field - both fixed, now returns 24 channels correctly. All other safe reads in this controller passed or got expected server validation errors |
| Listings | listings | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 37/37 spec endpoints verified. CancelListingBulkOperation, CreateTemplatesFromViewInBulk, GetEbayListingOperations were missing from the old SDK entirely - hand-written against references/api/v1/Listings.md (response shape is untyped "object" per spec; not yet tested against a live account). Plus 1 legacy extra (GetEbayListingAudit) not in current spec |
| Locations | locations | v1 | 2026-08-20 | done | promoted to LinnworksAPI/V1/Controllers/Locations; builds clean; 6/6 endpoints verified against references/api/v1/Locations.md. LIVE-TESTED 2026-08-13: GetWarehouseTOTEs against a real account succeeded (8 totes returned, List<WarehouseTOTE> deserialization confirmed) |
| Macro | macro | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 2/2 spec endpoints verified |
| OpenOrders | openorders | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 17/17 spec endpoints verified. LIVE-TESTED 2026-08-13: GetAvailableChannels succeeded (104 channels). GetOpenOrders(ViewId=0) failed with a real server-side error (ViewId=0 isn't a real view) - retried with ViewId=1 and succeeded: 1860 total open orders, 10 returned on the requested page, OrderId/NumOrderId fields correct. Confirms GetOpenOrdersRequest/PostFilterPagedResponse<OpenOrder> round-trip correctly - the earlier failure was bad test input, not an SDK defect |
| OrderPrintStatus | orderprintstatus (reverse-documented) | v1 | 2026-08-13 | done | no PublicApiSpecs file exists; reverse-documented via scripts/reverse_document_controller.py into references/api/v1/OrderPrintStatus.md, then promoted via scripts/port_controller.py; builds clean; 1/1 method verified |
| OrderWorkflow | orderworkflow (reverse-documented) | v1 | 2026-08-13 | done | no PublicApiSpecs file exists; reverse-documented via scripts/reverse_document_controller.py into references/api/v1/OrderWorkflow.md, then promoted via scripts/port_controller.py; builds clean; 12/12 methods verified |
| Orders | orders | v1 | 2026-08-20 | done | promoted to LinnworksAPI/V1/Controllers/Orders (via scripts/port_controller.py); builds clean; 101/101 spec endpoints verified, plus 2 legacy extras (Get_OpenOrderBasicInfoFromItems, MoveToFulfilmentCenter) not in current spec - confirm before relying on long-term. v2 spec also exists (orders-v2.json). LIVE-TESTED 2026-08-13: GetCountries (218 results), GetPaymentMethods (11), GetShippingMethods (8), GetOrderNoteTypes (1), GetAvailableFolders (58) all succeeded against a real account |
| Orders | orders-v2 | v2 | 2026-08-13 | done | written new in LinnworksAPI/V2/Controllers/Orders (no v1 equivalent to port); builds clean; 5/5 spec endpoints implemented. Models generated via scripts/generate_v2_models.py (32 files, recursive $ref resolution) - new V2 Core (ApiContextV2/RestClient/ApiObjectManagerV2, namespace LinnworksAPI.V2) built alongside since v2 needs real REST verbs + JSON bodies, not v1's form-encoded style. GetOrders' oneOf response (GetOrdersResponse\|AnonymousGetOrdersResponse) always deserializes as the named-customer shape - not yet tested against a live account |
| Picking | picking | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 14/14 spec endpoints verified |
| PostSale | postsale | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 2/2 spec endpoints verified |
| PostalServices | postalservices | v1 | 2026-08-20 | done | promoted to LinnworksAPI/V1/Controllers/PostalServices; builds clean; 5/5 endpoints verified. Channel moved to Shared/Common (also used by Inventory, Orders, ProcessedOrders). LIVE-TESTED 2026-08-13: GetPostalServices against a real account succeeded (17 services returned) |
| PrintService | printservice | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 9/9 spec endpoints verified |
| ProcessedOrders | processedorders | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 36/36 spec endpoints verified, plus 1 legacy extra (CreateReturn) not in current spec - confirm before relying on long-term |
| PurchaseOrder | purchaseorder | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 40/40 spec endpoints verified, plus 1 legacy extra (Get_PurchaseOrderItem_OpenOrders) not in current spec - confirm before relying on long-term |
| ReturnsRefunds | returnsrefunds | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 32/32 spec endpoints verified |
| RulesEngine | rulesengine | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 34/34 spec endpoints verified, plus 2 legacy extras (GetValuesFromExisting, GetValuesFromExistingBatch) not in current spec - confirm before relying on long-term. LIVE-TESTED 2026-08-13 (full read-only sweep, see above): GetActionTypes failed initially on missing ActionType/DisplayType enum values (neither enum has a spec to check against) - fixed by pulling the real value set from a live call, now returns all 18 action types correctly |
| Settings | settings | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 7/7 spec endpoints verified |
| ShipStation | shipstation | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 5/5 spec endpoints verified |
| ShippingService | shippingservice | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 5/5 spec endpoints verified |
| Stock | stock | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 51/51 spec endpoints verified, plus 1 legacy extra (Update_StockItemPartial) not in current spec - confirm before relying on long-term |
| WarehouseTransfer | warehousetransfer | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 49/49 spec endpoints verified |
| WarehouseTransfer | warehousetransfer-v2 | v2 | 2026-08-20 | done | this is actually the Amazon FBA inbound shipment API (shipping plans/packing/placement options/boxes/delivery windows), not a v2 of v1's WarehouseTransfer - 45/45 operations generated via scripts/generate_v2_controller.py (new tool, built for this); builds clean; 59 models auto-resolved via $ref. Spec has no operationId on 44/45 ops, so method names were mechanically derived from verb+path (e.g. GetFbaInboundShippingPlansByShippingPlanIdShipments) - NOT official Linnworks names, flagged in the file header; rename once real naming is confirmed. None of this has been tested against a live account |
| Wms | wms | v1 | 2026-08-20 | done | promoted via scripts/port_controller.py; builds clean; 11/11 spec endpoints verified |

## Status values
- `todo` — not yet reviewed against current spec
- `generated` — spec docs exist in references/api/, controller not yet ported
- `in-review` — partially ported; some spec endpoints don't exist in the old SDK and need to be written new, not just copied
- `done` — promoted into `LinnworksAPI/V{1,2}/Controllers/`, matches current spec (legacy extras beyond the spec are noted, not silently dropped)
