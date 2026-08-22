---
title: Customers and Buyers
slug: customers
related_concepts: [open_orders, processed_orders, shipping]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Orders.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Customer.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
  - type: linnworks_llms
    ref: vendor/llms.txt
---

## Purpose

Customer and buyer information in Linnworks is primarily associated with individual orders through `OrderCustomerInfo`,
containing delivery and billing addresses, contact details, and the originating channel buyer username.

Linnworks also exposes CRM and address lookup functionality (`Orders.CustomerLookUp`). However,
order customer information and CRM address-book records should not be treated as the same object, nor
do they constitute a globally stable, unified customer identity. Customer data is Personally Identifiable Information (PII)
and may be missing, masked, or redacted depending on marketplace privacy policies and order processing lifecycle stage.

---

## Architectural Separation: Order Customer Info vs CRM Records

```
Channel Order Ingestion (DeliveryAddress, BillingAddress, ChannelBuyerName, PIIRedactionDays)
        │
        ▼
Linnworks Order (Open or Processed)
        │
        └── OrderCustomerInfo
              ├── Address (Delivery CustomerAddress)
              ├── BillingAddress (Billing CustomerAddress)
              └── ChannelBuyerName (Channel Username)
                    │
                    │ Orders.SetOrderCustomerInfo(saveToCrm = true)
                    ▼
              Shipping/Delivery Address saved to CRM / Address Book
              (Searchable via Orders.CustomerLookUp)
```

---

## Customer Identity and Contact Fields

| Field | Type | Meaning & Constraints |
|---|---|---|
| `Address.FullName` | `string` | Recipient delivery full name. Not a globally unique customer key. |
| `Address.Company` | `string` | Company or business name associated with the delivery address. |
| `Address.EmailAddress` | `string` | Buyer email address. May be a channel-masked proxy email (e.g. Amazon/eBay relay). |
| `Address.PhoneNumber` | `string` | Contact phone number. Required by certain express courier services. |
| `Address.Address1` – `Address3` | `string` | Street address lines. |
| `Address.Town` / `Region` | `string` | City/town and county/state/province. |
| `Address.PostCode` | `string` | Postal code / ZIP code. |
| `Address.Country` | `string` | Destination country name string. |
| `Address.CountryId` | `Guid` (string) | Linnworks system ID for the destination country (internal `CustomerAddress` model). |
| `ChannelBuyerName` | `string` | Buyer username on the originating sales channel (e.g. eBay username). |

> [!WARNING]
> Do not use `FullName`, `EmailAddress`, `PhoneNumber`, or delivery address as a globally unique Linnworks customer identifier. Names and addresses can be shared, and phone/email values can change, be missing, or be masked. `ChannelBuyerName` is channel-specific and must always be evaluated in conjunction with the order's `Source` and `SubSource`.

---

## Important Models

| Model | Description |
|---|---|
| `OrderCustomerInfo` | Internal per-order customer block containing `ChannelBuyerName`, delivery `Address`, and `BillingAddress`. |
| `CustomerAddress` | Internal Linnworks customer/address model containing: `EmailAddress`, `Address1`, `Address2`, `Address3`, `Town`, `Region`, `PostCode`, `Country`, `Continent`, `FullName`, `Company`, `PhoneNumber`, and `CountryId`. |
| Channel Integration `Address` | Incoming channel-order address contract containing: `FullName`, `Company`, `Address1`–`Address3`, `Town`, `Region`, `PostCode`, `Country`, `CountryCode`, `PhoneNumber`, and `EmailAddress`. |
| `Country` | Country reference model returned by `Orders.GetCountries`: `CountryId` (Guid), `CountryName`, `CountryCode` (ISO), `Continent`, `Currency`, `CustomsRequired`, `TaxRate`, `AddressFormat`, `Regions`. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Update complete customer block on an order** | `Orders.SetOrderCustomerInfo` | Sets delivery and billing addresses; optional `saveToCrm` (bool) saves the shipping address into CRM. |
| **Update billing address specifically** | `Orders.UpdateBillingAddress` | Updates only the `BillingAddress` block on an open order. |
| **Search stored CRM address-book records** | `Orders.CustomerLookUp` | Searches stored address records by field string and search text. |
| **Discover standardized country definitions** | `Orders.GetCountries` | Returns recognized country definitions, `CountryId` GUIDs, ISO `CountryCode`, and tax settings. |
| **Create standalone customer (Legacy/SDK)** | `Customer.CreateNewCustomer` | SDK-specific helper — verify target SDK and account compatibility; do not select automatically. |

---

## Common Operations

- `Orders.SetOrderCustomerInfo` — Update the complete buyer delivery and billing record on an open order.
- `Orders.UpdateBillingAddress` — Update the billing address for an open order.
- `Orders.CustomerLookUp` — Search stored address-book records in the CRM by keyword.
- `Orders.GetCountries` — Look up standardized country definitions, ISO codes, and region names.

---

## Gotchas & Operational Rules

### Customer contact fields are not unique identifiers

Do not assume `FullName`, `EmailAddress`, `PhoneNumber`, or postal address uniquely identifies a customer across multiple orders. If correlating repeat buyers in macros:
- Define explicit, account-specific matching rules.
- Combine `ChannelBuyerName` with `Source` and `SubSource`.
- Account for guests, shared household addresses, and changing contact numbers.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### `saveToCrm` saves shipping address, not a unified customer profile

When calling `Orders.SetOrderCustomerInfo`, setting `saveToCrm = true` saves the order's shipping/delivery address into the CRM address book. Do not treat this as creating or updating a globally unified customer profile across channels.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Customer PII may be redacted after order processing

Channel integrations can specify `PIIRedactionDays` (the number of days after order processing when customer PII should be redacted; if null or not supplied, PII is never redacted). Macros operating on historical processed orders must tolerate null, missing, or redacted customer fields.

**Source:** `linnworks_llms` — `vendor/llms.txt`

### Do not log raw customer PII in macro logs

Macro logs visible in the Linnworks UI or execution history should identify orders using `NumOrderId`. Avoid logging full customer names, phone numbers, postal addresses, or email addresses unless explicitly diagnosing errors (in which case values should be partially masked).

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Customer information is order-specific

`OrderCustomerInfo` belongs to the individual order. Modifying customer details on an open order does NOT retroactively update past orders from the same buyer.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Phone requirements are carrier-service specific

While carrier label generation requests supply the customer's phone number, phone presence rules depend on the specific carrier service (e.g. international or express couriers). Do not globally reject orders for missing phone numbers unless the assigned postal service explicitly requires one.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Channel email addresses may be masked or restricted

The `EmailAddress` stored on an order originates from the sales channel and may be a temporary relay/proxy email (e.g. Amazon or eBay relay addresses). Do not assume channel email addresses are permanent customer contact identifiers.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Do not make routing decisions from free-text Country alone

`CustomerAddress` exposes `Country` (string) and `CountryId` (Guid), while the Channel Integration contract provides `CountryCode` (ISO). When implementing international vs domestic routing logic, normalize country values against `Orders.GetCountries` rather than relying on ambiguous free-text string variations (e.g. `"UK"`, `"United Kingdom"`, `"GB"`).

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders carry embedded `OrderCustomerInfo`
- [`processed_orders`](processed_orders.md) — Processed order customer details are subject to PII redaction
- [`shipping`](shipping.md) — Delivery address and destination country determine carrier service availability

---

## Related Workflows

- (Used in address validation, delivery phone checks, and customer data export macros)
