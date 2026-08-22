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
their rendering and tag definitions, and references to configured sending email accounts, providing
APIs for generating template-based or free-text transactional emails.

Templates are associated with Linnworks rendering contexts through their `TemplateType`, which determines
applicable database fields and tag definitions. Macros utilize this controller to generate ad-hoc notifications
for specific context entity IDs or dispatch custom transactional messages.

---

## Core Identifiers and Template Fields

| Field | Type | Meaning & Constraints |
|---|---|---|
| `pkEmailTemplateRowId` | `int32` | Primary unique row identifier of an email template in Linnworks. |
| `Name` | `string` | Human-readable name of the template (e.g. `UK Express Dispatch Advice`). |
| `TemplateType` | `string` | Entity/event context type. Treat values as account/API-defined strings; do not hardcode an assumed enum without verification. |
| `fkEmailAccountRowId` | `int32` | Identifier of the configured sending email account referenced by the template. |
| `ids` | `Guid[]` | Context entity UUIDs passed to `GenerateAdhocEmail` or `GenerateFreeTextEmail`. Entity type is determined by `TemplateType`. |

---

## Important Models

| Model | Description |
|---|---|
| `EmailTemplateHeader` | Summary template model returned by `GetEmailTemplates`: `pkEmailTemplateRowId`, `Name`, `TemplateType`, `Enabled`, `fkEmailAccountRowId`, `AttachPDF`, `IsAdhoc`, `HTML`, `AccountName`. |
| `EmailTemplate` | Full template model returned by `GetEmailTemplate`: `pkEmailTemplateRowId`, `Name`, `Subject`, `Body`, `HTML`, `Condition`, `AttachPDF`, `fkEmailAccountRowId`, `TemplateTypeDefinition`. |
| `EmailTemplateType` | Template context definition containing rendering metadata: `Type`, `Name`, `IsAdhoc`, `Tags` (`TemplateTag[]`), `AttachPDFAvailable`. |
| `TemplateTag` | Tag definition: `Tag` (code), `Name` (display name), `SelectionPath`, `IsList` (true for iteration/list tags). |
| `GenerateAdhocEmailRequest` | Request payload for `GenerateAdhocEmail`: `ids` (`Guid[]`), `templateId` (`int32`), `tags` (`EmailStubCustomTag[]`), `attachments` (`string[]`). |
| `EmailStubCustomTag` | Runtime custom tag override: `TagName` (`string`), `TagValue` (`string`), `pkEmailStubTagId` (`int32`), `fkEmailStubId` (`int32`). |
| `GenerateAdhocEmailResponse` | Generation outcome: `isComplete` (`boolean`) and `FailedRecipients` (`string[]`). |
| `GenerateFreeTextEmailRequest` | Free-text custom email payload: `ids` (`Guid[]`), `subject` (`string`), `body` (`string`), `templateType` (`string`). |
| `GenerateFreeTextEmailResponse` | Free-text send result: `isComplete` (`boolean`) and `FailedRecipients` (`string[]`). |

> [!NOTE]
> **SDK vs. OpenAPI Wrapper:**
> SDK callers interact with `GenerateAdhocEmailRequest` directly. Public OpenAPI / raw HTTP clients wrap this payload inside `Email_GenerateAdhocEmailRequest { request: GenerateAdhocEmailRequest }`.

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Permissions Required | Rate Limit |
|---|---|---|---|
| **List configured email template summaries** | `Email.GetEmailTemplates` | `GlobalPermissions.Email.Templates.GetEmailTemplatesNode` | 150/min |
| **Read full template body, HTML, and tag definitions** | `Email.GetEmailTemplate` | `GlobalPermissions.Email.Templates.GetEmailTemplateNode` | 150/min |
| **Generate template-based email for entity IDs** | `Email.GenerateAdhocEmail` | `GlobalPermissions.Email.SendEmails.SendAdhocEmailsNode` | 150/min |
| **Send free-text email for entity context IDs** | `Email.GenerateFreeTextEmail` | `GlobalPermissions.Email.SendEmails.SendFreeTextEmailsNode` | 150/min |

---

## Common Operations

- `Email.GetEmailTemplates` — Retrieve all email template headers configured in the account.
- `Email.GetEmailTemplate` — Retrieve the full body, subject, sending account reference, and available `Tags` schema for `pkEmailTemplateRowId`.
- `Email.GenerateAdhocEmail` — Generate and dispatch emails for a batch of context entity IDs using a specified template ID.
- `Email.GenerateFreeTextEmail` — Generate and send custom free-text emails for context entity IDs using a specified `templateType` without requiring a saved template ID.

---

## Template Substitution Tags

Linnworks email templates use substitution tags formatted as `[{Tag}]`.

- **Dynamic Tag Discovery:** Available substitution tags depend on the `TemplateType`. Call `Email.GetEmailTemplate` and inspect `TemplateTypeDefinition.Tags` to discover valid tag codes rather than guessing property paths.
- **List / Iteration Tags:** When `tag.IsList == true`, the tag represents an iterative structure (e.g. `FOREACH(OrderItems) {BEGIN} [{ORDERITEMS.ITEMNUMBER}] {END}`).
- **Custom Tags at Generation:** Callers can pass runtime key-value overrides via `GenerateAdhocEmailRequest.tags` using `EmailStubCustomTag` (`TagName` and `TagValue`).

---

## Gotchas & Operational Rules

### Do not invent template tags — inspect `TemplateTypeDefinition.Tags`

Do not hardcode guessed template tag names. Retrieve the template definition via `Email.GetEmailTemplate(templateId)` and examine `template.TemplateTypeDefinition.Tags`. Tags vary significantly across template types.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### Validate template suitability before generating

Before calling `Email.GenerateAdhocEmail`, verify:
1. `template.Enabled == true`.
2. Confirm ad-hoc support using `template.TemplateTypeDefinition.IsAdhoc` or the header's `IsAdhoc` (note: `IsAdhoc` is not a property on the root `EmailTemplate` object).
3. Ensure context entity IDs in `ids[]` match the template's `TemplateType`.
4. Ensure the template references a valid configured sending account (`fkEmailAccountRowId != 0`).

Do not select templates based on fuzzy name matching alone without checking these flags.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/Email.cs` | `macro_convention` — `references/standards/macro_conventions.md`

### `GenerateAdhocEmail.ids` is template-context dependent

`ids` is a list of UUID context identifiers. The required entity represented by those UUIDs depends on the selected `TemplateType`. Do not assume all email templates expect open-order IDs; verify the relevant template context before supplying IDs.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### `GenerateFreeTextEmail` requires context IDs and `templateType`

`GenerateFreeTextEmail` does not accept arbitrary recipient email addresses directly. It requires context `ids` (`Guid[]`) and a `templateType` string to identify recipient data from the underlying system entity.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### API success does not guarantee final mailbox delivery

`GenerateAdhocEmail` and `GenerateFreeTextEmail` return `isComplete = true` and a list of `FailedRecipients`. A successful API call confirms the generation and dispatch attempt was executed by the server; it does not guarantee final delivery by external receiving mail exchangers. Inspect `response.FailedRecipients` for immediate transmission failures.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/email.json`

### Marketplace messaging policies and proxy emails

Ensure messages sent to marketplace-provided buyer addresses comply with the communication policy of the originating `Source` / `SubSource`. Do not assume a channel proxy email permits arbitrary marketing or off-platform communication.

**Source:** `macro_convention` — `references/standards/macro_conventions.md`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders serve as the context entity for order-related email templates
- [`processed_orders`](processed_orders.md) — Dispatch advice emails are typically generated for processed orders
- [`customers`](customers.md) — Recipient delivery email addresses originate from customer order data

---

## Related Workflows

- (Used in automated customer notification, exception alerts, and dispatch confirmation macros)
