---
title: Print Service and Documentation
slug: print_service
related_concepts: [open_orders, shipping, pickwaves]
related_workflows: []
sources:
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/PrintService.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/Controllers/PrintZone.cs
  - type: sdk_source
    ref: vendor/LinnworksNetSDK/ClassBase/CreatePDFResult.cs
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/printservice.json
  - type: public_api_spec
    ref: vendor/PublicApiSpecs/1.0/orders.json
  - type: macro_convention
    ref: references/standards/macro_conventions.md
---

## Purpose

The document generation, template rendering, and physical print routing subsystem in Linnworks.

`PrintService` renders PDF documents from configured Linnworks layout templates (e.g. invoices,
picklists, stock item barcode labels, warehouse transfer manifests, and packing slips). It also
supports virtual printer routing (directing print jobs to local hardware via the Linnworks Print
Service client) and Print Zones (dynamically routing jobs to specific warehouse packing stations).

> [!NOTE]
> **Separation of Concerns:** Carrier integrations generate outbound shipping labels and tracking
> numbers through the shipping integration workflow. `PrintService` handles Linnworks template/PDF
> rendering and print routing, but is not the carrier rating or label-generation engine.

---

## Core Request and Routing Fields

| Field | Type | Description |
|---|---|---|
| `templateID` | `int32` | Numeric identifier of the Linnworks design template. |
| `templateType` | `string` | Template category/family string. Obtain valid types via `PrintService.GetTemplateList`. |
| `printerName` | `string` | Name of the virtual printer destination configured in Linnworks. |
| `printZoneCode` | `string` | Warehouse print zone code. When provided, overrides the template's printer for that zone. |
| `IDs` | `Guid[]` | List of context entity UUIDs (e.g. `pkOrderId` for order invoices, `pkTransferId` for transfers). |

---

## Important Models

| Model | Description |
|---|---|
| `CreatePDFResult` | PDF generation result: `URL` (direct download URL to generated PDF), `IdsProcessed` (`Guid[]`), `ProcessedIds`, `PageCount`, `PrintErrors` (`string[]`). |
| `TemplateHeader` | Summary template model returned by `GetTemplateList`: `TemplateId` (int32), `TemplateType` (string), `TemplateName` (string). |
| `PrintZone` | Warehouse print zone definition: `ZoneCode`, `ZoneName`, `Description`. |
| `VirtualPrinter` | Virtual printer definition configured in Linnworks Print Service. |

Use `get_model` to see complete field schemas.

---

## Endpoint Decision Table

| Requirement | Preferred Endpoint | Important Semantics |
|---|---|---|
| **Discover configured layout templates** | `PrintService.GetTemplateList` | Takes optional `templateType` (empty string returns all templates). |
| **Preview a template layout as PDF** | `PrintService.PrintTemplatePreview` | Generates a preview PDF for a specific `templateId`. |
| **Render generic entities/orders/transfers** | `PrintService.CreatePDFfromJobForceTemplate` | Renders templates for a list of entity `IDs` (`Guid[]`). |
| **Render stock-item barcode labels with counts** | `PrintService.CreatePDFfromJobForceTemplateWithQuantities` | Accepts `IDsAndQuantities` (`StockItemId` + print quantity). |
| **Render StockIn / PrintingKey documents** | `PrintService.CreatePDFfromJobForceTemplateStockIn` | Renders documents using `PrintingKeys` (e.g. PO receiving). |
| **Generate return shipping label PDF** | `PrintService.CreateReturnShippingLabelsPDF` | Generates customer return shipping label PDFs for an order. |
| **Discover warehouse print zones** | `PrintZone.GetAllPrintZones` | Returns configured print routing zones across the account. |
| **Mark order invoice print state** | `Orders.SetInvoicesPrinted` | Sets the invoice printed flag (cannot run on locked/parked orders). |
| **Mark order picklist print state** | `Orders.SetPickListPrinted` | Sets the picklist printed flag (cannot run on locked/parked orders). |
| **Mark order shipping label print state** | `Orders.SetLabelsPrinted` | Sets the shipping label printed flag on an open order. |

---

## Common Operations

- `PrintService.GetTemplateList` — Discover all active document templates configured in the account.
- `PrintService.CreatePDFfromJobForceTemplate` — Render and optionally dispatch PDF print jobs for open orders or transfers.
- `PrintService.CreatePDFfromJobForceTemplateWithQuantities` — Generate stock item barcode labels with specified print quantities.
- `PrintZone.GetAllPrintZones` — Retrieve all active print zones for station-based print routing.
- `Orders.SetInvoicesPrinted` / `Orders.SetPickListPrinted` / `Orders.SetLabelsPrinted` — Update order print-status flags.

---

## Gotchas & Operational Rules

### Do not invent `templateType` strings — use `GetTemplateList`

`templateType` is a string identifying the template family. Retrieve available templates dynamically via `PrintService.GetTemplateList()` and use the returned `templateType`, `templateName`, and `templateID` values rather than hardcoding guessed string constants.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/PrintService.cs`

### Printer routing precedence

When calling PDF generation endpoints:
1. If `printerName` is explicitly supplied, Linnworks routes the job to that virtual printer.
2. If `printerName` is omitted/null, Linnworks uses the printer specified in the template layout settings.
3. If `printZoneCode` is provided, it overrides the destination printer if the template has a configured printer for that print zone.

Do not assume `printZoneCode` is itself a printer name; it is a routing zone code.

**Source:** `sdk_source` — `vendor/LinnworksNetSDK/Controllers/PrintService.cs`

### Setting print status flags does NOT generate PDFs

Calling `Orders.SetInvoicesPrinted` or `Orders.SetLabelsPrinted` only updates the metadata boolean flags on the order records in the database.
- It does NOT render a PDF or trigger physical printing.
- The flag indicates to operators, UI grids, and rules that the document has been marked as printed.
- To produce physical output, call the appropriate `PrintService.CreatePDFfromJobForceTemplate` endpoint.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Print-status mutation restrictions vary by endpoint

`Orders.SetInvoicesPrinted`, `Orders.SetPickListPrinted`, `Orders.ClearInvoicePrinted`, and `Orders.ClearPickListPrinted` explicitly reject locked or parked orders. Verify `order.GeneralInfo.IsLocked == false` before attempting to modify invoice or picklist print flags.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/orders.json`

### Template context must match the supplied entity IDs

`PrintService.CreatePDFfromJobForceTemplate` accepts a list of UUIDs (`IDs`). The supplied IDs must match the entity context expected by the chosen template (e.g. `pkOrderId` for invoice templates, `pkTransferId` for warehouse transfer manifests). Supplying mismatched IDs can result in generation failures.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/printservice.json`

### Virtual printer discovery is legacy / deprecated

`PrintService.VP_GetPrinters` is marked as deprecated in public API specifications. Do not build new macro automation dependencies around direct virtual printer enumeration without verifying support on the target account.

**Source:** `public_api_spec` — `vendor/PublicApiSpecs/1.0/printservice.json`

---

## Related Concepts

- [`open_orders`](open_orders.md) — Open orders serve as the source entity for invoices, picklists, and pack slips
- [`shipping`](shipping.md) — Carrier integrations generate shipping labels and tracking data; PrintService handles Linnworks document rendering
- [`pickwaves`](pickwaves.md) — Picklists and tote sheets are generated for picking waves

---

## Related Workflows

- (Used in automated invoice generation, batch picklist printing, and packing station routing macros)
