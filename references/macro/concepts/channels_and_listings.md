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
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/inventory.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/genericlistings.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/listings.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: linnworks_llms
    ref: vendor/llms.txt
---

## Purpose

The multichannel sales channel integration and listing management subsystem in Linnworks.

This subsystem manages two distinct functional areas:
1. **Channel Mapping & Synchronization:** Linking Linnworks catalog inventory items to external channel listings, routing order items to catalog SKUs, and pushing change-driven price and stock updates to marketplaces.
2. **Listing Creation & Templates:** Generating new listings on sales channels using Configurators, Listing Templates, and Processing Queues (via the Generic Listing Tool or channel-specific listing APIs).

---

## Architectural Separation: Mapping vs Synchronization vs Listing

```
Sales Channel Connection (Source / SubSource)
      │
      ├── 1. Product Mapping (Inventory Controller)
      │      Linnworks StockItem (SKU) ◄───► Channel SKU + Reference
      │
      ├── 2. Stock / Price Sync (Change-Driven Updates)
      │      Calculated Channel Quantity & Pricing pushed on deltas
      │
      ├── 3. Order Ingestion (Orders Controller)
      │      Incoming ChannelSKU matched to internal StockItem
      │
      └── 4. Listing Management (GenericListings / Listings Controllers)
             Configurator (Category & Attributes)
                 ↓
             Listing Template (Item Data + Pricing)
                 ↓
             Process / Push (Publish / Relist / Update)
                 ↓
             External Marketplace Listing (ExternalListingId / Reference)
```

---

## Core Identifiers

| Identifier | Type | Meaning |
|---|---|---|
| `StockItemId` | `Guid` (string) | Linnworks catalog inventory item primary key. |
| `SKU` | `string` | Canonical internal Linnworks catalog SKU. |
| `Source` | `string` | Linnworks channel/integration platform identifier (e.g. `AMAZON`, `EBAY`, `SHOPIFY`). |
| `SubSource` | `string` | Specific store/account instance within that channel (e.g. `Amazon UK`, `US_Shopify`). |
| `ChannelSKU` | `string` | The seller/merchant SKU as represented on the external channel (e.g. `AMZ-RED-TSHIRT-M`). |
| `channelSKURowId` | `Guid` (string) | Unique ID of a specific channel SKU mapping record in Linnworks. |
| `Reference` / `ExternalListingId` | `string` | Marketplace-assigned product/listing identifier (e.g. Amazon ASIN `B0ABCDE123` or eBay Item ID `123456789012`). |
| `ConfiguratorId` | `int32` | Identifier of a listing configurator hosting category rules and channel attributes. |
| `TemplateId` | `int32` | Identifier of an individual listing template. |

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Discover all configured sales channels** | `Inventory.GetChannels` | Returns active channel configurations and sub-sources across the account. |
| **Find channel accounts for a specific platform** | `Inventory.GetChannelsBySource` | Returns configured store accounts for a given `source` (e.g. `AMAZON`). |
| **Read channel SKU links for one stock item** | `Inventory.GetInventoryItemChannelSKUs` | Returns all active channel links (`StockItemChannelSKU[]`) for a `StockItemId`. |
| **Batch read channel SKU links for multiple items** | `Inventory.BatchGetInventoryItemChannelSKUs` | Retrieves channel SKU mappings for a list of `inventoryItemIds`. |
| **Link a catalog item to a channel SKU** | `Inventory.CreateChannelMapping` | Establishes a link between `StockItemId` and channel `Source`, `SubSource`, and `ChannelSKU`. |
| **Update mapping properties or prices** | `Inventory.UpdateChannelMapping` | Updates channel-specific price or title overrides on an existing mapping. |
| **Unlink an inventory item from a channel listing** | `Inventory.UnlinkChannelListing` | Removes mapping by `channelRefId`, `source`, and `subSource`. |
| **Read GLT listing configurators** | `GenericListings.GetConfiguratorsInfoPaged` | Paged query for Generic Listing Tool configurator configurations. |
| **Generate generic listing templates** | `GenericListings.CreateTemplates` | Constructs listing templates from catalog stock items and configurator rules. |
| **Push / Update / Relist / Delete GLT listings** | `GenericListings.ProcessTemplates` | Transmits templates to the channel integration for execution. |
| **Create channel-specific configurators (eBay/Magento)** | `Listings.CreateeBayConfigurators` / `Listings.CreateBigcommerceConfigurators` | Channel-specific configurator endpoints for integrations not using GLT. |

---

## Common Operations

- `Inventory.GetChannels` — Discover active channel integrations and sub-sources.
- `Inventory.GetInventoryItemChannelSKUs` — Inspect marketplace links for a product.
- `Inventory.CreateChannelMapping` — Map a new channel SKU to an internal catalog item.
- `Inventory.UnlinkChannelListing` — Break a link between a channel listing and an inventory item.
- `GenericListings.CreateTemplates` — Build draft listing templates for supported generic channels.
- `GenericListings.ProcessTemplates` — Submit listing templates to channels for creation, update, or deletion.

---

## Gotchas & Operational Rules

### `ChannelSKU` vs `Linnworks SKU` vs `External Listing Reference`

Do not confuse seller SKUs with marketplace product identifiers:
- **Linnworks SKU (`SKU`):** Internal catalog identifier (e.g. `LW-RED-TSHIRT-M`).
- **Channel SKU (`ChannelSKU`):** Seller SKU on the marketplace (e.g. `AMZ-RED-TSHIRT-M`).
- **External Listing Reference (`Reference` / `ExternalListingId`):** Marketplace-assigned product ID (e.g. Amazon ASIN `B0ABCDE123` or eBay Item ID `123456789012`).

A single Linnworks stock item can link to multiple distinct `ChannelSKU` values across different `Source` / `SubSource` accounts.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Inventory.cs` | `vendor/PublicApiSpecs/1.0/inventory.json`

### Generic Listing Tool (GLT) is not universal across all channels

Do not assume `GenericListings/*` applies to every marketplace. Supported generic channels use GLT, whereas platforms like eBay, Magento, and BigCommerce historically utilize channel-specific listing APIs (`Listings.CreateeBayConfigurators`, `Listings.CreateBigcommerceConfigurators`, `Listings.ProcessEbayListing`).

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Listings.cs` | `vendor/LinnworksNetSDK/Controllers/GenericListings.cs`

### Do not hardcode `Source` / `SubSource` strings

`Source` and `SubSource` represent user-configured integrations (e.g. `Amazon UK`, `MyShopifyStore`). Never hardcode assumed sub-source names in macro code. Discover real values dynamically via `Inventory.GetChannels()` or accept them as configurable macro parameters.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Channel quantity is not necessarily raw physical stock

The stock quantity Linnworks transmits to a sales channel is calculated from available inventory and channel rules:
- Minus open order allocations (`InOrderBook`)
- Capped by **Max Listed Quantity** rules
- Adjusted by percentage allocation rules
- Suppressed by **End When** threshold settings

Do not assume the quantity submitted to a channel will match the raw physical stock level.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/inventory.json`

### External-channel price and stock updates are change-driven

For connected sales channels, Linnworks synchronisation services dispatch price and inventory updates as deltas (changed items only) rather than rewriting complete catalog feeds on every cycle. Do not generalize this to initial bulk imports or marketplace native catalog reconciliations.

**Source:** `linnworks_llms` — `vendor/llms.txt`

### Do not assume all downloaded channel order items are mapped

When orders are imported from channels, Linnworks attempts to link incoming `ChannelSKU` items to internal stock items. Unmapped lines can exist if a listing was created directly on the marketplace without a corresponding mapping. Use `Orders.UpdateLinkItem` or `Orders.CreateNewItemAndLink` when resolving unmapped lines.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

---

## Related Concepts

- [`inventory`](inventory.md) — Product catalog hosting stock levels and channel mapping links
- [`order_items`](order_items.md) — Order lines capture both the internal `SKU` and the marketplace `ChannelSKU`
- [`open_orders`](open_orders.md) — Open orders carry `Source` and `SubSource` channel provenance

---

## Related Workflows

- (Used in multichannel price updates, automated listing creation, and catalog sync macros)
