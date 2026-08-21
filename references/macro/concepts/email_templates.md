---
title: Email Notifications and Templates
slug: email_templates
related_concepts: [open_orders, processed_orders, customers]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/Email.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/email.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The Linnworks email-template and custom-email subsystem. It exposes configured email templates,
their rendering and tag definitions, sending-account configuration, and APIs for generating
template-based or free-text emails.

Templates are associated with specific Linnworks entity and event contexts depending on their
`TemplateType` (e.g. order events, dispatch notifications, return receipts). Macros utilize this
controller to generate ad-hoc notifications for specific entities or dispatch custom transactional
messages.

---

## Core Identifiers and Template Fields

| Field | Type | Meaning & Constraints |
|---|---|---|
| `pkEmailTemplateRowId` | `int32` | Primary unique row identifier of an email template in Linnworks. |
| `Name` | `string` | Human-readable name of the template (e.g. `UK Express Dispatch Advice`). |
| `TemplateType` | `string` | Entity/event context type. Treat values as account/API-defined strings; do not hardcode an assumed enum without verification. |
| `fkEmailAccountRowId` | `int32` | ID of the specific email account configured to dispatch this template. |
| `ids` | `Guid[]` | Context entity UUIDs passed to `GenerateAdhocEmail`. Interpret IDs according to the template context (e.g. `pkOrderId` for order templates). |

---

## Important Models

| Model | Description |
|---|---|
| `EmailTemplateHeader` | Summary template model returned by `GetEmailTemplates`: `pkEmailTemplateRowId`, `Name`, `TemplateType`, `Enabled`, `AttachPDF`, `IsAdhoc`, `HTML`. |
| `EmailTemplate` | Complete template model returned by `GetEmailTemplate`: `Subject`, `Body`, `HTML`, `Condition`, `AttachPDF`, `TemplateTypeDefinition`, `fkEmailAccountRowId`. |
| `EmailTemplateType` | Template context definition containing tag metadata and PDF attachment availability (`Tags`, `AttachPDFAvailable`, `IsAdhoc`). |
| `TemplateTag` | Tag definition: `Tag` (code), `Name` (display name), `SelectionPath`, `IsList` (true for iteration/list tags). |
| `GenerateAdhocEmailRequest` | Request payload for `GenerateAdhocEmail`: `ids` (`Guid[]`), `templateId` (`int32`), `tags` (`EmailStubCustomTag[]`), `attachments` (`string[]`). |
| `EmailStubCustomTag` | Explicit runtime tag override: `Tag` (string) and `Value` (string). |
| `GenerateAdhocEmailResponse` | Generation outcome: `isComplete` (boolean) and `FailedRecipients` (`string[]`). |
| `GenerateFreeTextEmailRequest` | Free-text custom email payload: `recipient`, `subject`, `body`, `fkEmailAccountRowId`. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Permissions Required | Rate Limit |
|---|---|---|---|
| **List configured email template summaries** | `Email.GetEmailTemplates` | `GlobalPermissions.Email.Templates.GetEmailTemplatesNode` | 150/min |
| **Read full template body, HTML, and tag definitions** | `Email.GetEmailTemplate` | `GlobalPermissions.Email.Templates.GetEmailTemplateNode` | 150/min |
| **Generate template-based email for entity IDs** | `Email.GenerateAdhocEmail` | `GlobalPermissions.Email.SendEmails.SendAdhocEmailsNode` | 150/min |
| **Send a direct free-text email (custom subject/body)** | `Email.GenerateFreeTextEmail` | `GlobalPermissions.Email.SendEmails.SendFreeTextEmailsNode` | 150/min |

---

## Common Operations

- `Email.GetEmailTemplates` — Retrieve all email template headers configured in the account.
- `Email.GetEmailTemplate` — Retrieve the full body, subject, sending account, and available `Tags` schema for `pkEmailTemplateRowId`.
- `Email.GenerateAdhocEmail` — Generate and dispatch emails for a batch of context entity IDs using a specified template.
- `Email.GenerateFreeTextEmail` — Send a direct free-text email without relying on a predefined template.

---

## Template Substitution Tags

Linnworks email templates use substitution tags formatted as `[{Tag}]`.

- **Dynamic Tag Discovery:** Available substitution tags depend on the `TemplateType`. Call `Email.GetEmailTemplate` and inspect `TemplateTypeDefinition.Tags` to discover valid tag codes rather than guessing property paths.
- **List / Iteration Tags:** When `tag.IsList == true`, the tag represents an iterative structure (e.g. order item rows, packaging lines).
- **Custom Tags at Generation:** Callers can pass runtime key-value pairs via `GenerateAdhocEmailRequest.tags` (`EmailStubCustomTag`) to populate custom tags not present in the default database schema.

---

## Gotchas & Operational Rules

### Do not invent template tags — inspect `TemplateTypeDefinition.Tags`

Do not hardcode guessed template tag names. Retrieve the template definition via `Email.GetEmailTemplate(templateId)` and examine `template.TemplateTypeDefinition.Tags`. Tags vary significantly across template types.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### Validate template suitability before generating

Before calling `Email.GenerateAdhocEmail`, verify:
1. `template.Enabled == true`
2. `template.IsAdhoc == true` (confirmed suitable for ad-hoc generation)
3. The context entity IDs in `ids[]` match the template's `TemplateType`
4. The sending email account (`fkEmailAccountRowId`) is configured and active

Do not select templates based on fuzzy name matching alone without checking these flags.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Email.cs` | `macro_convention` — `references/standards/macro_conventions.md`

### `GenerateAdhocEmail.ids` is context-dependent

`GenerateAdhocEmailRequest.ids` accepts a list of UUIDs. The required UUID type is determined by the template's `TemplateType` (e.g. `pkOrderId` for order-scoped templates, RMA/Return IDs for return templates). Do not assume `ids` always expects open order IDs.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### API success does not guarantee final mailbox delivery

`GenerateAdhocEmail` returns `isComplete = true` and a list of `FailedRecipients`. A successful API call confirms the generation and dispatch attempt was executed by the server; it does not guarantee final delivery by external receiving mail exchangers. Inspect `response.FailedRecipients` for immediate transmission failures.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### Marketplace messaging policies and proxy emails

When sending emails to marketplace buyers (e.g. Amazon or eBay orders), sales channels often provide masked proxy emails with strict communication policies. Unsolicited marketing or free-text emails sent via `GenerateFreeTextEmail` to channel proxy addresses can trigger policy violations. Restrict automated customer emails to essential order fulfillment updates.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders serve as the context entity for order-related email templates
- [`processed_orders`](processed_orders.md) — Dispatch advice emails are typically generated for processed orders
- [`customers`](customers.md) — Recipient delivery email addresses originate from customer order data

---

## Related Workflows

- (Used in automated customer notification, exception alerts, and dispatch confirmation macros)
