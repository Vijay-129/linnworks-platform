---
title: Shipping and Postal Services
slug: shipping
related_concepts: [open_orders, locations]
related_workflows: [modify_open_orders_by_sku]
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/PostalServices.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/ShippingService.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/postalservices.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
---

## Purpose

The shipping configuration subsystem in Linnworks. It maps carrier services (Royal Mail, DPD,
DHL, FedEx, USPS, etc.) and shipping methods to open orders. It controls carrier selection,
postal service mapping, packaging calculation, shipping quotes, and label generation.

Macros use this subsystem to dynamically reassign shipping services (e.g. upgrade to next-day delivery
if order value > £100, or switch carrier based on weight or destination country).

## Core identifiers

| Identifier | Type | Description |
|---|---|---|
| `PostalServiceId` | `Guid` (string) | Unique ID of a Linnworks postal service definition. |
| `PostalServiceName` | `string` | The user-defined service name (e.g. `Royal Mail Tracked 24`, `DPD Next Day`). |
| `ShippingInfo.PostalServiceId` | `Guid` (string) | The active postal service GUID assigned to an `OpenOrder`. |
| `ShippingInfo.PostalServiceName` | `string` | The active postal service name displayed on the order. |
| `ShippingInfo.TrackingNumber` | `string` | Carrier consignment/tracking number once label is printed. |
| `Vendor` | `string` | The carrier/integration vendor code (e.g. `RoyalMail`, `DPD`). |

## Important models

| Model | Description |
|---|---|
| `PostalService` | Core postal service object: `id` (Guid), `PostalServiceName`, `Vendor`, `ServiceCountry`. |
| `PostalService_WithChannelAndShippingLinks` | Extended postal service model returned by `GetPostalServices`, including linked channels. |
| `OrderShippingInfo` | Shipping information block on an `OpenOrder`: address, service, weight, package dimensions. |
| `ChangeShippingMethodRequest` | Payload to reassign shipping service: `orderIds` (Guid[]), `ShippingMethod` (string). |
| `GetShippingQuoteRequest` | Request payload to query live carrier rates across linked integrations. |

Use `get_model` to see full field lists.

## Common operations

- `PostalServices.GetPostalServices` — Retrieve all available postal services in the account and their linked channels.
- `Orders.GetShippingMethods` — Get list of available shipping method strings for open orders.
- `Orders.ChangeShippingMethod` — Update the shipping method for a list of open orders.
- `ShippingService.GetShippingQuote` — Request real-time rates from carrier integrations for package dimensions/weight.
- `Orders.RecalculateSingleOrderPackaging` — Recalculate package split, dimensions, and weight after item adjustments.
- `Orders.ClearShippingLabelInfo` — Reset label information if an order's shipping service is changed after label printing.

## Lifecycle / State

```
Order Imported (Channel Service assigned)
       ↓
Postal Service Mapping / Macro Evaluation (Assigned Linnworks Postal Service)
       ↓
Packaging Calculation (Weight, Dimensions, Split Packaging)
       ↓
Label Printing (Tracking number generated, Carrier manifest created)
       ↓
Despatch (Order processed, tracking sent to sales channel)
```

## Gotchas

### ChangeShippingMethod cannot run on printed/manifested orders

If a shipping label has already been printed, changing the shipping method will fail or corrupt carrier
manifests. If re-routing a printed order, call `Orders.ClearShippingLabelInfo` or cancel the label first.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### PostalServiceId is distinct from Channel Service Name

The name sent by the channel (e.g. "Standard Shipping") is mapped to an internal Linnworks PostalService.
Macros should inspect and assign internal Linnworks PostalService IDs/Names, not channel strings.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/PostalServices.cs`

### Address modifications require recalculating shipping

If a macro updates customer address, country, or line items on an open order, call
`Orders.RecalculateSingleOrderPackaging` to ensure the package group, weight, and delivery zone update accordingly.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

## Related concepts

- `open_orders` — Shipping info is embedded in `OpenOrder.ShippingInfo`
- `locations` — Carrier availability often depends on the fulfillment stock location

## Related workflows

- `modify_open_orders_by_sku` — Often used to switch shipping methods based on items contained
