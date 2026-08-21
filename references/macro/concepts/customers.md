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
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/customer.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

Customer information in Linnworks is primarily associated with orders through `OrderCustomerInfo`,
containing delivery and billing addresses, contact details, and the originating channel buyer username.

Linnworks also exposes CRM and address lookup functionality (`Orders.CustomerLookUp`). However,
order customer information and CRM address-book records should not be treated as the same object, nor
do they constitute a globally stable, unified customer identity. Customer data is PII and may be missing,
masked, or redacted depending on the channel and order lifecycle stage.

---

## Architectural Separation: Order Customer Info vs CRM Records

```
Order (Open or Processed)
  │
  └── OrderCustomerInfo
        ├── Address (Delivery CustomerAddress)
        ├── BillingAddress (Billing CustomerAddress)
        └── ChannelBuyerName (Channel Username)
              │
              │  (Optional: saveToCrm = true)
              ▼
    Linnworks CRM / Address Book
    (Searchable via Orders.CustomerLookUp)
```

---

## Customer Identity and Contact Fields

| Field | Type | Meaning & Constraints |
|---|---|---|
| `Address.FullName` | `string` | Recipient full delivery name. Not a unique customer key. |
| `Address.Company` | `string` | Company or business name associated with the delivery address. |
| `Address.EmailAddress` | `string` | Buyer email address. May be a channel-masked proxy email. |
| `Address.PhoneNumber` | `string` | Contact phone number. Required by certain carrier services. |
| `Address.Address1` – `Address3` | `string` | Street address lines. |
| `Address.Town` / `Region` | `string` | City/town and county/state/province. |
| `Address.PostCode` | `string` | Postal code / ZIP code. |
| `Address.Country` | `string` | Destination country name string. |
| `Address.CountryId` | `Guid` (string) | Linnworks system ID for the destination country. |
| `ChannelBuyerName` | `string` | Buyer username on the originating sales channel (e.g. eBay username). |

> [!WARNING]
> Do not use `FullName`, `EmailAddress`, `PhoneNumber`, or delivery address as a globally unique Linnworks customer identifier. Names and addresses can be shared, and phone/email values can change, be missing, or be masked. `ChannelBuyerName` is channel-specific and must always be evaluated in conjunction with the order's `Source` and `SubSource`.

---

## Important Models

| Model | Description |
|---|---|
| `OrderCustomerInfo` | Per-order customer block containing `ChannelBuyerName`, delivery `Address`, and `BillingAddress`. |
| `CustomerAddress` | Standardized address/contact model containing: `EmailAddress`, `Address1`, `Address2`, `Address3`, `Town`, `Region`, `PostCode`, `Country`, `Continent`, `FullName`, `Company`, `PhoneNumber`, and `CountryId`. |
| `Country` | Standardized Linnworks country reference record containing `CountryId`, `CountryName`, and ISO codes. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Rationale & Semantics |
|---|---|---|
| **Update complete customer block on an order** | `Orders.SetOrderCustomerInfo` | Sets delivery and billing addresses; optional `saveToCrm` (bool) persists address to CRM. |
| **Update billing address specifically** | `Orders.UpdateBillingAddress` | Updates only the `BillingAddress` block on an open order. |
| **Search stored CRM customer addresses** | `Orders.CustomerLookUp` | Searches stored address records by field (`NAME`, `EMAIL`, `POSTCODE`) and search string. |
| **Discover standardized country definitions** | `Orders.GetCountries` | Returns recognized country definitions, `CountryId` GUIDs, and ISO codes. |
| **Create standalone customer (SDK/Legacy)** | `Customer.CreateNewCustomer` | SDK-specific helper — verify availability against target account/API version. |

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

### Customer PII may be redacted post-despatch

Do not assume customer name, address, email, or phone data remains available indefinitely on historical orders. Channel integrations can configure `PIIRedactionDays`, after which personal data is scrubbed. Macros operating on historical processed orders must tolerate null or redacted customer fields.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Do not log raw customer PII in macro logs

Macro logs visible in the Linnworks UI or execution history should identify orders using `NumOrderId`. Avoid logging full customer names, phone numbers, postal addresses, or email addresses unless explicitly diagnosing errors (in which case values should be partially masked).

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

### Customer information is order-specific

`OrderCustomerInfo` belongs to the individual order. Modifying customer details on an open order does NOT retroactively update past orders from the same customer. To persist updated shipping details into the account's address book, pass `saveToCrm = true` when calling `Orders.SetOrderCustomerInfo`.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Phone requirements are shipping-service specific

While carrier label generation requests supply the customer's phone number, requirement rules vary by carrier and service. Validate phone number presence when required by specific express carrier profiles rather than applying a universal constraint across all orders.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Channel email addresses may be masked or restricted

The `EmailAddress` stored on an order originates from the sales channel and may be a temporary relay/proxy email. Do not assume channel email addresses are permanent or suitable for external marketing/communication.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Do not make shipping decisions from free-text Country alone

`CustomerAddress` exposes both `Country` (string) and `CountryId` (Guid), while carrier integrations often evaluate ISO country codes. When implementing international vs domestic routing logic, normalize country values against `Orders.GetCountries` rather than relying on ambiguous free-text string variations (e.g. "UK", "United Kingdom", "GB").

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders carry embedded `OrderCustomerInfo`
- [`processed_orders`](processed_orders.md) — Processed order customer details are subject to PII redaction
- [`shipping`](shipping.md) — Delivery address and destination country determine carrier service availability

---

## Related Workflows

- (Used in address validation, delivery phone checks, and customer data export macros)
