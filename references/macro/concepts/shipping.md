---
title: Shipping and Postal Services
slug: shipping
related_concepts: [open_orders, locations, rules_engine]
related_workflows: [modify_open_orders_by_sku]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/PostalServices.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ShippingService.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/postalservices.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/shippingservice.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The shipping configuration, carrier integration, and postal routing subsystems in Linnworks.

Linnworks divides shipping operations across three complementary API families:
- **`PostalServices`:** Manages internal postal service definitions, channel-to-service mappings, and courier service linking.
- **`Orders`:** Controls order-level shipping details (`OrderShippingInfo`), shipping service assignment (`Orders.ChangeShippingMethod`), and packaging calculations (`Orders.RecalculateSingleOrderPackaging`).
- **`ShippingService`:** Interfaces with external courier integrations (Royal Mail, DPD, DHL, FedEx, UPS, etc.) for live rate quotes, carrier label generation, consignment cancellation, and end-of-day manifest filing.

Macros use this subsystem to dynamically reassign shipping methods (e.g. upgrading to express when order value > £100, switching couriers based on weight or destination country, or requesting real-time quotes).

---

## Core Identifiers and Fields

| Field | Type | Description |
|---|---|---|
| `pkPostalServiceId` | `Guid` (string) | Unique primary identifier of a Linnworks postal service definition (`PostalService`). |
| `PostalServiceName` | `string` | User-configured service name (e.g. `Royal Mail Tracked 24`, `DPD Next Day`). |
| `ShippingInfo.PostalServiceId` | `Guid` (string) | The active postal service GUID currently assigned to an `OpenOrder`. |
| `ShippingInfo.PostalServiceName` | `string` | The active postal service name displayed on the order. |
| `ShippingInfo.TrackingNumber` | `string` | Carrier tracking/consignment number when generated or assigned for the selected service. |
| `Vendor` | `string` | Courier integration vendor code (e.g. `RoyalMail`, `DPD`, `DHL`). |

---

## Important Models

| Model | Description |
|---|---|
| `PostalService` | Base postal service definition: `pkPostalServiceId`, `PostalServiceName`, `PostalServiceTag`, `PostalServiceCode`, `Vendor`, `ServiceCountry`, `TrackingNumberRequired`, `WeightRequired`, `IgnorePackagingGroup`, `fkShippingAPIConfigId`, `IntegratedServiceId`. |
| `PostalService_WithChannelAndShippingLinks` | Extended model returned by `PostalServices.GetPostalServices`: includes `id`, `Channels` (`ChannelServiceLinks[]`), and `ShippingServices` mapping. |
| `OrderShippingInfo` | Shipping block on `OpenOrder`: `PostalServiceId`, `PostalServiceName`, `Vendor`, `TotalWeight`, `ItemWeight`, `PackageCategory`, `PackageType`, `PostageCost`, `TrackingNumber`, `ManualAdjust`. |
| `Orders_ChangeShippingMethodRequest` | Payload for `Orders.ChangeShippingMethod`: `orderIds` (`Guid[]`), `shippingMethod` (`string`). |
| `GetShippingQuoteRequest` | Payload for `ShippingService.GetShippingQuote`: queries live carrier rates for `pkOrderId` across configured `accounts` (`string[]`). |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics | Rate Limit |
|---|---|---|---|
| **Retrieve all configured postal services** | `PostalServices.GetPostalServices` | Returns postal services with linked channel and courier integration data. | 150/min |
| **Inspect channel links for a postal service** | `PostalServices.GetChannelLinks` | Returns mapped marketplace shipping tags for `postalServiceId`. | 150/min |
| **Get available shipping method names** | `Orders.GetShippingMethods` | Returns list of shipping service name strings configured on the account. | 250/min |
| **Change shipping service on open orders** | `Orders.ChangeShippingMethod` | Assigns shipping service by name (`shippingMethod`) to a batch of `orderIds`. | 250/min |
| **Discover configured carrier integrations** | `ShippingService.GetIntegrations` | Lists active carrier accounts/configs needed before requesting quotes. | 150/min |
| **Query live carrier shipping quotes** | `ShippingService.GetShippingQuote` | Requests live quotes for an existing `pkOrderId` across integration accounts. | 150/min |
| **Apply chosen quote to order** | `ShippingService.SetShippingMethodFromQuote` | Sets the selected quoted service on the open order. | 150/min |
| **Recalculate package split, dimensions, weight** | `Orders.RecalculateSingleOrderPackaging` | Recalculates packaging weights, dimensions, and split packages. | 250/min |
| **Cancel carrier shipping label consignment** | `ShippingService.CancelOrderShippingLabel` | Cancels the active carrier consignment/label with the external courier. | 150/min |
| **Clear local label fields on order** | `Orders.ClearShippingLabelInfo` | Clears Linnworks-side label fields (does not cancel carrier consignment). | 250/min |
| **Retrieve filed carrier manifests** | `ShippingService.GetFiledManifestsByVendor` | Paged query of filed manifests by vendor between date ranges. | 150/min |

---

## Shipping Lifecycle Flow

```
Channel Order Imported
        │  (Channel shipping method/tag attached)
        ▼
Postal-Service Mapping / Rules Engine
        │  (Linnworks postal service selected and assigned)
        ▼
Packaging Calculation (Orders.RecalculateSingleOrderPackaging)
        │  (Item weight + packaging weight + dimensions + split packaging)
        ▼
Optional Live Shipping Quote
        │  (ShippingService.GetIntegrations → GetShippingQuote → SetShippingMethodFromQuote)
        ▼
Shipping Label Generation
        │  (Carrier consignment created + tracking number generated where supported)
        ▼
Order Processing / Despatch (Orders.ProcessOrder)
        │  (Despatch notification + tracking sent to sales channel)
        ▼
Manifest Filing (where required by courier/integration)
        │  (Consignments filed in batch with carrier for collection)
        ▼
Carrier Collection
```

---

## Gotchas & Operational Rules

### `pkPostalServiceId` vs `ShippingInfo.PostalServiceId`

Distinguish postal-service definition identifiers from order assignments:
- `pkPostalServiceId` (Guid) identifies the postal service record in `PostalService` and `PostalServices` configuration APIs.
- `ShippingInfo.PostalServiceId` (Guid) identifies the active postal service assigned to an `OpenOrder`.
- `Orders.ChangeShippingMethod` requires the string **`shippingMethod`** (the `PostalServiceName`), not a GUID.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/PostalServices.cs` | `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Channel shipping tag ≠ Linnworks postal service name

Sales channels transmit external shipping tags (e.g. `"Expedited"`, `"Standard"`).
- Linnworks maps these channel tags to internal postal services via `PostalServices.GetChannelLinks`.
- When programmatically updating shipping services via `Orders.ChangeShippingMethod`, always supply the internal Linnworks postal service name, not the raw channel string.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/PostalServices.cs`

### Changing shipping after label generation requires label cleanup

If a shipping label has already been generated for an order:
- Do not simply change the shipping service with `Orders.ChangeShippingMethod` while leaving an active carrier label intact.
- Cancel the external carrier consignment first using **`ShippingService.CancelOrderShippingLabel`**.
- Clear the local Linnworks label data with **`Orders.ClearShippingLabelInfo`** before assigning a new service and generating fresh labels.
- Note: Calling `Orders.ClearShippingLabelInfo` alone resets local order metadata but does **not** cancel the carrier consignment with the external courier.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/ShippingService.cs` | `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Label generation and manifest filing are distinct lifecycle events

Label printing creates individual carrier consignments and tracking numbers. Manifest filing occurs subsequently at end-of-day when batches of consignments are formally filed with the courier. Do not assume manifest filing happens automatically upon label creation.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/shippingservice.json`

### Recalculate packaging after item or packaging modifications

If a macro changes line items, quantities, item dimensions, or individual packaging assignments, call **`Orders.RecalculateSingleOrderPackaging`** before requesting quotes or generating labels.
- If the customer delivery address or country changes, separately re-evaluate shipping-service selection or Rules Engine logic that depends on destination zones.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### `ManualAdjust` suppresses automatic packaging recalculation

If an operator or workflow has manually overridden package dimensions or weights, `OrderShippingInfo.ManualAdjust` will be set to `true`. In this state, automatic recalculation routines will respect the manual override and will not replace the specified values.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Orders.cs`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Shipping details are embedded in `OpenOrder.ShippingInfo`
- [`locations`](locations.md) — Carrier service availability is scoped to fulfillment stock locations
- [`rules_engine`](rules_engine.md) — Automated shipping service assignment based on weight, value, and destination

---

## Related Workflows

- [`modify_open_orders_by_sku`](../workflows/modify_open_orders_by_sku.md) — Can reassign shipping methods or trigger packaging recalculations
