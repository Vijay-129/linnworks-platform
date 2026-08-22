---
title: Sales Channels and Product Listings
slug: channels_and_listings
related_concepts: [inventory, order_items, open_orders]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Inventory.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/GenericListings.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Listings.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/genericlistings.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/listings.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
  - type: linnworks_llms
    ref: vendor/llms.txt
---

## Purpose

The multichannel sales channel integration and listing management subsystem in Linnworks.

This subsystem manages four distinct functional areas:
1. **Catalog Mapping:** Linking Linnworks catalog inventory items to external channel listings and seller SKUs.
2. **Inventory & Price Synchronization:** Pushing change-driven stock calculations and price updates to connected marketplaces.
3. **Order Ingestion & Linking:** Downloading orders and resolving incoming channel line items to internal inventory records.
4. **Listing Creation & Management:** Generating and publishing new listings using Configurators and Listing Templates (via the Generic Listing Tool or dedicated channel-specific listing APIs).

---

## Architectural Separation

```
Channel Connection (Source + SubSource)
        │
        ├── 1. Catalog Mapping (Inventory Controller)
        │     StockItemId / SKU ◄──► ChannelSKU + Reference
        │
        ├── 2. Inventory / Price Synchronization (Change-Driven Updates)
        │     Linnworks calculates channel quantity
        │     → changed inventory/price sent to channel integration
        │
        ├── 3. Order Ingestion & Line Linking (Orders Controller)
        │     Downloaded order line (ChannelSKU)
        │     → mapped/linked StockItem where possible
        │     → unmapped lines resolved via Orders.UpdateLinkItem
        │
        └── 4. Listing Management (GenericListings / Listings Controllers)
              │
              ├── GLT-Supported Channels
              │     Configurator
              │         ↓
              │     CreateTemplates
              │         ↓
              │     SaveTemplateFields (optional field adjustments)
              │         ↓
              │     ProcessTemplates (Publish / Update / Relist / Delete)
              │
              └── eBay / Magento / BigCommerce
                    Channel-specific Listings configurators and templates
```

---

## Core Identifiers

| Identifier | Type | Meaning |
|---|---|---|
| `StockItemId` | `Guid` (string) | Linnworks catalog inventory item primary key. |
| `SKU` | `string` | Canonical internal Linnworks catalog SKU. |
| `Source` | `string` | Channel/integration platform identifier (e.g. `AMAZON`, `EBAY`, `SHOPIFY`). |
| `SubSource` | `string` | Specific store/account instance within that channel (e.g. `Amazon UK`, `US_Shopify`). |
| `ChannelSKU` | `string` | Seller/merchant SKU as represented on the external channel (e.g. `AMZ-RED-TSHIRT-M`). |
| `channelSKURowId` | `Guid` (string) | Unique ID of a specific channel SKU mapping record in Linnworks. |
| `Reference` | `string` | Channel integration mapping/product reference; semantics are integration-specific. |
| `ExternalListingId` | `model/channel-specific` | External marketplace listing identifier used in listing-management workflows. |
| `ConfiguratorId` | `int32` | Identifier of a listing configurator hosting category rules and channel attributes. |
| `TemplateId` | `int32` | Identifier of an individual listing template. |

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Discover all configured sales channels** | `Inventory.GetChannels` | Returns configured channel records across the account; inspect `Enabled` property for active status. |
| **Find channel accounts for a specific platform** | `Inventory.GetChannelsBySource` | Returns configured store accounts for a given `source` (e.g. `AMAZON`). |
| **Read channel SKU links for one stock item** | `Inventory.GetInventoryItemChannelSKUs` | Returns all channel links (`StockItemChannelSKU[]`) for a `StockItemId`. |
| **Batch read channel SKU links for multiple items** | `Inventory.BatchGetInventoryItemChannelSKUs` | Retrieves channel SKU mappings for a list of `inventoryItemIds`. |
| **Link a catalog item to a channel SKU** | `Inventory.CreateChannelMapping` | Establishes a link between `StockItemId` and channel `Source`, `SubSource`, and `ChannelSKU`. |
| **Update mapping properties** | `Inventory.UpdateChannelMapping` | Updates existing channel mapping settings (e.g. `MaxListedQuantity`) supported by request model. |
| **Delete channel SKU mapping records** | `Inventory.DeleteInventoryItemChannelSKUs` | Deletes channel SKU mappings by a list of `inventoryItemChannelSKUIds` (`Guid[]`). |
| **Unlink an inventory item from a channel listing** | `Inventory.UnlinkChannelListing` | Removes mapping by `channelRefId`, `source`, and `subSource`. |
| **Read GLT listing configurators** | `GenericListings.GetConfiguratorsInfoPaged` | Paged query for Generic Listing Tool configurators. |
| **Generate generic listing templates** | `GenericListings.CreateTemplates` | Constructs listing templates from catalog stock items and configurator rules. |
| **Retrieve GLT templates by stock item IDs** | `GenericListings.OpenTemplatesByInventory` | Loads existing generic listing templates for specified inventory items. |
| **Modify generic template fields** | `GenericListings.SaveTemplateFields` | Updates fields on draft generic templates prior to publishing. |
| **Push / Update / Relist / Delete GLT listings** | `GenericListings.ProcessTemplates` | Transmits templates to the channel integration for execution. |
| **Create channel-specific configurators** | `Listings.CreateeBayConfigurators` / `Listings.CreateBigcommerceConfigurators` | Channel-specific configurator endpoints for integrations not using GLT. |
| **Generate channel-specific templates** | `Listings.CreateEbayTemplates` / `Listings.CreateBigcommerceTemplates` | Constructs templates for eBay and BigCommerce integrations. |
| **Process channel-specific listings** | `Listings.ProcesseBayListings` / `Listings.ProcessBigcommerceListings` | Pushes channel-specific templates to marketplace APIs. |
| **Link unmapped downloaded order line** | `Orders.UpdateLinkItem` / `Orders.CreateNewItemAndLink` | Links incoming channel order line item to an internal inventory record. |

---

## Gotchas & Operational Rules

### `ChannelSKU` vs `Linnworks SKU` vs `Reference` vs `ExternalListingId`

Do not confuse seller SKUs with marketplace product identifiers:
- **Linnworks SKU (`SKU`):** Internal catalog identifier (e.g. `LW-RED-TSHIRT-M`).
- **Channel SKU (`ChannelSKU`):** Seller SKU on the marketplace (e.g. `AMZ-RED-TSHIRT-M`).
- **Reference (`Reference`):** Product/mapping reference used by channel integration sync endpoints.
- **External Listing Reference (`ExternalListingId`):** Marketplace-assigned listing identifier used in listing management.

A single Linnworks stock item can link to multiple distinct `ChannelSKU` values across different `Source` / `SubSource` accounts.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Inventory.cs` | `public_api_spec` — `vendor/PublicApiSpecs/1.0/inventory.json`

### Generic Listing Tool (GLT) is not universal across all channels

Do not assume `GenericListings/*` applies to every marketplace. Supported generic channels use GLT, whereas platforms like eBay, Magento, and BigCommerce utilize dedicated channel-specific listing APIs (`Listings.CreateeBayConfigurators`, `Listings.CreateBigcommerceConfigurators`, `Listings.ProcesseBayListings`, `Listings.ProcessBigcommerceListings`).

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Listings.cs` | `vendor/LinnworksNetSDK/Controllers/GenericListings.cs`

### Do not hardcode `Source` / `SubSource` strings

`Source` and `SubSource` represent user-configured integrations (e.g. `Amazon UK`, `MyShopifyStore`). Never hardcode assumed sub-source names in macro code. Discover real values dynamically via `Inventory.GetChannels()` and inspect the `Enabled` property.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Channel quantity is not necessarily raw physical stock

The stock quantity Linnworks transmits to a sales channel is calculated from available inventory and channel configuration rules:
- Adjusted for open-order demand/allocation according to Linnworks channel-inventory calculation
- Capped by **Max Listed Quantity** rules
- Adjusted by percentage allocation rules
- Suppressed by **End When** threshold settings

Do not assume the quantity submitted to a channel will match raw physical stock on hand.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/inventory.json`

### Channel-integration inventory and price updates are change-driven

For Linnworks Channel Integration inventory and price update endpoints, Linnworks sends relevant changed products (deltas) rather than resending the entire catalog feed on every synchronization cycle.

**Source:** `linnworks_llms` — `vendor/llms.txt`

### Product mapping and order-item linking are distinct operations

- **Catalog Mapping (`Inventory.CreateChannelMapping`):** Establishes the persistent catalog-level link between a `StockItemId` and a marketplace `ChannelSKU`.
- **Order Line Linking (`Orders.UpdateLinkItem`):** Resolves unmapped lines on downloaded orders where an incoming channel item has no established catalog link.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json` | `vendor/PublicApiSpecs/1.0/inventory.json`

---

## Related Concepts

- [`inventory`](inventory.md) — Product catalog hosting stock levels and channel mapping links
- [`order_items`](order_items.md) — Order lines capture both the internal `SKU` and the marketplace `ChannelSKU`
- [`open_orders`](open_orders.md) — Open orders carry `Source` and `SubSource` channel provenance

---

## Related Workflows

- (Used in multichannel price updates, automated listing creation, and catalog sync macros)
